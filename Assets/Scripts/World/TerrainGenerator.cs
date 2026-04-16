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
        public const float MaxHeight = 200f;

        /// <summary>Mesh resolution per axis. 33×33 = 1,089 vertices. Fast to create.</summary>
        private const int MeshRes = 33;

        // Cached biome materials — shared across all chunks of same biome
        private static readonly Material[] _biomeMaterials = new Material[System.Enum.GetValues(typeof(BiomeType)).Length];

        // Shared seed state for edge computation
        private static float _seedX, _seedZ;
        private static PlanetSkeleton _activeSkeleton;

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

            _seedX = worldSeed * 1.7f;
            _seedZ = worldSeed * 3.1f;
            _activeSkeleton = skeleton;

            float amplitude = GetBiomeAmplitude(sample.Biome);
            int octaves = GetBiomeOctaves(sample.Biome);

            for (int z = 0; z < hmRes; z++)
            {
                for (int x = 0; x < hmRes; x++)
                {
                    float worldX = coord.X * chunkSize + x * step;
                    float worldZ = coord.Z * chunkSize + z * step;

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

                    float nx = worldX * 0.01f;
                    float nz = worldZ * 0.01f;
                    float noise = SimplexNoise.OctaveNoise(nx, nz, octaves, 0.5f, 2f, _seedX, _seedZ);

                    chunkData.Heightmap[z * hmRes + x] = Mathf.Clamp01(baseHeight + (noise - 0.5f) * amplitude);
                }
            }
        }

        /// <summary>
        /// Create a mesh-based terrain GameObject from chunk heightmap data.
        /// Returns the MeshRenderer's GameObject (NOT a Terrain component).
        /// </summary>
        public static GameObject CreateTerrain(ChunkData chunkData)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            int chunkSize = ChunkCoord.ChunkSize;
            int hmRes = ChunkData.HeightmapRes;

            // Build mesh
            var mesh = new Mesh();
            mesh.name = $"Terrain_{coord.X}_{coord.Z}";

            int vertCount = (MeshRes + 1) * (MeshRes + 1);
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            float cellSize = (float)chunkSize / MeshRes;

            for (int z = 0; z <= MeshRes; z++)
            {
                for (int x = 0; x <= MeshRes; x++)
                {
                    int idx = z * (MeshRes + 1) + x;

                    float localX = x * cellSize;
                    float localZ = z * cellSize;

                    // Sample height from stored heightmap (bilinear interpolation)
                    float hmX = (float)x / MeshRes * (hmRes - 1);
                    float hmZ = (float)z / MeshRes * (hmRes - 1);
                    float height = SampleHeightmap(chunkData.Heightmap, hmRes, hmX, hmZ) * MaxHeight;

                    vertices[idx] = new Vector3(localX, height, localZ);
                    uvs[idx] = new Vector2((float)x / MeshRes, (float)z / MeshRes);
                }
            }

            // Build triangles
            int triCount = MeshRes * MeshRes * 6;
            var triangles = new int[triCount];
            int tri = 0;
            for (int z = 0; z < MeshRes; z++)
            {
                for (int x = 0; x < MeshRes; x++)
                {
                    int bl = z * (MeshRes + 1) + x;
                    int br = bl + 1;
                    int tl = (z + 1) * (MeshRes + 1) + x;
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
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Create GameObject
            var go = new GameObject($"Chunk_{coord.X}_{coord.Z}");
            go.transform.position = coord.WorldOrigin;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetBiomeMaterial(chunkData.Biome);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // MeshCollider for physics (player walking)
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

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

        /// <summary>Get or create a cached material for a biome.</summary>
        private static Material GetBiomeMaterial(BiomeType biome)
        {
            int idx = (int)biome;
            if (idx < 0 || idx >= _biomeMaterials.Length) idx = 0;

            if (_biomeMaterials[idx] == null)
            {
                var color = BiomeTable.BaseColor(biome);
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else
                    mat.color = color;
                _biomeMaterials[idx] = mat;
            }

            return _biomeMaterials[idx];
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

        /// <summary>Compute height at world position (for edge stitching and spawn).</summary>
        public static float ComputeHeightAtWorldPos(int worldX, int worldZ, BiomeType biome, float baseFallback)
        {
            float nx = worldX * 0.01f;
            float nz = worldZ * 0.01f;

            float baseHeight = baseFallback;
            if (_activeSkeleton != null)
            {
                float skX = (float)worldX / ChunkCoord.ChunkSize + _activeSkeleton.Width / 2f;
                float skZ = (float)worldZ / ChunkCoord.ChunkSize + _activeSkeleton.Height / 2f;
                skX = ((skX % _activeSkeleton.Width) + _activeSkeleton.Width) % _activeSkeleton.Width;
                skZ = ((skZ % _activeSkeleton.Height) + _activeSkeleton.Height) % _activeSkeleton.Height;

                int sx0 = Mathf.FloorToInt(skX);
                int sz0 = Mathf.FloorToInt(skZ);
                int sx1 = (sx0 + 1) % _activeSkeleton.Width;
                int sz1 = (sz0 + 1) % _activeSkeleton.Height;
                float fx = skX - sx0;
                float fz = skZ - sz0;

                baseHeight = Mathf.Lerp(
                    Mathf.Lerp(_activeSkeleton.GetElevation(sx0, sz0), _activeSkeleton.GetElevation(sx1, sz0), fx),
                    Mathf.Lerp(_activeSkeleton.GetElevation(sx0, sz1), _activeSkeleton.GetElevation(sx1, sz1), fx),
                    fz);
            }

            float amplitude = GetBiomeAmplitude(biome);
            int octaves = GetBiomeOctaves(biome);
            float noise = SimplexNoise.OctaveNoise(nx, nz, octaves, 0.5f, 2f, _seedX, _seedZ);
            return Mathf.Clamp01(baseHeight + (noise - 0.5f) * amplitude);
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
