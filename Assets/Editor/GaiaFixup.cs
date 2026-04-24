#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Gaia;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2022_2_OR_NEWER
using UnityEditor.Rendering.Universal;
#endif

namespace ForeverEngine.Editor.Gaia
{
    /// <summary>
    /// One-click fix-up for Gaia-created scenes on a URP project:
    ///   1. Run Unity's Built-In -> URP material converter (so Gaia tree leaves
    ///      stop rendering white and rocks stop rendering invisible).
    ///   2. Run NatureManufactureMatFixer for the HDRP-property-name rocks that
    ///      the standard converter misses.
    ///   3. Walk every Gaia Spawner in the open scene and invoke Spawn(true) so
    ///      biome categories that didn't populate get another pass.
    ///   4. Save the scene.
    ///
    /// Added 2026-04-24 when the Coniferous Forest biome produced white-leaf
    /// trees and zero rocks in the playtest preview.
    /// </summary>
    public static class GaiaFixup
    {
        [MenuItem("Forever Engine/Gaia/Fix URP + Respawn All")]
        public static void Run()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Gaia Fixup", "Step 1/4 — Clean broken terrains...", 0.02f);
                CleanBrokenTerrains();

                EditorUtility.DisplayProgressBar("Gaia Fixup", "Step 2/4 — Built-In to URP material convert...", 0.10f);
                RunBuiltInToUrpConverter();

                EditorUtility.DisplayProgressBar("Gaia Fixup", "Step 3/4 — NatureManufactureMatFixer...", 0.50f);
                RunNatureManufactureMatFixer();

                EditorUtility.DisplayProgressBar("Gaia Fixup", "Step 4/4 — Respawn all Gaia spawners in scene...", 0.75f);
                RespawnAll();

                EditorUtility.DisplayProgressBar("Gaia Fixup", "Saving scene...", 0.95f);
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

                Debug.Log("[GaiaFixup] DONE.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GaiaFixup] FAIL: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Finds every Terrain component in the open scene whose terrainData is null
        /// and destroys its GameObject. A Terrain with null terrainData throws
        /// NullReferenceException from Gaia's ProceduralWorldsGlobalWeather every
        /// update tick, spamming the console and blocking other diagnostics.
        ///
        /// Root cause seen 2026-04-24: the old TestBakeTerrain GameObject survived
        /// Gaia's world-creation coroutine even though its terrainData had been
        /// wiped, leaving a broken Terrain that the weather controller kept hitting.
        /// </summary>
        [MenuItem("Forever Engine/Gaia/Only: Clean Broken Terrains")]
        public static void CleanBrokenTerrains()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int killed = 0;
            var toDestroy = new List<GameObject>();
            foreach (var t in terrains)
            {
                if (t == null) continue;
                if (t.terrainData == null)
                {
                    Debug.LogWarning(
                        $"[GaiaFixup] Removing Terrain '{t.name}' — terrainData is null. " +
                        "This is what was spamming ProceduralWorldsGlobalWeather errors.");
                    toDestroy.Add(t.gameObject);
                }
            }
            foreach (var go in toDestroy)
            {
                UnityEngine.Object.DestroyImmediate(go);
                killed++;
            }
            if (killed > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    SceneManager.GetActiveScene());
            Debug.Log($"[GaiaFixup] Scanned {terrains.Length} terrain(s); removed {killed} broken.");
        }

        [MenuItem("Forever Engine/Gaia/Only: Built-In to URP Convert")]
        public static void RunBuiltInToUrpConverter()
        {
#if UNITY_2022_2_OR_NEWER
            // Converts every BuiltInRP Material asset to URP/Lit (or URP/Simple Lit)
            // in place, preserving textures. Most Gaia 2023 biome tree leaf materials
            // are still authored against the built-in pipeline; URP renders them white
            // until this runs.
            try
            {
                var converters = new List<ConverterId>
                {
                    ConverterId.Material,
                    ConverterId.ReadonlyMaterial,
                };
                Converters.RunInBatchMode(
                    ConverterContainerId.BuiltInToURP,
                    converters,
                    ConverterFilter.Inclusive);
                Debug.Log("[GaiaFixup] Built-In -> URP material converter finished.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[GaiaFixup] Built-In to URP converter threw: " + ex.Message +
                    "\n  If your URP version doesn't expose Converters.RunInBatchMode, open " +
                    "'Window > Rendering > Render Pipeline Converter' manually and run it.");
            }
#else
            Debug.LogWarning("[GaiaFixup] Built-In to URP Converter API requires Unity 2022.2+. Run from the menu: Window > Rendering > Render Pipeline Converter.");
#endif
        }

        [MenuItem("Forever Engine/Gaia/Only: Run NatureManufactureMatFixer")]
        public static void RunNatureManufactureMatFixer()
        {
            try
            {
                ForeverEngine.Editor.NatureManufactureMatFixer.Run();
                Debug.Log("[GaiaFixup] NatureManufactureMatFixer finished.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[GaiaFixup] NatureManufactureMatFixer threw: " + ex);
            }
        }

        [MenuItem("Forever Engine/Gaia/Only: Respawn All Gaia Spawners In Scene")]
        public static void RespawnAll()
        {
            var spawners = UnityEngine.Object
                .FindObjectsByType<Spawner>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(s => s != null)
                .ToArray();

            if (spawners.Length == 0)
            {
                Debug.LogWarning("[GaiaFixup] No Gaia Spawner components found in the open scene. " +
                                 "Open the scene that has your Gaia biome before running.");
                return;
            }

            Debug.Log($"[GaiaFixup] Respawning {spawners.Length} Gaia spawner(s)...");
            int ok = 0, fail = 0;
            for (int i = 0; i < spawners.Length; i++)
            {
                var s = spawners[i];
                EditorUtility.DisplayProgressBar(
                    "Gaia Fixup",
                    $"Spawner {i + 1}/{spawners.Length}: {s.name}",
                    0.70f + 0.25f * ((float)i / spawners.Length));
                try
                {
                    // allTerrains:true so the spawner paints across the whole world,
                    // not just its own local range (which defaults to ~128m).
                    s.Spawn(allTerrains: true);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Debug.LogError($"[GaiaFixup] {s.name} failed: {ex.Message}");
                }
            }
            Debug.Log($"[GaiaFixup] Respawn complete. ok={ok} fail={fail}");
        }
    }
}
#endif
