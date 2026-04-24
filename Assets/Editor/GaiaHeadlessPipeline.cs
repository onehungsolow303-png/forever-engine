#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Gaia;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2022_2_OR_NEWER
using UnityEditor.Rendering.Universal;
#endif

namespace ForeverEngine.Editor.Gaia
{
    /// <summary>
    /// End-to-end Gaia world creation in one batchmode-friendly entry point.
    /// No menu clicks required.
    ///
    /// Invocation:
    ///   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Editor.Gaia.GaiaHeadlessPipeline.BuildConiferousMedium \
    ///     -logFile "C:/Dev/gaia-headless.log"
    ///
    /// Pipeline:
    ///   1. New empty scene, saved to Assets/Scenes/GaiaWorld_<Biome>_<Size>.unity
    ///   2. Clean any stray broken Terrain objects (null terrainData)
    ///   3. Load BiomePreset + build WorldCreationSettings, call GaiaAPI.CreateGaiaWorld
    ///   4. Poll m_worldCreationRunning via EditorApplication.update until done
    ///      (coroutine completes in the background — deliberately do not -quit
    ///      on entry, because the coroutine needs editor update ticks to run)
    ///   5. Once creation is done, convert Built-In → URP materials, run
    ///      NatureManufactureMatFixer, respawn all Gaia spawners, save, Exit(0)
    ///
    /// Failure modes that call EditorApplication.Exit(1):
    ///   - BiomePreset asset missing
    ///   - CreateGaiaWorld returns false (scene save rejected, settings null)
    ///   - Timeout (20 minutes) without completion
    ///   - Any exception during post-processing
    /// </summary>
    public static class GaiaHeadlessPipeline
    {
        private const string BiomesDir =
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Biomes";
        private const double TimeoutSeconds = 20 * 60;

        // Captured at start-of-run so EditorApplication.update can read them.
        private static string _biomeName;
        private static string _sizeName;
        private static string _scenePath;
        private static DateTime _startedAt;

        // ── Entry points for -executeMethod ─────────────────────────────────

        public static void BuildConiferousSmall() =>
            RunAsync("Coniferous Forest", GaiaWorldBuilder.WorldSize.Small);

        public static void BuildConiferousMedium() =>
            RunAsync("Coniferous Forest", GaiaWorldBuilder.WorldSize.Medium);

        public static void BuildConiferousLarge() =>
            RunAsync("Coniferous Forest", GaiaWorldBuilder.WorldSize.Large);

        public static void BuildAlpineMedium() =>
            RunAsync("Alpine Meadow", GaiaWorldBuilder.WorldSize.Medium);

        public static void BuildGiantMedium() =>
            RunAsync("Giant Forest", GaiaWorldBuilder.WorldSize.Medium);

        // ── Pipeline ────────────────────────────────────────────────────────

