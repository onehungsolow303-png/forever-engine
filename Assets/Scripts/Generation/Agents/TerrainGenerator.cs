using ForeverEngine.Generation.Data;
using ForeverEngine.Generation.Utility;

namespace ForeverEngine.Generation.Agents
{
    public static class TerrainGenerator
    {
        public struct TerrainResult
        {
            public float[] Elevation;   // Flattened [y*w+x], 0-1
            public float[] Moisture;    // Flattened [y*w+x], 0-1
            public byte[] TerrainColor; // Flattened [y*w+x*3], RGB
            public bool[] Walkability;  // Flattened [y*w+x]
            public int Width, Height;
        }

        public static TerrainResult Generate(GenerationRequest request, MapProfile profile)
        {
            int w = request.Width, h = request.Height;
            PerlinNoise.Seed(request.Seed);

            var result = new TerrainResult
            {
                Width = w, Height = h,
                Elevation = new float[w * h],
                Moisture = new float[w * h],
                TerrainColor = new byte[w * h * 3],
                Walkability = new bool[w * h]
            };

            float scale = 0.02f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    result.Elevation[idx] = PerlinNoise.Octave(x * scale, y * scale, 6);
                    result.Moisture[idx] = PerlinNoise.Octave(x * scale + 100f, y * scale + 100f, 4);

                    float elev = result.Elevation[idx];
                    result.Walkability[idx] = elev >= profile.WalkabilityThreshold;

                    // Terrain color from elevation/moisture
                    int ci = idx * 3;
                    if (elev < 0.3f) { result.TerrainColor[ci] = 40; result.TerrainColor[ci+1] = 40; result.TerrainColor[ci+2] = 50; } // Deep stone
                    else if (elev < profile.WalkabilityThreshold) { result.TerrainColor[ci] = 60; result.TerrainColor[ci+1] = 55; result.TerrainColor[ci+2] = 50; } // Wall
                    else if (elev < 0.65f) { result.TerrainColor[ci] = 120; result.TerrainColor[ci+1] = 100; result.TerrainColor[ci+2] = 80; } // Floor
                    else { result.TerrainColor[ci] = 90; result.TerrainColor[ci+1] = 85; result.TerrainColor[ci+2] = 75; } // Elevated
                }
            }
            return result;
        }
    }
}
