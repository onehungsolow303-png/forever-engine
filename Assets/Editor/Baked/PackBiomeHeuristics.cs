using System;
using System.Collections.Generic;
using ForeverEngine.Procedural;

namespace ForeverEngine.Procedural.Editor
{
    public enum PackRole
    {
        Unknown,                // No classification; user must curate manually.
        OutdoorBiomeContent,    // Goes into AssetPackBiomeCatalog.
        IndoorExcluded,         // Never in biome catalog (DungeonArchitect territory).
        StamperOnly,            // Heightmap stamps, never prop spawning.
        Tool,                   // Not content (e.g. DungeonArchitect).
    }

    public struct ClassificationResult
    {
        public PackRole Role;
        public BiomeType[] SuggestedBiomes;
    }

    public static class PackBiomeHeuristics
    {
        // Hard-coded pack table. Order matters — first substring match wins, so
        // specific identifiers (e.g. "mega stamp") must precede broader ones (e.g.
        // "procedural worlds" which Gaia also lives under).
        private static readonly (string[] keywords, PackRole role, BiomeType[] biomes)[] PackTable =
        {
            // --- INDOOR (DungeonArchitect-scope; never in biome catalog) ---
            (new[] { "lordenfel" },                                        PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "3dforge" },                                          PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "naked_singularity", "dragon_catacomb", "catacomb" }, PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "multistory dungeons", "multistory_dungeons" },       PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "waltww", "cavedungeontoolkit" },                     PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "realistic natural cave" },                           PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),

            // --- TOOLS ---
            (new[] { "dungeonarchitect", "coderespawn" },                  PackRole.Tool,                Array.Empty<BiomeType>()),

            // --- STAMPER (must precede Gaia — both live under "Procedural Worlds") ---
            (new[] { "mega stamp" },                                       PackRole.StamperOnly,         Array.Empty<BiomeType>()),

            // --- OUTDOOR (goes into catalog with pre-checked biomes) ---
            (new[] { "naturemanufacture" },                                PackRole.OutdoorBiomeContent, new[] { BiomeType.Mountain, BiomeType.Taiga, BiomeType.BorealForest }),
            (new[] { "eternal temple" },                                   PackRole.OutdoorBiomeContent, new[] { BiomeType.TemperateForest, BiomeType.Mountain }),
            (new[] { "magic pig" },                                        PackRole.OutdoorBiomeContent, new[] { BiomeType.Grassland, BiomeType.TemperateForest }),
            (new[] { "gaia" },                                             PackRole.OutdoorBiomeContent, new[] { BiomeType.Grassland, BiomeType.TemperateForest }),
        };

        // Legacy keyword-based biome suggester for Unknown packs. Still useful when
        // a new pack is imported that isn't in the hard-coded table yet.
        private static readonly (string keyword, BiomeType[] biomes)[] KeywordFallback =
        {
            ("nordic",     new[] { BiomeType.BorealForest, BiomeType.Taiga }),
            ("boreal",     new[] { BiomeType.BorealForest, BiomeType.Taiga }),
            ("taiga",      new[] { BiomeType.Taiga, BiomeType.BorealForest }),
            ("temperate",  new[] { BiomeType.TemperateForest, BiomeType.Grassland }),
            ("deciduous",  new[] { BiomeType.TemperateForest }),
            ("desert",     new[] { BiomeType.Desert, BiomeType.AridSteppe }),
            ("arid",       new[] { BiomeType.AridSteppe, BiomeType.Desert }),
            ("tundra",     new[] { BiomeType.Tundra, BiomeType.IceSheet }),
            ("savanna",    new[] { BiomeType.Savanna }),
            ("tropical",   new[] { BiomeType.TropicalRainforest }),
            ("rainforest", new[] { BiomeType.TropicalRainforest }),
            ("grass",      new[] { BiomeType.Grassland }),
            ("mountain",   new[] { BiomeType.Mountain }),
            ("alpine",     new[] { BiomeType.Mountain, BiomeType.Tundra }),
            ("ocean",      new[] { BiomeType.Ocean }),
            ("beach",      new[] { BiomeType.Beach }),
        };

        public static ClassificationResult Classify(string packName)
        {
            if (string.IsNullOrEmpty(packName))
                return new ClassificationResult { Role = PackRole.Unknown, SuggestedBiomes = Array.Empty<BiomeType>() };

            var lower = packName.ToLowerInvariant();
            foreach (var (keywords, role, biomes) in PackTable)
            {
                foreach (var kw in keywords)
                {
                    if (lower.Contains(kw))
                        return new ClassificationResult { Role = role, SuggestedBiomes = biomes };
                }
            }

            // Unknown pack — try keyword-based biome suggestion so the categorization
            // window still pre-checks sensible biomes. Role stays Unknown so the UI
            // flags it for manual review.
            var result = new HashSet<BiomeType>();
            foreach (var (keyword, biomes) in KeywordFallback)
            {
                if (lower.Contains(keyword))
                    foreach (var b in biomes) result.Add(b);
            }
            var arr = new BiomeType[result.Count];
            result.CopyTo(arr);
            return new ClassificationResult { Role = PackRole.Unknown, SuggestedBiomes = arr };
        }

        // Backward-compatible wrapper. Callers that only care about biome suggestions
        // (e.g. AssetPackCategorizationWindow's pre-check logic) can still use this.
        public static BiomeType[] SuggestBiomes(string packName) => Classify(packName).SuggestedBiomes;
    }
}
