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

            int size = ChunkCoord.ChunkSize;
            float seedX = worldSeed * 1.7f + coord.X * 100f;
            float seedZ = worldSeed * 3.1f + coord.Z * 100f;

            float amplitude = GetBiomeAmplitude(sample.Biome);
            int octaves = GetBiomeOctaves(sample.Biome);
            float baseHeight = sample.Elevation;

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wx = (coord.X * size + x) * 0.01f;
                    float wz = (coord.Z * size + z) * 0.01f;

                    float noise = SimplexNoise.OctaveNoise(
                        wx, wz, octaves, 0.5f, 2f, seedX, seedZ);

                    float height = baseHeight + (noise - 0.5f) * amplitude;
                    chunkData.Heightmap[z * size + x] = Mathf.Clamp01(height);
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

            // TerrainData
            var terrainData = new TerrainData();
            // heightmapResolution must be power-of-2 + 1. 257 is valid (256+1).
            terrainData.heightmapResolution = size + 1;
            terrainData.size = new Vector3(size, MaxHeight, size);

            // Convert flat heightmap to Unity's 2D format
            float[,] heights = new float[size + 1, size + 1];
            for (int z = 0; z <= size; z++)
            {
                for (int x = 0; x <= size; x++)
                {
                    int sx = Mathf.Min(x, size - 1);
                    int sz = Mathf.Min(z, size - 1);
                    heights[z, x] = chunkData.Heightmap[sz * size + sx];
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
