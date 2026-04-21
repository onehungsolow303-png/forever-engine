using System.Collections.Generic;
using ForeverEngine.Procedural;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Keyword-based biome suggestion from pack folder name. Returns an empty
    /// array when no keywords match. User confirms/edits in the categorization
    /// window; heuristics just save typing.
    /// </summary>
    public static class PackBiomeHeuristics
    {
        private static readonly (string keyword, BiomeType[] biomes)[] Rules = new[]
        {
            ("nordic",       new[] { BiomeType.BorealForest, BiomeType.Taiga }),
            ("boreal",       new[] { BiomeType.BorealForest, BiomeType.Taiga }),
            ("taiga",        new[] { BiomeType.Taiga, BiomeType.BorealForest }),
            ("temperate",    new[] { BiomeType.TemperateForest, BiomeType.Grassland }),
            ("deciduous",    new[] { BiomeType.TemperateForest }),
            ("desert",       new[] { BiomeType.Desert, BiomeType.AridSteppe }),
            ("arid",         new[] { BiomeType.AridSteppe, BiomeType.Desert }),
            ("tundra",       new[] { BiomeType.Tundra, BiomeType.IceSheet }),
            ("savanna",      new[] { BiomeType.Savanna }),
            ("tropical",     new[] { BiomeType.TropicalRainforest }),
            ("rainforest",   new[] { BiomeType.TropicalRainforest }),
            ("grass",        new[] { BiomeType.Grassland }),
            ("mountain",     new[] { BiomeType.Mountain }),
            ("alpine",       new[] { BiomeType.Mountain, BiomeType.Tundra }),
            ("ocean",        new[] { BiomeType.Ocean }),
            ("beach",        new[] { BiomeType.Beach }),
        };

        public static BiomeType[] SuggestBiomes(string packName)
        {
            var name = packName.ToLowerInvariant();
            var result = new HashSet<BiomeType>();
            foreach (var (keyword, biomes) in Rules)
            {
                if (name.Contains(keyword))
                    foreach (var b in biomes) result.Add(b);
            }
            var arr = new BiomeType[result.Count];
            result.CopyTo(arr);
            return arr;
        }
    }
}
