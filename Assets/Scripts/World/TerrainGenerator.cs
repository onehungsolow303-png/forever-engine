using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Builds mesh-based terrain from ChunkData heightmaps.
    /// Uses simple mesh planes instead of Unity Terrain for fast streaming.
    /// Each chunk = one mesh with displaced vertices + MeshCollider.
    /// </summary>
    public static class TerrainGenerator
    {
        // Only used by the skeleton fallback to scale its [0..1] noise output
        // into absolute meters. Baked data already ships as meters.
        private const float SkeletonSurfaceScaleMeters = 400f;

        // Single shared terrain material for all chunks (Phase 4B). Biome distinction
        // is now conveyed via per-vertex colors written by BiomeVertexColorer.
        private static Material _sharedTerrainMaterial;

        /// <summary>Returns the shared TerrainBlendedMaterial, loading it on first
        /// access from Resources. Falls back to a procedural URP Lit grey if the
        /// asset is missing (populator hasn't been run).</summary>
        private static Material GetSharedTerrainMaterial()
        {
            if (_sharedTerrainMaterial != null) return _sharedTerrainMaterial;

            _sharedTerrainMaterial = Resources.Load<Material>("TerrainBlendedMaterial");
            if (_sharedTerrainMaterial != null) return _sharedTerrainMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _sharedTerrainMaterial = new Material(shader);
            if (_sharedTerrainMaterial.HasProperty("_BaseColor"))
                _sharedTerrainMaterial.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f));
            else
                _sharedTerrainMaterial.color = new Color(0.5f, 0.5f, 0.5f);
            Debug.LogWarning("[TerrainGenerator] TerrainBlendedMaterial not found in Resources; falling back to procedural grey. Run 'Forever Engine → Create Terrain Blended Material'.");
            return _sharedTerrainMaterial;
        }

        /// <summary>
        /// Generate heightmap data for a chunk. Writes into chunkData.Heightmap.
        /// </summary>
        public static void GenerateHeightmap(ChunkData chunkData, PlanetSkeleton skeleton, int worldSeed)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            var sample = skeleton.SampleAt(coord);
            chunkData.Biome = sample.Biome;
            chunkData.BaseElevation = sample.Elevation;

            int chunkSize = ChunkCoord.ChunkSize;
            int hmRes = ChunkData.HeightmapRes;
            // Step so heightmap[0] is at chunk's left edge AND heightmap[hmRes-1] is at right edge.
            // Previous formula (chunkSize / hmRes) left a `step`-sized gap at boundaries, meaning
            // adjacent chunks sampled noise at different world positions → visible seams.
            // With (hmRes - 1), both chunks sample identical noise at their shared edge.
            float step = (float)chunkSize / (hmRes - 1);

            for (int z = 0; z < hmRes; z++)
            {
                for (int x = 0; x < hmRes; x++)
                {
                    float worldX = coord.X * chunkSize + x * step;
                    float worldZ = coord.Z * chunkSize + z * step;
                    chunkData.Heightmap[z * hmRes + x] =
                        SampleHeightAt(worldX, worldZ, sample.Biome, skeleton, worldSeed);
                }
            }
        }

        /// <summary>
        /// Position-deterministic height sampler. Returns the same value for the
        /// same (worldX, worldZ) regardless of which chunk calls it. Amplitude is
        /// bilinear-blended across the 4 surrounding skeleton cells' biomes so
        /// cross-biome chunk edges don't produce a height step. The `biomeHint`
        /// argument is ignored for terrain math; kept for API compatibility.
        /// </summary>
        public static float SampleHeightAt(float worldX, float worldZ, BiomeType biomeHint,
                                            PlanetSkeleton skeleton, int worldSeed)
        {
            // Phase 2: if a baked world is available, use it as the authoritative
            // elevation source. Baked heights are absolute meters; the existing
            // skeleton-noise path is the dev/test fallback when no bake exists.
            var bakedSource = BakedChunkSourceRuntime.Get();
            if (bakedSource != null)
            {
                if (bakedSource.TryGetTileForWorld(worldX, worldZ, out var macro))
                {
                    return ForeverEngine.Core.World.Baked.BakedElevationSynth.Sample(
                        worldX, worldZ, macro, macro.Header.LayerId);
                }
                // No tile covers (worldX, worldZ) — ocean / void. Return 0f to match
                // BakedChunkSource.SampleMacroElevation's missing-tile fallback.
                return 0f;
            }

            int chunkSize = ChunkCoord.ChunkSize;
            float skeletonX = (worldX / chunkSize + skeleton.Width / 2f) % skeleton.Width;
            float skeletonZ = (worldZ / chunkSize + skeleton.Height / 2f) % skeleton.Height;
            if (skeletonX < 0) skeletonX += skeleton.Width;
            if (skeletonZ < 0) skeletonZ += skeleton.Height;

            int sx0 = Mathf.FloorToInt(skeletonX);
            int sz0 = Mathf.FloorToInt(skeletonZ);
            int sx1 = (sx0 + 1) % skeleton.Width;
            int sz1 = (sz0 + 1) % skeleton.Height;
            float fx = skeletonX - sx0;
            float fz = skeletonZ - sz0;

            float baseHeight = Mathf.Lerp(
                Mathf.Lerp(skeleton.GetElevation(sx0, sz0), skeleton.GetElevation(sx1, sz0), fx),
                Mathf.Lerp(skeleton.GetElevation(sx0, sz1), skeleton.GetElevation(sx1, sz1), fx),
                fz);

            // Bilinear-blend per-biome amplitude across the same 4 cells used for
            // elevation interpolation. Two chunks sharing an edge now sample
            // identical (blended) amplitude at that edge → no step.
            BiomeType b00 = skeleton.GetBiome(sx0, sz0);
            BiomeType b10 = skeleton.GetBiome(sx1, sz0);
            BiomeType b01 = skeleton.GetBiome(sx0, sz1);
            BiomeType b11 = skeleton.GetBiome(sx1, sz1);
            float amplitude = Mathf.Lerp(
                Mathf.Lerp(GetBiomeAmplitude(b00), GetBiomeAmplitude(b10), fx),
                Mathf.Lerp(GetBiomeAmplitude(b01), GetBiomeAmplitude(b11), fx),
                fz);

            // Octaves: use max across the 4 corners. OctaveNoise normalizes to 0-1
            // regardless of count, so higher octaves only add finer detail multiplied
            // by the small amplitude in low-amp biomes — visually invisible there,
            // preserves mountain detail at mountain-adjacent boundaries.
            int octaves = Mathf.Max(
                Mathf.Max(GetBiomeOctaves(b00), GetBiomeOctaves(b10)),
                Mathf.Max(GetBiomeOctaves(b01), GetBiomeOctaves(b11)));

            float seedX = worldSeed * 1.7f;
            float seedZ = worldSeed * 3.1f;
            float noise = SimplexNoise.OctaveNoise(worldX * 0.01f, worldZ * 0.01f,
                                                   octaves, 0.5f, 2f, seedX, seedZ);
            return Mathf.Clamp01(baseHeight + (noise - 0.5f) * amplitude) * SkeletonSurfaceScaleMeters;
        }

        /// <summary>
        /// Create a mesh-based terrain GameObject from chunk heightmap data.
        /// Returns a root GameObject with a LODGroup and three child meshes
        /// at decreasing resolutions. LOD0 keeps the MeshCollider and shadows;
        /// LOD1 keeps shadows; LOD2 is render-only.
        /// </summary>
        public static GameObject CreateTerrain(ChunkData chunkData, bool needsCollider = true)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);

            // Root GameObject — LODGroup lives here, one child per LOD level.
            var root = new GameObject($"Chunk_{coord.X}_{coord.Z}");
            root.transform.position = coord.WorldOrigin;

            var material = GetSharedTerrainMaterial();

            // Single-LOD build: LOD swaps between 33 / 17 / 9 vert meshes at
            // chunk level produced both visible cracks at neighbor boundaries
            // (different vertex densities on shared edges) and a pop on every
            // threshold cross. With ~1ms frame time headroom, a uniform LOD0
            // across all rendered chunks is cheaper than the swap artifacts.
            var lod0 = BuildLodMesh(chunkData, 33, material, castShadows: true, addCollider: true);
            lod0.transform.SetParent(root.transform, worldPositionStays: false);
            lod0.name = "LOD0";

            return root;
        }

        private static GameObject BuildLodMesh(
            ChunkData chunkData, int res, Material material, bool castShadows, bool addCollider)
        {
            int chunkSize = ChunkCoord.ChunkSize;
            int hmRes = ChunkData.HeightmapRes;

            var mesh = new Mesh { name = $"LodMesh_{res}" };
            int vertCount = (res + 1) * (res + 1);
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            float cellSize = (float)chunkSize / res;

            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);

            for (int z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++)
                {
                    int idx = z * (res + 1) + x;
                    float localX = x * cellSize;
                    float localZ = z * cellSize;
                    float hmX = (float)x / res * (hmRes - 1);
                    float hmZ = (float)z / res * (hmRes - 1);
                    float height = SampleHeightmap(chunkData.Heightmap, hmRes, hmX, hmZ);
                    vertices[idx] = new Vector3(localX, height, localZ);
                    // Tile the biome material 128x per chunk (one tile per 2m).
                    // 32x (8m/tile) read as a flat wash — ground textures lose
                    // their per-pixel detail at player height. 128x (2m/tile)
                    // matches the natural scale of grass/sand/rock textures.
                    const float tilesPerChunk = 128f;
                    uvs[idx] = new Vector2((float)x / res * tilesPerChunk, (float)z / res * tilesPerChunk);
                }
            }

            int triCount = res * res * 6;
            var triangles = new int[triCount];
            int tri = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int bl = z * (res + 1) + x;
                    int br = bl + 1;
                    int tl = (z + 1) * (res + 1) + x;
                    int tr = tl + 1;
                    triangles[tri++] = bl;
                    triangles[tri++] = tl;
                    triangles[tri++] = br;
                    triangles[tri++] = br;
                    triangles[tri++] = tl;
                    triangles[tri++] = tr;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals(); // Server-mode: analytical normals removed; let Unity compute smooth normals from heightmap geometry.
            mesh.RecalculateBounds();

            // Phase 4B: per-vertex biome color blend. The TerrainBlended shader
            // (on GetSharedTerrainMaterial) multiplies albedo by mesh.colors so
            // cross-biome seams gradient instead of hard-edging.
            var bakedSource = ForeverEngine.Procedural.BakedChunkSourceRuntime.Get();
            BiomeVertexColorer.WriteVertexColors(
                mesh,
                chunkOrigin: coord.WorldOrigin,
                chunkSize: chunkSize,
                res: res,
                source: bakedSource);

            var go = new GameObject();
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;

            if (addCollider)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
            return go;
        }

        /// <summary>Destroy a chunk's terrain.</summary>
        public static void DestroyTerrain(GameObject terrainGO)
        {
            if (terrainGO == null) return;
            Object.Destroy(terrainGO);
        }

        /// <summary>Overload for backwards compatibility with Terrain parameter.</summary>
        public static void DestroyTerrain(Terrain terrain)
        {
            if (terrain != null) Object.Destroy(terrain.gameObject);
        }

        /// <summary>Bilinear sample from a flat heightmap array.</summary>
        private static float SampleHeightmap(float[] heightmap, int res, float x, float z)
        {
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            int x1 = Mathf.Min(x0 + 1, res - 1);
            int z1 = Mathf.Min(z0 + 1, res - 1);
            x0 = Mathf.Clamp(x0, 0, res - 1);
            z0 = Mathf.Clamp(z0, 0, res - 1);

            float fx = x - Mathf.FloorToInt(x);
            float fz = z - Mathf.FloorToInt(z);

            float h00 = heightmap[z0 * res + x0];
            float h10 = heightmap[z0 * res + x1];
            float h01 = heightmap[z1 * res + x0];
            float h11 = heightmap[z1 * res + x1];

            return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fz);
        }

        private static float GetBiomeAmplitude(BiomeType biome) => biome switch
        {
            BiomeType.Mountain => 0.3f,
            BiomeType.BorealForest => 0.08f,
            BiomeType.TemperateForest => 0.06f,
            BiomeType.Grassland => 0.03f,
            BiomeType.Desert => 0.05f,
            BiomeType.Tundra => 0.02f,
            BiomeType.IceSheet => 0.01f,
            BiomeType.Ocean => 0.01f,
            BiomeType.Beach => 0.01f,
            _ => 0.05f,
        };

        private static int GetBiomeOctaves(BiomeType biome) => biome switch
        {
            BiomeType.Mountain => 5,
            _ => 3,
        };
    }
}
