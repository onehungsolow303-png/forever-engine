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

                var settings = BuildSettings(biome, size);
                if (!GaiaSessionManager.CreateOrUpdateWorld(settings, executeNow: true, isUpdate: false))
                    throw new Exception("GaiaSessionManager.CreateOrUpdateWorld returned false");

                Log("  world-creation coroutine started. Polling m_worldCreationRunning...");
                // The coroutine needs editor update ticks to progress. Hook the
                // update loop and let Unity run until it flips the flag back to
                // false. Do NOT call Exit here — Unity needs to stay alive.
                EditorApplication.update += PollCompletion;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GaiaHeadless] FAIL during kickoff: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void PollCompletion()
        {
            var sm = GaiaSessionManager.GetSessionManager(pickUp: false, createIfMissing: false);
            var elapsed = (DateTime.UtcNow - _startedAt).TotalSeconds;

            // Initial frames: session manager may not exist yet / may not have
            // started the coroutine. Give it up to 5 seconds before we even
            // consider "not running" to mean "done".
            bool flagBusy = sm != null && sm.m_worldCreationRunning;
            bool earlyGrace = elapsed < 5.0;

            if (flagBusy || earlyGrace)
            {
                if (elapsed > TimeoutSeconds)
                {
                    Debug.LogError($"[GaiaHeadless] TIMEOUT after {TimeoutSeconds}s. Aborting.");
                    EditorApplication.update -= PollCompletion;
                    EditorApplication.Exit(2);
                }
                return;
            }

            // Creation is done — unhook and do post-processing.
            EditorApplication.update -= PollCompletion;
            try
            {
                Log($"  world creation finished in {elapsed:F1}s. Running post-processing.");
                Log("=== Step 4/5: post-processing (URP convert + matfixer + respawn) ===");
                CleanBrokenTerrains();
                RunBuiltInToUrpConverter();
                RunNatureManufactureMatFixer();
                RespawnAll();

                Log("=== Step 5/5: save scene ===");
                EditorSceneManager.SaveOpenScenes();

                Log($"=== DONE in {(DateTime.UtcNow - _startedAt).TotalSeconds:F1}s. Scene: {_scenePath} ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GaiaHeadless] FAIL during post-processing: {ex}");
                EditorApplication.Exit(1);
            }
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

        private static void RespawnAll()
        {
            var spawners = UnityEngine.Object.FindObjectsByType<Spawner>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int ok = 0, fail = 0;
            foreach (var s in spawners)
            {
                if (s == null) continue;
                try { s.Spawn(allTerrains: true); ok++; }
                catch (Exception ex) { fail++; Debug.LogWarning($"[GaiaHeadless] respawn {s.name}: {ex.Message}"); }
            }
            Log($"  respawned {ok} spawner(s), {fail} failure(s).");
        }

        private static void Log(string msg) => Debug.Log($"[GaiaHeadless] {msg}");
    }
}
#endif
