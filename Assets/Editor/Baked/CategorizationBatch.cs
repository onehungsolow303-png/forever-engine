#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    // Batchmode entry point for the pack categorization flow. Mirrors the
    // interactive AssetPackCategorizationWindow.SaveCatalog path but requires
    // no GUI — invoke from the command line via:
    //
    //   Unity.exe -batchmode -nographics -quit -projectPath ... \
    //             -executeMethod ForeverEngine.Procedural.Editor.CategorizationBatch.Run
    //
    // Scans Assets/ for packs, classifies each via PackBiomeHeuristics, and
    // writes Assets/Resources/AssetPackBiomeCatalog.asset containing only
    // Outdoor/Unknown packs with their heuristic-suggested biomes.
    public static class CategorizationBatch
    {
        private const string CatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";

        public static void Run()
        {
            // Start fresh — the old catalog had wholesale categorization we want
            // to purge. The new catalog is written from scratch below.
            if (File.Exists(CatalogPath))
            {
                AssetDatabase.DeleteAsset(CatalogPath);
                Debug.Log($"[CategorizationBatch] Deleted existing {CatalogPath}.");
            }

            var packs = AssetPackScanner.ScanRoot(Application.dataPath);
            Debug.Log($"[CategorizationBatch] Discovered {packs.Length} packs.");

            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            AssetDatabase.CreateAsset(catalog, CatalogPath);

            // Default-deny: ONLY PackRole.OutdoorBiomeContent feeds the catalog.
            // Unknown packs must be explicitly curated via the
            // AssetPackCategorizationWindow before they can contribute — batch
            // ingest refuses them to prevent dungeon/creature/tool packs from
            // leaking in on unfortunate keyword matches.
            int outdoor = 0, indoor = 0, stamper = 0, tool = 0, creatures = 0, unknownRejected = 0;
            var entries = new List<AssetPackBiomeEntry>();

            foreach (var p in packs)
            {
                var classification = PackBiomeHeuristics.Classify(p.Name);
                switch (classification.Role)
                {
                    case PackRole.IndoorExcluded: indoor++;          continue;
                    case PackRole.StamperOnly:    stamper++;         continue;
                    case PackRole.Tool:           tool++;            continue;
                    case PackRole.Creatures:      creatures++;       continue;
                    case PackRole.Unknown:
                        Debug.LogWarning($"[CategorizationBatch] Unknown pack '{p.Name}' — rejected from auto-ingest. Curate via Forever Engine/Bake/Categorize Asset Packs.");
                        unknownRejected++;
                        continue;
                }

                // OutdoorBiomeContent only.
                var biomes = classification.SuggestedBiomes;
                if (biomes == null || biomes.Length == 0)
                {
                    Debug.LogWarning($"[CategorizationBatch] Outdoor pack '{p.Name}' has no biome suggestions — skipping. Add biome hints to PackBiomeHeuristics table.");
                    continue;
                }

                var entry = new AssetPackBiomeEntry
                {
                    PackName = p.Name,
                    SuitableBiomes = biomes,
                };
                PackPrefabHarvester.Harvest(p.AbsolutePath, entry);
                entries.Add(entry);
                outdoor++;
            }

            catalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[CategorizationBatch] Wrote {entries.Count} entries to {CatalogPath}. " +
                $"Outdoor={outdoor}, Indoor={indoor}, StamperOnly={stamper}, " +
                $"Tool={tool}, Creatures={creatures}, UnknownRejected={unknownRejected}."
            );
        }
    }
}
#endif
