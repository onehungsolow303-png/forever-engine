// Assets/Scripts/World/TerrainGenerator.cs
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

            // Biome-specific noise parameters
            float amplitude = GetBiomeAmplitude(sample.Biome);
            int octaves = GetBiomeOctaves(sample.Biome);
            float baseHeight = sample.Elevation;

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    // World-space position for noise sampling
                    float wx = (coord.X * size + x) * 0.01f; // Scale down for noise
                    float wz = (coord.Z * size + z) * 0.01f;

                    float noise = SimplexNoise.OctaveNoise(
                        wx, wz, octaves, 0.5f, 2f, seedX, seedZ);

                    // Center noise around base elevation
                    float height = baseHeight + (noise - 0.5f) * amplitude;
                    chunkData.Heightmap[z * size + x] = Mathf.Clamp01(height);
                }
            }
        }

        /// <summary>
        /// Create a Unity Terrain GameObject from chunk heightmap data.
        /// Returns the Terrain component.
        /// </summary>
        public static Terrain CreateTerrain(ChunkData chunkData)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            int size = ChunkCoord.ChunkSize;

            // Create TerrainData
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = size + 1; // Unity requires power of 2 + 1
            terrainData.size = new Vector3(size, MaxHeight, size);

            // Convert flat heightmap to 2D array (Unity format)
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

            // Apply biome terrain layer (color)
            var layer = new TerrainLayer();
            // Use a simple white texture tinted by biome color
            layer.diffuseTexture = Texture2D.whiteTexture;
            layer.tileSize = new Vector2(4f, 4f);
            terrainData.terrainLayers = new[] { layer };

            // Create GameObject
            var go = Terrain.CreateTerrainGameObject(terrainData);
            go.name = $"Chunk_{coord.X}_{coord.Z}";
            go.transform.position = coord.WorldOrigin;

            // Tint by biome
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = CreateBiomeMaterial(chunkData.Biome);

            return terrain;
        }

        /// <summary>Destroy a chunk's terrain GameObject.</summary>
        public static void DestroyTerrain(Terrain terrain)
        {
            if (terrain == null) return;
            Object.Destroy(terrain.gameObject);
        }

        private static Material CreateBiomeMaterial(BiomeType biome)
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                ?? Shader.Find("Nature/Terrain/Standard");
            var mat = new Material(shader);
            // The terrain will be tinted by the biome base color
            // Full texture painting is Plan B (Terrain & Decoration)
            return mat;
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
