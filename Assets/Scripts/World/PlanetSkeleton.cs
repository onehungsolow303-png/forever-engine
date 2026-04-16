// Assets/Scripts/World/PlanetSkeleton.cs
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Planet-wide low-resolution grid storing elevation, temperature, and moisture.
    /// Generated once from a world seed. Each cell maps to ~8 chunks.
    /// Chunks sample this grid for their base terrain parameters.
    /// </summary>
    public class PlanetSkeleton
    {
        public int Width { get; }   // 2048
        public int Height { get; }  // 1024

        private readonly float[] _elevation;
        private readonly float[] _temperature;
        private readonly float[] _moisture;

        public int Seed { get; }

        /// <summary>How many chunks one skeleton cell covers (approximate).</summary>
        public const int CellsPerChunk = 8;

        public PlanetSkeleton(int seed, int width = 2048, int height = 1024)
        {
            Seed = seed;
            Width = width;
            Height = height;
            _elevation = new float[width * height];
            _temperature = new float[width * height];
            _moisture = new float[width * height];

            Generate(seed);
        }

        private void Generate(int seed)
        {
            float seedX = seed * 1.3f;
            float seedZ = seed * 2.7f;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int idx = y * Width + x;

                    // Normalized coords (0-1)
                    float nx = (float)x / Width;
                    float ny = (float)y / Height;

                    // Continental elevation: large-scale noise
                    float continental = SimplexNoise.OctaveNoise(
                        nx * 4f, ny * 4f, 4, 0.5f, 2f, seedX, seedZ);

                    // Tectonic ridges: medium-scale noise for mountain ranges
                    float tectonic = SimplexNoise.OctaveNoise(
                        nx * 8f, ny * 8f, 3, 0.6f, 2.5f, seedX + 500f, seedZ + 500f);
                    tectonic = Mathf.Pow(tectonic, 2f) * 0.4f; // Sharpen ridges

                    // Combined elevation
                    _elevation[idx] = Mathf.Clamp01(continental * 0.7f + tectonic * 0.3f);

                    // Temperature: latitude-based (row 0 and Height = poles, middle = equator)
                    float latitudeFactor = 1f - Mathf.Abs(ny - 0.5f) * 2f; // 0 at poles, 1 at equator
                    float elevationCooling = Mathf.Max(0f, _elevation[idx] - 0.5f) * 0.8f; // Mountains are colder
                    _temperature[idx] = Mathf.Clamp01(latitudeFactor - elevationCooling);

                    // Moisture: noise-based + bonus near ocean + rain shadow from elevation
                    float baseMoisture = SimplexNoise.OctaveNoise(
                        nx * 6f, ny * 6f, 3, 0.5f, 2f, seedX + 1000f, seedZ + 1000f);
                    float oceanBonus = _elevation[idx] < 0.32f ? 0.3f : 0f;
                    float rainShadow = Mathf.Max(0f, _elevation[idx] - 0.6f) * 0.5f;
                    _moisture[idx] = Mathf.Clamp01(baseMoisture + oceanBonus - rainShadow);
                }
            }

            // Smooth ocean proximity moisture (simple 3-pass blur on ocean-adjacent cells)
            SpreadOceanMoisture();
        }

        private void SpreadOceanMoisture()
        {
            // 3 passes: cells near ocean get moisture boost that fades inland
            var bonus = new float[Width * Height];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 1; y < Height - 1; y++)
                {
                    for (int x = 1; x < Width - 1; x++)
                    {
                        int idx = y * Width + x;
                        if (_elevation[idx] < 0.30f) { bonus[idx] = 0.25f; continue; }

                        float avg = (bonus[(y - 1) * Width + x] + bonus[(y + 1) * Width + x]
                            + bonus[y * Width + x - 1] + bonus[y * Width + x + 1]) * 0.25f;
                        bonus[idx] = avg * 0.85f; // Decay factor
                    }
                }
            }
            for (int i = 0; i < _moisture.Length; i++)
                _moisture[i] = Mathf.Clamp01(_moisture[i] + bonus[i]);
        }

        /// <summary>
        /// Sample skeleton data for a chunk coordinate.
        /// Maps chunk coords to skeleton grid coords.
        /// </summary>
        public SkeletonSample SampleAt(ChunkCoord coord)
        {
            // Map chunk coord to skeleton grid position
            // Center the skeleton around (0,0) so spawn is in the middle
            int sx = (coord.X + Width / 2) % Width;
            int sy = (coord.Z + Height / 2) % Height;
            if (sx < 0) sx += Width;
            if (sy < 0) sy += Height;

            int idx = sy * Width + sx;
            return new SkeletonSample
            {
                Elevation = _elevation[idx],
                Temperature = _temperature[idx],
                Moisture = _moisture[idx],
                Biome = BiomeTable.Lookup(_elevation[idx], _temperature[idx], _moisture[idx]),
            };
        }

        /// <summary>Direct elevation access by skeleton grid coords (for bilinear interpolation).</summary>
        public float GetElevation(int sx, int sz)
        {
            sx = ((sx % Width) + Width) % Width;
            sz = ((sz % Height) + Height) % Height;
            return _elevation[sz * Width + sx];
        }

        public struct SkeletonSample
        {
            public float Elevation;
            public float Temperature;
            public float Moisture;
            public BiomeType Biome;
        }
    }
}
