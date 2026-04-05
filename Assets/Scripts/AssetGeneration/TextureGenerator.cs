using UnityEngine;
using ForeverEngine.Generation.Utility;

namespace ForeverEngine.AssetGeneration
{
    public static class TextureGenerator
    {
        public static Texture2D GenerateTerrainTexture(int width, int height, Color floorColor, Color wallColor, int seed = 42)
        {
            PerlinNoise.Seed(seed);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float n = PerlinNoise.Octave(x * 0.1f, y * 0.1f, 3);
                    Color c = Color.Lerp(floorColor, wallColor, n);
                    // Add subtle variation
                    float variation = PerlinNoise.Sample(x * 0.5f, y * 0.5f) * 0.1f - 0.05f;
                    c.r = Mathf.Clamp01(c.r + variation);
                    c.g = Mathf.Clamp01(c.g + variation);
                    c.b = Mathf.Clamp01(c.b + variation);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateTileset(int tileSize, int tilesPerRow, Color[] tileColors, int seed = 42)
        {
            int totalTiles = tileColors.Length;
            int rows = (totalTiles + tilesPerRow - 1) / tilesPerRow;
            int w = tileSize * tilesPerRow, h = tileSize * rows;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            PerlinNoise.Seed(seed);
            for (int t = 0; t < totalTiles; t++)
            {
                int col = t % tilesPerRow, row = t / tilesPerRow;
                int ox = col * tileSize, oy = row * tileSize;
                for (int y = 0; y < tileSize; y++)
                    for (int x = 0; x < tileSize; x++)
                    {
                        float n = PerlinNoise.Sample((ox+x)*0.3f, (oy+y)*0.3f) * 0.15f - 0.075f;
                        Color c = tileColors[t];
                        c.r = Mathf.Clamp01(c.r + n); c.g = Mathf.Clamp01(c.g + n); c.b = Mathf.Clamp01(c.b + n);
                        tex.SetPixel(ox + x, oy + y, c);
                    }
            }
            tex.Apply();
            return tex;
        }
    }
}
