using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Builds a Unity Terrain from a ChunkData's heightmap.
    /// Samples the PlanetSkeleton for base elevation, adds Simplex noise octaves,
    /// applies biome-specific modifiers (mountains=jagged, plains=smooth).
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>Max terrain height in Unity units (meters).</summary>
        public const float MaxHeight = 200f;

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
            float step = (float)chunkSize / hmRes; // 2m per sample at 128 res

            // Seed offsets are world-seed-only (NOT per-chunk) so noise is continuous.
            _seedX = worldSeed * 1.7f;
            _seedZ = worldSeed * 3.1f;
            _activeSkeleton = skeleton;
            float seedX = _seedX;
            float seedZ = _seedZ;

            for (int z = 0; z < hmRes; z++)
            {
                for (int x = 0; x < hmRes; x++)
                {
                    // World-space position — continuous across all chunks
                    float worldX = coord.X * chunkSize + x * step;
                    float worldZ = coord.Z * chunkSize + z * step;

                    // Sample skeleton at this exact world position (not just chunk center)
                    // This blends base elevation smoothly across chunk boundaries
                    float skeletonX = (float)(worldX / chunkSize + skeleton.Width / 2) % skeleton.Width;
                    float skeletonZ = (float)(worldZ / chunkSize + skeleton.Height / 2) % skeleton.Height;
                    if (skeletonX < 0) skeletonX += skeleton.Width;
                    if (skeletonZ < 0) skeletonZ += skeleton.Height;

                    // Bilinear interpolation of skeleton elevation
                    int sx0 = Mathf.FloorToInt(skeletonX);
                    int sz0 = Mathf.FloorToInt(skeletonZ);
                    int sx1 = (sx0 + 1) % skeleton.Width;
                    int sz1 = (sz0 + 1) % skeleton.Height;
                    float fx = skeletonX - sx0;
                    float fz = skeletonZ - sz0;

                    float e00 = skeleton.GetElevation(sx0, sz0);
                    float e10 = skeleton.GetElevation(sx1, sz0);
                    float e01 = skeleton.GetElevation(sx0, sz1);
                    float e11 = skeleton.GetElevation(sx1, sz1);
                    float baseHeight = Mathf.Lerp(
                        Mathf.Lerp(e00, e10, fx),
                        Mathf.Lerp(e01, e11, fx),
                        fz);

                    // Local detail noise — uses world coords so it's seamless
                    float nx = worldX * 0.01f;
                    float nz = worldZ * 0.01f;

                    float amplitude = GetBiomeAmplitude(sample.Biome);
                    int octaves = GetBiomeOctaves(sample.Biome);

                    float noise = SimplexNoise.OctaveNoise(
                        nx, nz, octaves, 0.5f, 2f, seedX, seedZ);

                    float height = baseHeight + (noise - 0.5f) * amplitude;
                    chunkData.Heightmap[z * hmRes + x] = Mathf.Clamp01(height);
                }
            }
        }

        /// <summary>
        /// Create a Unity Terrain GameObject from chunk heightmap data.
        /// Includes collider, biome-colored texture, and proper positioning.
        /// </summary>
        public static Terrain CreateTerrain(ChunkData chunkData)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            int size = ChunkCoord.ChunkSize;

            // TerrainData — 65 resolution (power-of-2 + 1). 4m per sample. Fast.
            var terrainData = new TerrainData();
            int res = 65;
            terrainData.heightmapResolution = res;
            terrainData.size = new Vector3(size, MaxHeight, size);

            // Build terrain heightmap from stored 128×128 data.
            // Terrain res (129) closely matches stored res (128) — direct 1:1 mapping
            // with edge sample computed at world position for seamless stitching.
            int hmRes = ChunkData.HeightmapRes; // 128
            float[,] heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    if (x == res - 1 || z == res - 1)
                    {
                        // Edge: compute at world position for seamless stitching
                        float wx = (float)x / (res - 1) * size;
                        float wz = (float)z / (res - 1) * size;
                        heights[z, x] = ComputeHeightAtWorldPos(
                            coord.X * size + Mathf.RoundToInt(wx),
                            coord.Z * size + Mathf.RoundToInt(wz),
                            chunkData.Biome,
                            chunkData.BaseElevation);
                    }
                    else
                    {
                        // Interior: direct copy (129-1 = 128 = hmRes)
                        int sx = Mathf.Min(x, hmRes - 1);
                        int sz = Mathf.Min(z, hmRes - 1);
                        heights[z, x] = chunkData.Heightmap[sz * hmRes + sx];
                    }
                }
            }
            terrainData.SetHeights(0, 0, heights);

            // Create terrain GameObject (includes TerrainCollider automatically)
            var go = Terrain.CreateTerrainGameObject(terrainData);
            go.name = $"Chunk_{coord.X}_{coord.Z}";
            go.transform.position = coord.WorldOrigin;

            var terrain = go.GetComponent<Terrain>();

            // Apply biome color via a simple material — avoids URP Terrain Lit shader
            // issues in builds. Uses standard URP Lit or fallback Standard shader.
            var biomeColor = BiomeTable.BaseColor(chunkData.Biome);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", biomeColor);
            else
                mat.color = biomeColor;
            terrain.materialTemplate = mat;

            // Flush to ensure terrain data (including collider) is immediately available.
            terrain.Flush();

            return terrain;
        }

        /// <summary>
        /// Get the terrain surface height at a world position.
        /// Returns 0 if no terrain exists at that position.
        /// </summary>
        public static float GetHeightAt(ChunkData chunkData, float localX, float localZ)
        {
            int size = ChunkCoord.ChunkSize;
            int ix = Mathf.Clamp(Mathf.FloorToInt(localX), 0, size - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(localZ), 0, size - 1);
            return chunkData.Heightmap[iz * size + ix] * MaxHeight;
        }

        /// <summary>Destroy a chunk's terrain GameObject.</summary>
        public static void DestroyTerrain(Terrain terrain)
        {
            if (terrain == null) return;
            Object.Destroy(terrain.gameObject);
        }

        // Shared seed offsets — must match GenerateHeightmap exactly
        private static float _seedX, _seedZ;
        private static PlanetSkeleton _activeSkeleton;

        /// <summary>
        /// Compute height at an exact world position. Used for edge samples
        /// so chunk boundaries match perfectly. Uses the same noise + skeleton
        /// sampling as GenerateHeightmap.
        /// </summary>
        private static float ComputeHeightAtWorldPos(int worldX, int worldZ, BiomeType biome, float baseFallback)
        {
            float nx = worldX * 0.01f;
            float nz = worldZ * 0.01f;

            // Sample skeleton at this world position (bilinear interpolation)
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
            BiomeType.Mountain => 6,
            BiomeType.Desert => 4,
            _ => 4,
        };
    }
}
