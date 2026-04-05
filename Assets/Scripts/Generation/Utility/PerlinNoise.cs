namespace ForeverEngine.Generation.Utility
{
    public static class PerlinNoise
    {
        private static readonly int[] Perm = new int[512];

        public static void Seed(int seed)
        {
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            var rng = new System.Random(seed);
            for (int i = 255; i > 0; i--) { int j = rng.Next(i + 1); (p[i], p[j]) = (p[j], p[i]); }
            for (int i = 0; i < 512; i++) Perm[i] = p[i & 255];
        }

        public static float Sample(float x, float y)
        {
            int xi = (int)System.Math.Floor(x) & 255, yi = (int)System.Math.Floor(y) & 255;
            float xf = x - (float)System.Math.Floor(x), yf = y - (float)System.Math.Floor(y);
            float u = Fade(xf), v = Fade(yf);
            int aa = Perm[Perm[xi] + yi], ab = Perm[Perm[xi] + yi + 1];
            int ba = Perm[Perm[xi + 1] + yi], bb = Perm[Perm[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return (Lerp(x1, x2, v) + 1f) / 2f; // Normalize to 0-1
        }

        public static float Octave(float x, float y, int octaves, float persistence = 0.5f, float lacunarity = 2f)
        {
            float total = 0f, amplitude = 1f, frequency = 1f, maxVal = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += Sample(x * frequency, y * frequency) * amplitude;
                maxVal += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return total / maxVal;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);
        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 3;
            return (h == 0 ? x + y : h == 1 ? -x + y : h == 2 ? x - y : -x - y);
        }
    }
}
