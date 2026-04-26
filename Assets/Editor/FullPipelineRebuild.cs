#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Single entrypoint replacing FullRebakeWithMatFix. Orchestrates the entire
    /// prop pipeline deterministically:
    ///
    ///   1. NatureManufactureMatFixer — repair HDRP→URP material property drift
    ///   2. CategorizationBatch       — re-seed AssetPackBiomeCatalog from packs
    ///   3. PopulatePrefabRegistry    — walk catalog → PrefabRegistry.asset
    ///   4. StandaloneBuild (clean)   — force-clean player build with timestamp assertion
    ///   5. Bake (macro)              — write props.bin from the curated catalog
    ///   6. Integrity check           — sample GUIDs from props.bin, assert all
    ///                                  resolve in PrefabRegistry.asset
    ///
    /// Any step failing throws loudly — no silent partials. See
    /// docs/superpowers/plans/2026-04-23-prop-pipeline-consolidation.md for the
    /// architectural rationale.
    ///
    /// Invoke headlessly:
    ///   Unity.exe -batchmode -quit -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Procedural.Editor.FullPipelineRebuild.Run
    /// </summary>
    public static class FullPipelineRebuild
    {
        private const string BakedLayerRoot = "C:/Dev/.shared/baked/planet/layer_0";
        private const string CatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";
        private const string RegistryPath = "Assets/Resources/PrefabRegistry.asset";
        private const int IntegritySampleSize = 50;

        [MenuItem("Forever Engine/Bake/Full Pipeline Rebuild")]
        public static void RunMenu() => Run();

        // Convenience batchmode entry: forces PropSourceSelector to synthetic
        // before running the full pipeline. Use when Gaia-authored placements
        // are sparse / tree-instances unpopulated and you want forest-density
        // synthetic content for a playtest.
        public static void RunSynthetic()
        {
            PropSourceSelector.UseGaiaAuthored = false;
            Debug.Log("[FullPipelineRebuild] PropSourceSelector forced to Synthetic for this run.");
            Run();
        }

        public static void Run()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Log("=== Step 1/6: NatureManufactureMatFixer ===");
                ForeverEngine.Editor.NatureManufactureMatFixer.Run();

                Log("=== Step 2/6: CategorizationBatch (re-seed AssetPackBiomeCatalog) ===");
                CategorizationBatch.Run();
                AssertCatalogHasEntries();

                Log("=== Step 3/6: PopulatePrefabRegistry ===");
                PopulatePrefabRegistry.Run();
                AssertRegistryCoversCatalog();

                Log("=== Step 4/6: StandaloneBuild (force-clean) ===");
                ForeverEngine.Editor.StandaloneBuild.Build();

                Log("=== Step 5/6: Bake (macro with curated catalog) ===");
                RunBake();

                Log("=== Step 6/6: Integrity check — sample props.bin GUIDs vs PrefabRegistry ===");
                RunIntegrityCheck();

                Log($"=== DONE — pipeline clean in {sw.Elapsed:mm\\:ss} ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FullPipelineRebuild] FAIL after {sw.Elapsed:mm\\:ss}: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void AssertCatalogHasEntries()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(CatalogPath);
            if (catalog == null || catalog.Entries == null || catalog.Entries.Length == 0)
                throw new InvalidOperationException(
                    $"[FullPipelineRebuild] CategorizationBatch left {CatalogPath} missing or empty.");
            Log($"  catalog: {catalog.Entries.Length} entries");
        }

        private static void AssertRegistryCoversCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(CatalogPath);
            var registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(RegistryPath);
            if (registry == null)
                throw new InvalidOperationException($"[FullPipelineRebuild] {RegistryPath} missing after PopulatePrefabRegistry.");

            var catalogGuids = new HashSet<string>();
            foreach (var entry in catalog.Entries)
            {
                if (entry == null) continue;
                CollectGuids(entry.TreePrefabs, catalogGuids);
                CollectGuids(entry.RockPrefabs, catalogGuids);
                CollectGuids(entry.BushPrefabs, catalogGuids);
                CollectGuids(entry.StructurePrefabs, catalogGuids);
            }

            var missing = new List<string>();
            foreach (var guid in catalogGuids)
                if (registry.Resolve(guid) == null) missing.Add(guid);

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"[FullPipelineRebuild] PrefabRegistry missing {missing.Count}/{catalogGuids.Count} catalog GUIDs. " +
                    $"First 5: {string.Join(", ", missing.Take(5))}");

            Log($"  registry covers all {catalogGuids.Count} catalog GUIDs");
        }

        private static void CollectGuids(GameObject[] prefabs, HashSet<string> into)
        {
            if (prefabs == null) return;
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                var path = AssetDatabase.GetAssetPath(p);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) into.Add(guid);
            }
        }

        private static void RunBake()
        {
            // Inlined from BakeWithCuratedCatalog so EditorApplication.Exit calls
            // there don't short-circuit the integrity check (step 6).
            CreateTestBakeScene.Build();
            MacroBakeTool.BakeAllTilesInSceneOrThrow();

            var indexPath = $"{BakedLayerRoot}/index.json";
            if (!File.Exists(indexPath))
                throw new InvalidOperationException($"[FullPipelineRebuild] Bake produced no index.json at {indexPath}");
            Log($"  bake wrote index.json at {indexPath}");
        }

        private static void RunIntegrityCheck()
        {
            if (!Directory.Exists(BakedLayerRoot))
                throw new InvalidOperationException($"[FullPipelineRebuild] Baked layer root missing: {BakedLayerRoot}");

            var propsFiles = Directory.GetFiles(BakedLayerRoot, "props.bin", SearchOption.AllDirectories);
            if (propsFiles.Length == 0)
                throw new InvalidOperationException($"[FullPipelineRebuild] No props.bin files under {BakedLayerRoot}.");

            var allGuids = new List<string>();
            foreach (var f in propsFiles)
                allGuids.AddRange(ReadPropsGuids(f));

            if (allGuids.Count == 0)
                throw new InvalidOperationException($"[FullPipelineRebuild] {propsFiles.Length} props.bin file(s) contain zero placements.");

            var registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(RegistryPath);
            if (registry == null)
                throw new InvalidOperationException($"[FullPipelineRebuild] {RegistryPath} missing at integrity-check time.");

            int sampleSize = Math.Min(IntegritySampleSize, allGuids.Count);
            var rng = new System.Random(Environment.TickCount);
            var sample = new HashSet<string>();
            // Sample without replacement via index set — robust for small allGuids.
            var indices = Enumerable.Range(0, allGuids.Count).ToArray();
            for (int i = 0; i < indices.Length - 1 && sample.Count < sampleSize; i++)
            {
                int swapIdx = rng.Next(i, indices.Length);
                (indices[i], indices[swapIdx]) = (indices[swapIdx], indices[i]);
                sample.Add(allGuids[indices[i]]);
            }

            var missing = new List<string>();
            foreach (var guid in sample)
                if (registry.Resolve(guid) == null) missing.Add(guid);

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"[FullPipelineRebuild] Integrity FAIL: {missing.Count}/{sample.Count} sampled props.bin GUIDs " +
                    $"missing from PrefabRegistry. First 5: {string.Join(", ", missing.Take(5))}. " +
                    $"This means props.bin and PrefabRegistry disagree — client will render an empty world.");

            Log($"  integrity OK: sampled {sample.Count}/{allGuids.Count} GUIDs across {propsFiles.Length} props.bin files, all resolve in registry");
        }

        private static List<string> ReadPropsGuids(string path)
        {
            var result = new List<string>();
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var guid = br.ReadString();
                br.ReadString();     // prefab resource path
                br.ReadSingle();     // wx
                br.ReadSingle();     // wy
                br.ReadSingle();     // wz
                br.ReadSingle();     // yaw
                br.ReadSingle();     // uniform scale
                result.Add(guid);
            }
            return result;
        }

        private static void Log(string msg) => Debug.Log($"[FullPipelineRebuild] {msg}");
    }
}
#endif