        private static void RunAsync(string biomeName, GaiaWorldBuilder.WorldSize size)
        {
            try
            {
                _biomeName = biomeName;
                _sizeName = size.ToString();
                _startedAt = DateTime.UtcNow;

                Log($"=== Step 1/5: new empty scene for biome='{biomeName}' size={size} ===");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory("Assets/Scenes");
                _scenePath = $"Assets/Scenes/GaiaWorld_{biomeName.Replace(' ', '_')}_{size}.unity";
                EditorSceneManager.SaveScene(scene, _scenePath);
                Log($"  scene saved at {_scenePath}");

                Log("=== Step 2/5: clean any broken Terrains (null terrainData) ===");
                CleanBrokenTerrains();

                Log("=== Step 3/5: load biome + kick off GaiaAPI.CreateGaiaWorld ===");
                var biome = AssetDatabase.LoadAssetAtPath<BiomePreset>($"{BiomesDir}/{biomeName}.asset");
                if (biome == null)
                    throw new Exception($"Missing BiomePreset {biomeName} under {BiomesDir}");

                // In batchmode there is no SceneView; Gaia's ExecuteCreateWorld
                // calls SceneView.lastActiveSceneView.camera to position the
                // editor view on the new world and NREs when null, killing the
                // coroutine BEFORE it saves the scene / flips
                // m_worldCreationRunning. Spawning a hidden SceneView here
                // satisfies that call site.
                EnsureSceneViewExists();

                var settings = BuildSettings(biome, size);
                if (!GaiaSessionManager.CreateOrUpdateWorld(settings, executeNow: true, isUpdate: false))
                    throw new Exception("GaiaSessionManager.CreateOrUpdateWorld returned false");

                // Gaia drives its coroutine via EditorApplication.update —
                // unreliable in batchmode (ticks are sparse / non-existent
                // between -executeMethod calls). Instead, synchronously drive
                // m_updateOperationCoroutine.MoveNext() ourselves in a blocking
                // loop until Gaia clears the coroutine reference. This matches
                // what Gaia's own EditorUpdate does, just without the frame
                // dependency.
                Log("  world-creation coroutine started. Driving it to completion...");
                DriveGaiaCoroutineToCompletion();

                // Step 3 only creates the terrain tiles + runs stamper-active
                // spawners (texture splats). The full biome (trees/rocks/grass)
                // needs BiomePreset.CreateBiome which instantiates all
                // BiomeController child Spawner objects; then Spawner.Spawn
                // actually paints each prefab onto the terrain.
                Log("=== Step 4/6: instantiate biome spawners + spawn all ===");
                InstantiateAndSpawnBiome(biome);

                Log("=== Step 5/6: post-processing (URP convert + matfixer) ===");
                CleanBrokenTerrains();
                RunBuiltInToUrpConverter();
                RunNatureManufactureMatFixer();

                Log("=== Step 6/6: save scene ===");
                EditorSceneManager.SaveOpenScenes();

                Log($"=== DONE in {(DateTime.UtcNow - _startedAt).TotalSeconds:F1}s. Scene: {_scenePath} ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GaiaHeadless] FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Drives GaiaSessionManager.m_updateOperationCoroutine to completion
        /// synchronously. In batchmode Gaia's own EditorUpdate tick rate is
        /// too slow / irregular to progress the coroutine, so we MoveNext()
        /// ourselves. Matches Gaia's internal EditorUpdate behavior at
        /// GaiaSessionManager.cs:908–914 minus the 100-tick debounce.
        /// </summary>
        private static void DriveGaiaCoroutineToCompletion()
        {
            var sm = GaiaSessionManager.GetSessionManager(
                pickupExistingTerrain: false, createSession: false);
            if (sm == null)
                throw new Exception("No GaiaSessionManager in scene after CreateOrUpdateWorld.");

            var maxSteps = 10_000_000;   // safety limit; a world takes ~1e5 ticks
            int steps = 0;
            int lastLogPct = -1;
            while (sm.m_updateOperationCoroutine != null)
            {
                steps++;
                if ((DateTime.UtcNow - _startedAt).TotalSeconds > TimeoutSeconds)
                    throw new Exception($"TIMEOUT after {TimeoutSeconds}s.");

                try
                {
                    if (!sm.m_updateOperationCoroutine.MoveNext())
                    {
                        // Coroutine finished.
                        sm.m_updateOperationCoroutine = null;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Gaia coroutine threw at step {steps}: {ex.Message}", ex);
                }

                // Progress log every ~1% of max to show liveness.
                int pct = (int)(100L * steps / maxSteps);
                if (pct != lastLogPct && pct % 1 == 0 && steps % 50_000 == 0)
                {
                    lastLogPct = pct;
                    Log($"  ... driving coroutine step {steps:N0} (busyFlag={sm.m_worldCreationRunning})");
                }

                if (steps > maxSteps)
                    throw new Exception($"Coroutine exceeded {maxSteps:N0} MoveNext() calls without completing.");
            }

            var elapsed = (DateTime.UtcNow - _startedAt).TotalSeconds;
            Log($"  coroutine complete in {elapsed:F1}s after {steps:N0} MoveNext calls.");
        }

        // ── Helpers (duplicated intentionally so this script has no runtime
        //    dependency on the interactive GaiaFixup menu) ─────────────────

        private static WorldCreationSettings BuildSettings(BiomePreset biome, GaiaWorldBuilder.WorldSize size)
        {
            var s = ScriptableObject.CreateInstance<WorldCreationSettings>();
            s.m_qualityPreset = GaiaConstants.EnvironmentTarget.Desktop;
            s.m_seaLevel = 50;
            s.m_autoSpawnBiome = true;
            s.m_centerOffset = Vector2.zero;
            s.m_dateTimeString = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            s.m_spawnerPresetList = biome.m_spawnerPresetList;

            switch (size)
            {
                case GaiaWorldBuilder.WorldSize.Small:
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Medium;
                    s.m_xTiles = 1; s.m_zTiles = 1;
                    s.m_tileSize = 1024; s.m_tileHeight = 600;
                    s.m_createInScene = false; s.m_autoUnloadScenes = false;
                    s.m_applyFloatingPointFix = false;
                    break;
                case GaiaWorldBuilder.WorldSize.Medium:
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 2; s.m_zTiles = 2;
                    s.m_tileSize = 1024; s.m_tileHeight = 800;
                    s.m_createInScene = true; s.m_autoUnloadScenes = true;
                    s.m_applyFloatingPointFix = true;
                    break;
                case GaiaWorldBuilder.WorldSize.Large:
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 3; s.m_zTiles = 3;
                    s.m_tileSize = 1024; s.m_tileHeight = 1000;
                    s.m_createInScene = true; s.m_autoUnloadScenes = true;
                    s.m_applyFloatingPointFix = true; s.m_addLoadingScreen = true;
                    break;
            }
            return s;
        }

        private static void CleanBrokenTerrains()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var toDestroy = new List<GameObject>();
            foreach (var t in terrains)
                if (t != null && t.terrainData == null) toDestroy.Add(t.gameObject);
            foreach (var go in toDestroy)
                UnityEngine.Object.DestroyImmediate(go);
            if (toDestroy.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Log($"  removed {toDestroy.Count} broken Terrain(s).");
            }
        }

        private static void RunBuiltInToUrpConverter()
        {
#if UNITY_2022_2_OR_NEWER
            try
            {
                var ids = new List<ConverterId>
                {
                    ConverterId.Material,
                    ConverterId.ReadonlyMaterial,
                };
                Converters.RunInBatchMode(
                    ConverterContainerId.BuiltInToURP,
                    ids,
                    ConverterFilter.Inclusive);
                Log("  Built-In → URP converter finished.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GaiaHeadless] URP converter threw (continuing): {ex.Message}");
            }
#endif
        }

        private static void RunNatureManufactureMatFixer()
        {
            try
            {
                ForeverEngine.Editor.NatureManufactureMatFixer.Run();
                Log("  NatureManufactureMatFixer finished.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GaiaHeadless] MatFixer threw (continuing): {ex.Message}");
            }
        }

        /// <summary>
        /// Instantiates the full biome (BiomeController + every Spawner child)
        /// and triggers each active spawner to paint its prefabs onto the
        /// terrain. Each Spawn.Spawn() call kicks off its own coroutine which
        /// we drive to completion before moving to the next.
        /// </summary>
        private static void InstantiateAndSpawnBiome(BiomePreset biome)
        {
            // Build the biome GO tree (<Biome> Biome + child Spawner GameObjects).
            var biomeController = biome.CreateBiome(autoAssignPrototypes: true);
            if (biomeController == null)
                throw new Exception("BiomePreset.CreateBiome returned null.");
            Log($"  instantiated biome '{biomeController.name}' with {biomeController.m_autoSpawners?.Count ?? 0} auto-spawners.");

            if (biomeController.m_autoSpawners == null || biomeController.m_autoSpawners.Count == 0)
            {
                Log("  (no auto-spawners on this biome — skipping spawn step.)");
                return;
            }

            int ok = 0, skipped = 0, fail = 0;
            for (int i = 0; i < biomeController.m_autoSpawners.Count; i++)
            {
                var auto = biomeController.m_autoSpawners[i];
                if (auto == null || auto.spawner == null) { skipped++; continue; }
                if (!auto.isActive) { skipped++; continue; }

                var name = auto.spawner.name;
                Log($"    [{i + 1}/{biomeController.m_autoSpawners.Count}] spawning '{name}'...");
                try
                {
                    auto.spawner.Spawn(allTerrains: true);
                    DriveGaiaCoroutineToCompletion();
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Debug.LogWarning($"[GaiaHeadless]    '{name}' failed: {ex.Message}");
                }
            }
            Log($"  biome spawn complete. ok={ok} skipped={skipped} fail={fail}");
        }

        private static void Log(string msg) => Debug.Log($"[GaiaHeadless] {msg}");

        /// <summary>
        /// Batchmode has no SceneView by default, and Gaia's ExecuteCreateWorld
        /// dereferences <c>SceneView.lastActiveSceneView.camera</c>. Create a
        /// hidden one so the NRE doesn't kill the coroutine.
        /// </summary>
        private static void EnsureSceneViewExists()
        {
            if (SceneView.lastActiveSceneView != null) return;

            // GetWindow constructs a SceneView without showing it in batchmode.
            // The window object is enough to make `lastActiveSceneView` non-null.
            try
            {
                var sv = EditorWindow.GetWindow<SceneView>(utility: false, title: "Scene", focus: false);
                if (sv != null)
                {
                    Log("  spawned hidden SceneView for batchmode coroutine.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GaiaHeadless] Could not spawn SceneView: {ex.Message}");
            }
        }
    }
}
#endif
