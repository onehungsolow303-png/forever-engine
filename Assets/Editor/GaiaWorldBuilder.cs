#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Gaia;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Editor.Gaia
{
    /// <summary>
    /// One-click scripted Gaia Pro world creation. Replaces the click-through
    /// Gaia Manager flow with a single static method that:
    ///   1. Loads a BiomePreset ScriptableObject (Coniferous Forest / Alpine
    ///      Meadow / Giant Forest — the three shipped with Gaia Pro Assets and
    ///      Biomes).
    ///   2. Configures a WorldCreationSettings with tile size/count, streaming
    ///      toggles, floating-point fix, sea level, and the biome's spawner list.
    ///   3. Calls GaiaAPI.CreateGaiaWorld, which internally:
    ///        - Generates terrain tiles (multi-scene if m_createInScene=true)
    ///        - Auto-spawns the biome (trees, rocks, grass, details)
    ///        - Registers terrain scenes in build settings
    ///        - Unloads distant scenes if m_autoUnloadScenes=true
    ///        - Wires up Gaia's TerrainLoaderManager for runtime streaming
    ///
    /// Streaming-ready defaults: anything 3×3 tiles or larger enables
    /// m_createInScene + m_autoUnloadScenes + floating-point fix automatically,
    /// matching what the Gaia Manager does when the user bumps past the 3×3
    /// threshold in the UI.
    ///
    /// NOTE: CreateGaiaWorld runs on a coroutine and returns before the world
    /// is fully populated. Watch the Gaia progress bar in the editor; when it
    /// disappears the world is done.
    /// </summary>
    public static class GaiaWorldBuilder
    {
        private const string BiomesDir =
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Biomes";

        // ── One-click menu: build a Coniferous Forest at three scales. ─────

        [MenuItem("Forever Engine/Gaia/Build World/Coniferous Forest — Small (1×1, 1km)")]
        public static void BuildConiferousSmall() =>
            BuildWorld("Coniferous Forest", WorldSize.Small);

        [MenuItem("Forever Engine/Gaia/Build World/Coniferous Forest — Medium (2×2, 2km, streamed)")]
        public static void BuildConiferousMedium() =>
            BuildWorld("Coniferous Forest", WorldSize.Medium);

        [MenuItem("Forever Engine/Gaia/Build World/Coniferous Forest — Large (3×3, 3km, streamed)")]
        public static void BuildConiferousLarge() =>
            BuildWorld("Coniferous Forest", WorldSize.Large);

        [MenuItem("Forever Engine/Gaia/Build World/Alpine Meadow — Medium (2×2, 2km, streamed)")]
        public static void BuildAlpineMedium() =>
            BuildWorld("Alpine Meadow", WorldSize.Medium);

        [MenuItem("Forever Engine/Gaia/Build World/Giant Forest — Medium (2×2, 2km, streamed)")]
        public static void BuildGiantMedium() =>
            BuildWorld("Giant Forest", WorldSize.Medium);

        // ── Implementation ──────────────────────────────────────────────────

        public enum WorldSize { Small, Medium, Large, Huge }

        /// <summary>
        /// Build a new Gaia world with the given biome name and size preset.
        /// Opens an empty scene first (to avoid polluting the user's current
        /// scene), saves it as Assets/Scenes/GaiaWorld.unity, then kicks off
        /// the Gaia world-creation coroutine.
        /// </summary>
        public static void BuildWorld(string biomeName, WorldSize size)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[GaiaWorldBuilder] Exit play mode before building a Gaia world.");
                return;
            }

            // 1) Load the shipped biome preset.
            var biomePath = $"{BiomesDir}/{biomeName}.asset";
            var biome = AssetDatabase.LoadAssetAtPath<BiomePreset>(biomePath);
            if (biome == null)
            {
                Debug.LogError(
                    $"[GaiaWorldBuilder] Could not load BiomePreset at {biomePath}. " +
                    "Expected the Gaia Pro Assets and Biomes package to be installed.");
                return;
            }

            // 2) Start with a clean scene so we don't tangle Gaia objects with
            //    whatever the user had open (TestBake placeholders, etc.).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[GaiaWorldBuilder] Aborted — user cancelled scene save prompt.");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Assets/Scenes");
            var scenePath = $"Assets/Scenes/GaiaWorld_{biomeName.Replace(' ', '_')}_{size}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[GaiaWorldBuilder] Empty scene saved to {scenePath}.");

            // 3) Build the WorldCreationSettings.
            var settings = ScriptableObject.CreateInstance<WorldCreationSettings>();
            ApplySizePreset(settings, size);
            settings.m_qualityPreset = GaiaConstants.EnvironmentTarget.Desktop;
            settings.m_seaLevel = 50;
            settings.m_autoSpawnBiome = true;
            settings.m_centerOffset = Vector2.zero;
            settings.m_dateTimeString = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            settings.m_spawnerPresetList = biome.m_spawnerPresetList;

            Debug.Log(
                $"[GaiaWorldBuilder] Creating world: biome='{biomeName}' tiles={settings.m_xTiles}×{settings.m_zTiles} " +
                $"tileSize={settings.m_tileSize}m height={settings.m_tileHeight}m " +
                $"streaming={settings.m_createInScene} autoUnload={settings.m_autoUnloadScenes} " +
                $"floatFix={settings.m_applyFloatingPointFix} spawners={settings.m_spawnerPresetList?.Count ?? 0}");

            // 4) Fire the Gaia pipeline. Gaia runs a coroutine, shows its own
            //    progress bar, and writes the terrains into the open scene.
            //    When executeNow=true the session manager starts immediately.
            GaiaAPI.CreateGaiaWorld(settings, executeNow: true);

            Debug.Log(
                "[GaiaWorldBuilder] Gaia is building in the background. Watch the progress bar; " +
                "the coroutine will finish adding terrains and spawning the biome. After it " +
                "completes, run 'Forever Engine → Gaia → Fix URP + Respawn All' once to convert " +
                "any built-in-RP materials (tree leaves etc.) to URP.");
        }

        private static void ApplySizePreset(WorldCreationSettings s, WorldSize size)
        {
            switch (size)
            {
                case WorldSize.Small:
                    // 1×1 tile, 1024m. Single-scene, no streaming. Fastest preview.
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Medium;
                    s.m_xTiles = 1;
                    s.m_zTiles = 1;
                    s.m_tileSize = 1024;
                    s.m_tileHeight = 600;
                    s.m_createInScene = false;
                    s.m_autoUnloadScenes = false;
                    s.m_addLoadingScreen = false;
                    s.m_applyFloatingPointFix = false;
                    break;

                case WorldSize.Medium:
                    // 2×2 tiles, 1024m each = 2km world. Enable multi-scene so
                    // Gaia Pro's terrain streaming kicks in.
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 2;
                    s.m_zTiles = 2;
                    s.m_tileSize = 1024;
                    s.m_tileHeight = 800;
                    s.m_createInScene = true;
                    s.m_autoUnloadScenes = true;
                    s.m_addLoadingScreen = false;
                    s.m_applyFloatingPointFix = true;
                    break;

                case WorldSize.Large:
                    // 3×3 tiles, 1024m each = 3km world. This is the threshold
                    // where Gaia auto-enables streaming in its own UI.
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 3;
                    s.m_zTiles = 3;
                    s.m_tileSize = 1024;
                    s.m_tileHeight = 1000;
                    s.m_createInScene = true;
                    s.m_autoUnloadScenes = true;
                    s.m_addLoadingScreen = true;
                    s.m_applyFloatingPointFix = true;
                    break;

                case WorldSize.Huge:
                    // 4×4 tiles, 2048m each = 8km world. Big. Streaming
                    // mandatory; floating-point fix essential.
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 4;
                    s.m_zTiles = 4;
                    s.m_tileSize = 2048;
                    s.m_tileHeight = 2048;
                    s.m_createInScene = true;
                    s.m_autoUnloadScenes = true;
                    s.m_addLoadingScreen = true;
                    s.m_applyFloatingPointFix = true;
                    break;
            }
        }
    }
}
#endif
