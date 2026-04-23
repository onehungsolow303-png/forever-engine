#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Batchmode bake entry that RESPECTS the existing curated AssetPackBiomeCatalog.
    /// Unlike BatchBakeAll which re-seeds the catalog from heuristics (wholesale
    /// categorization — the Phase A bug we fixed), this entry:
    ///   1. Builds the test bake scene (idempotent — CreateTestBakeScene)
    ///   2. Runs MacroBakeTool.BakeAllTilesInSceneOrThrow() against the CURRENT
    ///      catalog.Entries (whatever Phase A left in place)
    ///   3. Exits
    ///
    /// Does NOT run Phase 4-5 (hero bake) — macro is the only layer prop-affected
    /// and the server serves macro tiles. Re-run Phase 4 separately if needed.
    ///
    /// Invoke: Unity.exe -batchmode -projectPath "C:/Dev/Forever engine"
    ///                   -executeMethod ForeverEngine.Procedural.Editor.BakeWithCuratedCatalog.Run
    ///                   -quit -logFile -
    /// </summary>
    public static class BakeWithCuratedCatalog
    {
        public static void Run()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Debug.Log("[BakeWithCuratedCatalog] Starting — catalog WILL NOT be re-seeded.");

                // Phase 1: build test scene (idempotent).
                CreateTestBakeScene.Build();
                Debug.Log("[BakeWithCuratedCatalog] Scene ready.");

                // Phase 2: verify catalog has entries (abort if empty — caller
                // should have curated it).
                var catalog = AssetDatabase.LoadAssetAtPath<ForeverEngine.Procedural.AssetPackBiomeCatalog>(
                    "Assets/Resources/AssetPackBiomeCatalog.asset");
                if (catalog == null || catalog.Entries == null || catalog.Entries.Length == 0)
                    throw new InvalidOperationException(
                        "AssetPackBiomeCatalog is missing or empty. Run Phase A curation first; do NOT re-seed from heuristics.");
                int prefabCount = 0;
                foreach (var e in catalog.Entries)
                {
                    if (e.TreePrefabs != null) prefabCount += e.TreePrefabs.Length;
                    if (e.RockPrefabs != null) prefabCount += e.RockPrefabs.Length;
                    if (e.BushPrefabs != null) prefabCount += e.BushPrefabs.Length;
                    if (e.StructurePrefabs != null) prefabCount += e.StructurePrefabs.Length;
                }
                Debug.Log($"[BakeWithCuratedCatalog] Catalog OK: {catalog.Entries.Length} entries, {prefabCount} prefab refs.");

                // Phase 3: macro bake.
                MacroBakeTool.BakeAllTilesInSceneOrThrow();
                var indexPath = "C:/Dev/.shared/baked/planet/layer_0/index.json";
                if (!File.Exists(indexPath))
                    throw new InvalidOperationException($"Macro bake produced no index.json at {indexPath}");

                // Phase 4: list output tree.
                var root = "C:/Dev/.shared/baked/planet/layer_0";
                long totalBytes = 0;
                int fileCount = 0;
                foreach (var p in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var fi = new FileInfo(p);
                    totalBytes += fi.Length;
                    fileCount++;
                    if (p.EndsWith("props.bin")) Debug.Log($"[BakeWithCuratedCatalog]   {fi.Length,10} {p.Replace('\\', '/')}");
                }
                Debug.Log($"[BakeWithCuratedCatalog] Wrote {fileCount} files, {totalBytes:N0} bytes total, in {sw.Elapsed:mm\\:ss}.");
                sw.Stop();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BakeWithCuratedCatalog] FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
