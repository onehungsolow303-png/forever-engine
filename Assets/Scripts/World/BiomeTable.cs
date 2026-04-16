// Assets/Scripts/World/BiomeTable.cs
namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Biome types following the Whittaker model (temperature × moisture).
    /// </summary>
    public enum BiomeType
    {
        Ocean,
        Beach,
        Desert,
        AridSteppe,
        Savanna,
        Grassland,
        TemperateForest,
        TropicalRainforest,
        BorealForest,
        Taiga,
        Tundra,
        IceSheet,
        Mountain,
        River,
    }

    /// <summary>
    /// Whittaker biome lookup from temperature and moisture values (0-1 each).
    /// Elevation overrides produce Mountain, Ocean, and Beach.
    /// </summary>
    public static class BiomeTable
    {
        /// <summary>
        /// Determine biome from elevation, temperature, and moisture (all 0-1).
        /// </summary>
        public static BiomeType Lookup(float elevation, float temperature, float moisture)
        {
            // Elevation overrides
            if (elevation < 0.28f) return BiomeType.Ocean;
            if (elevation < 0.32f) return BiomeType.Beach;
            if (elevation > 0.75f) return BiomeType.Mountain;

            // Whittaker grid: temperature (rows) × moisture (columns)
            if (temperature > 0.7f)
            {
                if (moisture > 0.7f) return BiomeType.TropicalRainforest;
                if (moisture > 0.3f) return BiomeType.Savanna;
                return BiomeType.Desert;
            }
            if (temperature > 0.5f)
            {
                if (moisture > 0.7f) return BiomeType.TemperateForest;
                if (moisture > 0.3f) return BiomeType.Grassland;
                return BiomeType.AridSteppe;
            }
            if (temperature > 0.3f)
            {
                if (moisture > 0.7f) return BiomeType.BorealForest;
                if (moisture > 0.3f) return BiomeType.Taiga;
                return BiomeType.Tundra;
            }
            // Cold
            if (moisture > 0.5f) return BiomeType.IceSheet;
            return BiomeType.Tundra;
        }

        /// <summary>Base terrain color for a biome (used for terrain texture tinting).</summary>
        public static UnityEngine.Color BaseColor(BiomeType biome) => biome switch
        {
            BiomeType.Ocean => new UnityEngine.Color(0.1f, 0.2f, 0.5f),
            BiomeType.Beach => new UnityEngine.Color(0.9f, 0.85f, 0.6f),
            BiomeType.Desert => new UnityEngine.Color(0.85f, 0.75f, 0.5f),
            BiomeType.AridSteppe => new UnityEngine.Color(0.7f, 0.65f, 0.4f),
            BiomeType.Savanna => new UnityEngine.Color(0.6f, 0.7f, 0.3f),
            BiomeType.Grassland => new UnityEngine.Color(0.3f, 0.6f, 0.2f),
            BiomeType.TemperateForest => new UnityEngine.Color(0.15f, 0.45f, 0.15f),
            BiomeType.TropicalRainforest => new UnityEngine.Color(0.1f, 0.5f, 0.1f),
            BiomeType.BorealForest => new UnityEngine.Color(0.2f, 0.4f, 0.25f),
            BiomeType.Taiga => new UnityEngine.Color(0.35f, 0.5f, 0.35f),
            BiomeType.Tundra => new UnityEngine.Color(0.6f, 0.65f, 0.6f),
            BiomeType.IceSheet => new UnityEngine.Color(0.9f, 0.95f, 1.0f),
            BiomeType.Mountain => new UnityEngine.Color(0.5f, 0.5f, 0.5f),
            BiomeType.River => new UnityEngine.Color(0.2f, 0.4f, 0.7f),
            _ => UnityEngine.Color.magenta,
        };
    }
}
