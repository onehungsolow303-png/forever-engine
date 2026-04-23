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

            int outdoor = 0, unknown = 0, excluded = 0, stamper = 0, tool = 0, emptyBiomes = 0;
            var entries = new List<AssetPackBiomeEntry>();

            foreach (var p in packs)
            {
                var classification = PackBiomeHeuristics.Classify(p.Name);
                switch (classification.Role)
                {
                    case PackRole.IndoorExcluded: excluded++; continue;
                    case PackRole.StamperOnly:    stamper++;  continue;
                    case PackRole.Tool:           tool++;     continue;
                }

                // Outdoor + Unknown both get cataloged, using Classify's biome
                // suggestions. Unknown packs with no matching biome keywords
                // produce empty assignments — log and skip them rather than
                // adding a useless empty entry.
                var biomes = classification.SuggestedBiomes;
                if (biomes == null || biomes.Length == 0)
                {
                    Debug.LogWarning($"[CategorizationBatch] Pack '{p.Name}' ({classification.Role}) has no biome suggestions — skipping.");
                    emptyBiomes++;
                    continue;
                }

                var entry = new AssetPackBiomeEntry
                {
                    PackName = p.Name,
                    SuitableBiomes = biomes,
                };
                PackPrefabHarvester.Harvest(p.AbsolutePath, entry);
                entries.Add(entry);

                if (classification.Role == PackRole.OutdoorBiomeContent) outdoor++;
                else unknown++;
            }

            catalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[CategorizationBatch] Wrote {entries.Count} entries to {CatalogPath}. " +
                $"Outdoor={outdoor}, Unknown(auto)={unknown}, Excluded={excluded}, " +
                $"StamperOnly={stamper}, Tool={tool}, EmptyBiomes(skipped)={emptyBiomes}."
            );
        }
    }
}
#endif
