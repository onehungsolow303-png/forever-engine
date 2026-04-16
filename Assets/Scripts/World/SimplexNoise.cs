// Assets/Scripts/World/SimplexNoise.cs
using UnityEngine;

namespace ForeverEngine.World
{
    /// <summary>
    /// Deterministic 2D Simplex noise. Seed-independent (use coordinate offsets for different seeds).
    /// Provides layered octave noise for terrain generation.
    /// </summary>
    public static class SimplexNoise
    {
        // Permutation table (doubled to avoid overflow)
        private static readonly int[] _perm = new int[512];
        private static readonly int[] _source = {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
            140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
            247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,
            57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
            74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,
            60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
            65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,
            200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
            52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,
            207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
            119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
            129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
            218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,
            81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
            184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,
            222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        static SimplexNoise()
        {
            for (int i = 0; i < 512; i++) _perm[i] = _source[i & 255];
        }

        private static readonly float F2 = 0.5f * (Mathf.Sqrt(3f) - 1f);
        private static readonly float G2 = (3f - Mathf.Sqrt(3f)) / 6f;

        private static readonly int[][] _grad2 = {
            new[]{1,1}, new[]{-1,1}, new[]{1,-1}, new[]{-1,-1},
            new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1}
        };

        /// <summary>Raw 2D Simplex noise, returns -1 to 1.</summary>
        public static float Noise2D(float x, float y)
        {
            float s = (x + y) * F2;
            int i = Mathf.FloorToInt(x + s);
            int j = Mathf.FloorToInt(y + s);

            float t = (i + j) * G2;
            float X0 = i - t, Y0 = j - t;
            float x0 = x - X0, y0 = y - Y0;

            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; } else { i1 = 0; j1 = 1; }

            float x1 = x0 - i1 + G2, y1 = y0 - j1 + G2;
            float x2 = x0 - 1f + 2f * G2, y2 = y0 - 1f + 2f * G2;

            int ii = i & 255, jj = j & 255;

            float n0 = Contribution(x0, y0, ii, jj);
            float n1 = Contribution(x1, y1, ii + i1, jj + j1);
            float n2 = Contribution(x2, y2, ii + 1, jj + 1);

            return 70f * (n0 + n1 + n2);
        }

        private static float Contribution(float x, float y, int gi, int gj)
        {
            float t = 0.5f - x * x - y * y;
            if (t < 0f) return 0f;
            t *= t;
            int[] g = _grad2[_perm[gi + _perm[gj & 255] & 255] & 7];
            return t * t * (g[0] * x + g[1] * y);
        }

        /// <summary>
        /// Layered octave noise (0-1 range). Multiple octaves produce natural-looking terrain.
        /// Seed offset shifts the noise field for different worlds.
        /// </summary>
        public static float OctaveNoise(float x, float y, int octaves, float persistence, float lacunarity, float seedOffsetX, float seedOffsetY)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxVal = 0f;

            for (int o = 0; o < octaves; o++)
            {
                float nx = (x + seedOffsetX) * frequency;
                float ny = (y + seedOffsetY) * frequency;
                total += Noise2D(nx, ny) * amplitude;
                maxVal += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return (total / maxVal + 1f) * 0.5f; // Normalize to 0-1
        }
    }
}
