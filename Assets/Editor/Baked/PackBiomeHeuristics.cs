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
        Creatures,              // Monsters / NPCs / characters — not world props.
    }

    public struct ClassificationResult
    {
        public PackRole Role;
        public BiomeType[] SuggestedBiomes;
    }

    public static class PackBiomeHeuristics
    {
        // Explicit pack table. Order matters — first substring match wins, so
        // specific identifiers (e.g. "mega stamp") must precede broader ones.
        //
        // Policy: default-deny. Only packs explicitly marked OutdoorBiomeContent
        // feed the biome catalog; everything else is excluded from auto-ingest.
        // Unknown packs still get keyword-based biome hints so a curator can
        // pre-check sensible biomes in the AssetPackCategorizationWindow, but
        // CategorizationBatch refuses to auto-ingest them.
        private static readonly (string[] keywords, PackRole role, BiomeType[] biomes)[] PackTable =
        {
            // --- INDOOR (DungeonArchitect-scope; never in biome catalog) ---
            (new[] { "lordenfel" },                                        PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "eternal temple", "eternaltemple" },                  PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "3dforge" },                                          PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "naked_singularity", "dragon_catacomb", "catacomb" }, PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "multistory dungeons", "multistory_dungeons" },       PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "waltww", "cavedungeontoolkit" },                     PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),
            (new[] { "realistic natural cave" },                           PackRole.IndoorExcluded,      Array.Empty<BiomeType>()),

            // --- CREATURES (monsters/NPCs, not world props) ---
            (new[] { "magic pig", "infinity pbr" },                        PackRole.Creatures,           Array.Empty<BiomeType>()),
            (new[] { "generatedmodels" },                                  PackRole.Creatures,           Array.Empty<BiomeType>()),

            // --- TOOLS ---
            (new[] { "dungeonarchitect", "coderespawn" },                  PackRole.Tool,                Array.Empty<BiomeType>()),
            (new[] { "hivemind" },                                         PackRole.Tool,                Array.Empty<BiomeType>()),

            // --- STAMPER (terrain heightmap tooling, never prop spawning) ---
            (new[] { "mega stamp" },                                       PackRole.StamperOnly,         Array.Empty<BiomeType>()),
            (new[] { "gaia user data" },                                   PackRole.StamperOnly,         Array.Empty<BiomeType>()),
            (new[] { "procedural worlds" },                                PackRole.StamperOnly,         Array.Empty<BiomeType>()),

            // --- OUTDOOR (the allowlist — only these feed the biome catalog) ---
            // NatureManufacture Assets is a meta-pack: Meadow / Winter Forest /
            // Summer Forest / Mountain / Rock & Boulders / Tall Grass, etc.
            // Broad biome coverage so the one pack can serve whatever biome the
            // tile sampler picks.
            (new[] { "naturemanufacture" },                                PackRole.OutdoorBiomeContent, new[]
            {
                BiomeType.Grassland,
                BiomeType.TemperateForest,
                BiomeType.BorealForest,
                BiomeType.Taiga,
                BiomeType.Mountain,
            }),
        };

        // Legacy keyword-based biome suggester for Unknown packs. Still useful
        // in the AssetPackCategorizationWindow UI so a human curator sees
        // sensible pre-checked biomes when manually curating an unrecognized
        // pack. Never feeds auto-ingest — that path rejects Unknown outright.
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

            // Unknown pack — keyword-based biome suggestion for UI pre-check hints.
            // Role stays Unknown so auto-ingest (CategorizationBatch) refuses it.
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

        public static BiomeType[] SuggestBiomes(string packName) => Classify(packName).SuggestedBiomes;
    }
}
