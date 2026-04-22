using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Batchmode-friendly full bake pipeline. Runs CreateTestBakeScene +
    /// catalog-seed-from-heuristics + MacroBake + HeroBake end-to-end and
    /// writes a status file at C:/Dev/phase1_bake_results.txt.
    ///
    /// Invoke: Unity.exe -batchmode -projectPath "C:/Dev/Forever engine"
    ///                   -executeMethod ForeverEngine.Procedural.Editor.BatchBakeAll.Run
    ///                   -quit -logFile "C:/Dev/batchmode_bake.log"
    /// </summary>
    public static class BatchBakeAll
    {
        private const string StatusPath = "C:/Dev/phase1_bake_results.txt";
        private const string HeroZoneAssetPath = "Assets/TestHeroZone.asset";
        private const string CatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";

        public static void Run()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var log = new List<string>();
            void L(string m) { Debug.Log($"[BatchBakeAll] {m}"); log.Add($"[{sw.Elapsed:mm\\:ss}] {m}"); }

            try
            {
                L("Starting headless bake pipeline.");

                // 1. Build the test scene. CreateTestBakeScene already handles
                //    batching properly + shows a final DisplayDialog. The dialog
                //    returns immediately (no-op) in batchmode.
                L("Phase 1: CreateTestBakeScene.Build()");
                CreateTestBakeScene.Build();
                L("  Test scene created at Assets/Scenes/TestBake.unity");

                // 2. Seed the catalog from scanner + heuristics. No user tagging.
                //    Produces an AssetPackBiomeCatalog with whatever packs the
                //    scanner finds + whichever biomes the heuristics assign.
                L("Phase 2: Seed AssetPackBiomeCatalog from heuristics.");
                SeedCatalogFromHeuristics(L);

                // 3. Macro bake all terrains in scene as multi-tile layout.
                L("Phase 3: MacroBakeTool.BakeAllTilesInScene()");
                MacroBakeTool.BakeAllTilesInScene();
                var indexPath = "C:/Dev/.shared/baked/planet/layer_0/index.json";
                if (!File.Exists(indexPath))
                    throw new InvalidOperationException($"Macro bake produced no index.json at {indexPath}");
                L($"  Macro bake index written: {indexPath}");

                // 4. Create hero zone asset programmatically.
                L("Phase 4: Create BakedHeroZoneAsset at Assets/TestHeroZone.asset");
                var existingZone = AssetDatabase.LoadAssetAtPath<BakedHeroZoneAsset>(HeroZoneAssetPath);
                BakedHeroZoneAsset zoneAsset;
                if (existingZone != null)
                {
                    zoneAsset = existingZone;
                }
                else
                {
                    zoneAsset = ScriptableObject.CreateInstance<BakedHeroZoneAsset>();
                    AssetDatabase.CreateAsset(zoneAsset, HeroZoneAssetPath);
                }
                zoneAsset.ZoneId = "test_zone";
                zoneAsset.LayerId = 0;
                zoneAsset.WorldMinX = 0f; zoneAsset.WorldMinZ = 0f;
                zoneAsset.WorldMaxX = 256f; zoneAsset.WorldMaxZ = 256f;
                zoneAsset.ResolutionMeters = 1f;
                EditorUtility.SetDirty(zoneAsset);
                AssetDatabase.SaveAssets();
                L("  Hero zone asset configured.");

                // 5. Hero bake. HeroBakeTool.BakeSelectedZone reads Selection.activeObject;
                //    set that programmatically.
                Selection.activeObject = zoneAsset;
                L("Phase 5: HeroBakeTool.BakeSelectedZone()");
                HeroBakeTool.BakeSelectedZone();
                var heroDir = $"C:/Dev/.shared/baked/planet/layer_0/hero/test_zone";
                if (!Directory.Exists(heroDir))
                    throw new InvalidOperationException($"Hero bake produced no output at {heroDir}");
                L($"  Hero bake output: {Directory.GetFiles(heroDir).Length} files in {heroDir}");

                // 6. List final output directory contents
                L("Phase 6: Output listing");
                var root = "C:/Dev/.shared/baked/planet/layer_0";
                foreach (var p in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var fi = new FileInfo(p);
                    L($"  {fi.Length,10} {p.Replace('\\','/')}");
                }

                sw.Stop();
                L($"SUCCESS. Total time: {sw.Elapsed:mm\\:ss}.");
                WriteStatus(success: true, lines: log);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                sw.Stop();
                log.Add($"[{sw.Elapsed:mm\\:ss}] FAIL: {ex.GetType().Name}: {ex.Message}");
                log.Add(ex.StackTrace ?? "(no stack)");
                WriteStatus(success: false, lines: log);
                Debug.LogError($"[BatchBakeAll] {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void SeedCatalogFromHeuristics(Action<string> L)
        {
            var packs = AssetPackScanner.ScanRoot(Application.dataPath);
            L($"  Scanner found {packs.Length} pack directories");

            var catalog = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                L("  Created new AssetPackBiomeCatalog.asset");
            }
            else
            {
                L("  Loaded existing AssetPackBiomeCatalog.asset");
            }

            var entries = new List<AssetPackBiomeEntry>();
            int withBiomes = 0;
            foreach (var p in packs)
            {
                if (p.SuggestedBiomes == null || p.SuggestedBiomes.Length == 0)
                    continue; // skip packs the heuristics couldn't categorize
                var entry = new AssetPackBiomeEntry
                {
                    PackName = p.Name,
                    SuitableBiomes = p.SuggestedBiomes,
                };
                try
                {
                    PackPrefabHarvester.Harvest(p.AbsolutePath, entry);
                }
                catch (Exception harvestEx)
                {
                    L($"    Harvest failed for {p.Name}: {harvestEx.Message}");
                }
                entries.Add(entry);
                withBiomes++;
            }
            catalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            L($"  Categorized {withBiomes} packs via heuristics, " +
              $"{packs.Length - withBiomes} skipped (no keyword match)");

            if (catalog.Entries.Length == 0)
            {
                // MacroBakeTool aborts on empty catalog. Add a single synthetic
                // entry with no prefabs so the bake proceeds (0 props, but still
                // produces heightmap/biome/splat/features files).
                catalog.Entries = new[]
                {
                    new AssetPackBiomeEntry
                    {
                        PackName = "_Synthetic_Empty",
                        SuitableBiomes = new[] { BiomeType.Grassland },
                        TreePrefabs = Array.Empty<GameObject>(),
                        RockPrefabs = Array.Empty<GameObject>(),
                        BushPrefabs = Array.Empty<GameObject>(),
                        StructurePrefabs = Array.Empty<GameObject>(),
                        TerrainMaterials = Array.Empty<Material>(),
                        AmbientAudio = Array.Empty<AudioClip>(),
                    }
                };
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                L("  No heuristic matches; injected synthetic empty entry so bake can proceed.");
            }
        }

        private static void WriteStatus(bool success, List<string> lines)
        {
            try
            {
                var status = new List<string>
                {
                    $"Status: {(success ? "SUCCESS" : "FAILED")}",
                    $"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    "",
                    "Log:",
                };
                status.AddRange(lines);
                File.WriteAllLines(StatusPath, status);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BatchBakeAll] Failed to write status file: {ex.Message}");
            }
        }
    }
}
