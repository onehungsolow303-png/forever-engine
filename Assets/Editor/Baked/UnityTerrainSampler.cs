using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Samples a Unity Terrain (typically Gaia-authored) at arbitrary
    /// resolutions. Heightmap is returned in meters (unnormalized).
    /// Biome / splat / features sampled from the Terrain's paint layers.
    /// </summary>
    public static class UnityTerrainSampler
    {
        public static float[] SampleHeightmap(Terrain terrain, int widthCells, int heightCells, float maxHeightMeters)
        {
            var td = terrain.terrainData;
            var result = new float[widthCells * heightCells];
            for (int z = 0; z < heightCells; z++)
            {
                for (int x = 0; x < widthCells; x++)
                {
                    float nx = (float)x / (widthCells - 1);
                    float nz = (float)z / (heightCells - 1);
                    float normalizedHeight = td.GetInterpolatedHeight(nx, nz) / td.size.y;
                    result[z * widthCells + x] = normalizedHeight * maxHeightMeters;
                }
            }
            return result;
        }

        public static byte[] SampleSplat(Terrain terrain, int widthCells, int heightCells)
        {
            var td = terrain.terrainData;
            int am = td.alphamapResolution;
            var raw = td.GetAlphamaps(0, 0, am, am);
            int layerCount = Mathf.Min(4, raw.GetLength(2));
            var result = new byte[widthCells * heightCells * 4];
            for (int z = 0; z < heightCells; z++)
            {
                for (int x = 0; x < widthCells; x++)
                {
                    float nx = (float)x / (widthCells - 1) * (am - 1);
                    float nz = (float)z / (heightCells - 1) * (am - 1);
                    int ix = Mathf.Clamp(Mathf.RoundToInt(nx), 0, am - 1);
                    int iz = Mathf.Clamp(Mathf.RoundToInt(nz), 0, am - 1);
                    int baseIdx = (z * widthCells + x) * 4;
                    for (int layer = 0; layer < 4; layer++)
                    {
                        byte w = layer < layerCount ? (byte)(Mathf.Clamp01(raw[iz, ix, layer]) * 255) : (byte)0;
                        result[baseIdx + layer] = w;
                    }
                }
            }
            return result;
        }

        public static byte[] SampleBiome(Terrain terrain, int widthCells, int heightCells, BiomeType[] splatLayerToBiome)
        {
            var splat = SampleSplat(terrain, widthCells, heightCells);
            var result = new byte[widthCells * heightCells];
            for (int i = 0; i < result.Length; i++)
            {
                int baseIdx = i * 4;
                int maxLayer = 0;
                byte maxWeight = splat[baseIdx];
                for (int layer = 1; layer < 4; layer++)
                {
                    if (splat[baseIdx + layer] > maxWeight)
                    {
                        maxWeight = splat[baseIdx + layer];
                        maxLayer = layer;
                    }
                }
                var biome = maxLayer < splatLayerToBiome.Length ? splatLayerToBiome[maxLayer] : BiomeType.Grassland;
                result[i] = (byte)biome;
            }
            return result;
        }
    }
}
