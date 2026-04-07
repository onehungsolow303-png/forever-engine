using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using ForeverEngine.Demo;
using ForeverEngine.Demo.UI;
using ForeverEngine.Demo.Overworld;
using ForeverEngine.Demo.Battle;
using ForeverEngine.Demo.Encounters;
using ForeverEngine.Demo.Locations;
using ForeverEngine.MonoBehaviour.Bootstrap;
using ForeverEngine.MonoBehaviour.Rendering;
using ForeverEngine.MonoBehaviour.Input;
using ForeverEngine.MonoBehaviour.Camera;
using UnityEngine.Tilemaps;

namespace ForeverEngine.Editor
{
    public static class DemoSceneBuilder
    {
        [MenuItem("Forever Engine/Build Demo Scenes")]
        public static void BuildAll()
        {
            BuildOverworld();
            BuildBattleMap();
            BuildGame();
            BuildMainMenu(); // Last so editor opens to MainMenu
            Debug.Log("[DemoSceneBuilder] All 4 demo scenes created!");
        }

        private static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            camGO.tag = "MainCamera";

            var menuGO = new GameObject("MainMenu");
            menuGO.AddComponent<DemoMainMenu>();

            // GameManager (persistent)
            var gmGO = new GameObject("GameManager");
            gmGO.AddComponent<GameManager>();

            // AI Systems (child of GameManager — persists via DontDestroyOnLoad)
            //
            // Phase 3 pivot: AIDirector, MemoryManager, ClaudeAPIClient,
            // DemoAIIntegration archived to _archive/forever-engine-pre-pivot/.
            // Their out-of-process replacements (AssetClient, DirectorClient,
            // ServiceWatchdog) are now instantiated by GameManager.Awake itself,
            // so this builder no longer needs to wire them.
            //
            // The per-frame AI subsystems (DynamicDifficulty, PlayerProfiler,
            // SystemMonitor, PerformanceRegulator, InferenceEngine) STAY in C#
            // per the tempo-split decision Q2=C and remain wired here.
            var aiGO = new GameObject("AI_Systems");
            aiGO.transform.SetParent(gmGO.transform);
            aiGO.AddComponent<ForeverEngine.AI.Learning.DynamicDifficulty>();
            aiGO.AddComponent<ForeverEngine.AI.PlayerModeling.PlayerProfiler>();
            aiGO.AddComponent<ForeverEngine.AI.SelfHealing.SystemMonitor>();
            aiGO.AddComponent<ForeverEngine.AI.SelfHealing.PerformanceRegulator>();
            aiGO.AddComponent<ForeverEngine.AI.Inference.InferenceEngine>();
            aiGO.AddComponent<ForeverEngine.AI.Inference.InferenceScheduler>();

            // Sound Manager (persistent)
            var sfxGO = new GameObject("SoundManager");
            sfxGO.transform.SetParent(gmGO.transform);
            sfxGO.AddComponent<ForeverEngine.Demo.Audio.SoundManager>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
            Debug.Log("[DemoSceneBuilder] MainMenu scene created");
        }

        private static void BuildOverworld()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.15f, 0.1f);
            camGO.transform.position = new Vector3(0, 0, -10);
            camGO.tag = "MainCamera";

            // Overworld Manager
            var owGO = new GameObject("OverworldManager");
            owGO.AddComponent<OverworldManager>();

            // Encounter Manager
            var encGO = new GameObject("EncounterManager");
            encGO.AddComponent<EncounterManager>();

            // Location Manager
            var locGO = new GameObject("LocationManager");
            locGO.AddComponent<LocationManager>();

            // HUD
            var hudGO = new GameObject("OverworldHUD");
            hudGO.AddComponent<OverworldHUD>();

            // Victory Screen (hidden by default)
            var victoryGO = new GameObject("VictoryScreen");
            victoryGO.AddComponent<VictoryScreen>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Overworld.unity");
            Debug.Log("[DemoSceneBuilder] Overworld scene created");
        }

        private static void BuildBattleMap()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            camGO.transform.position = new Vector3(4, 4, -10);
            camGO.tag = "MainCamera";

            // Battle Manager
            var bmGO = new GameObject("BattleManager");
            bmGO.AddComponent<BattleManager>();

            // Battle HUD
            var hudGO = new GameObject("BattleHUD");
            hudGO.AddComponent<BattleHUD>();

            // Loot Screen
            var lootGO = new GameObject("LootScreen");
            lootGO.AddComponent<LootScreen>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BattleMap.unity");
            Debug.Log("[DemoSceneBuilder] BattleMap scene created");
        }

        private static void BuildGame()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera with CameraController
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            camGO.transform.position = new Vector3(0, 0, -10);
            camGO.tag = "MainCamera";
            var cameraCtrl = camGO.AddComponent<CameraController>();

            // Grid + Tilemap for TileRenderer
            var gridGO = new GameObject("Grid");
            gridGO.AddComponent<Grid>();
            var tilemapGO = new GameObject("Tilemap");
            tilemapGO.transform.SetParent(gridGO.transform);
            var tilemap = tilemapGO.AddComponent<Tilemap>();
            tilemapGO.AddComponent<TilemapRenderer>();
            var tileRenderer = tilemapGO.AddComponent<TileRenderer>();

            // Entity Renderer
            var entityGO = new GameObject("EntityRenderer");
            var entityRenderer = entityGO.AddComponent<EntityRenderer>();

            // Fog Renderer
            var fogGO = new GameObject("FogRenderer");
            var fogRenderer = fogGO.AddComponent<FogRenderer>();

            // Input
            var inputGO = new GameObject("InputManager");
            inputGO.AddComponent<InputManager>();

            // Player Movement
            var moveGO = new GameObject("PlayerMovement");
            moveGO.AddComponent<PlayerMovement>();

            // GameBootstrap — wire all references
            var bootstrapGO = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGO.AddComponent<GameBootstrap>();

            // Use SerializedObject to set private SerializeField references
            var so = new SerializedObject(bootstrap);
            so.FindProperty("CameraController").objectReferenceValue = cameraCtrl;
            so.FindProperty("TileRenderer").objectReferenceValue = tileRenderer;
            so.FindProperty("EntityRenderer").objectReferenceValue = entityRenderer;
            so.FindProperty("FogRenderer").objectReferenceValue = fogRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            Debug.Log("[DemoSceneBuilder] Game scene created");
        }

        /// <summary>
        /// Opens BattleMap scene, enters Play mode, waits for the battle to
        /// render, captures a screenshot, then exits.  Callable from batch mode.
        /// </summary>
        [MenuItem("Forever Engine/Playtest Capture")]
        public static void PlaytestCapture()
        {
            string screenshotPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "tests", "playtest-battle.png"));

            // Ensure tests directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath));

            // Open BattleMap scene
            EditorSceneManager.OpenScene("Assets/Scenes/BattleMap.unity");

            int frameCount = 0;
            bool registered = false;

            void OnUpdate()
            {
                if (!EditorApplication.isPlaying) return;
                frameCount++;

                if (frameCount == 60)
                {
                    ScreenCapture.CaptureScreenshot(screenshotPath);
                    Debug.Log($"[PlaytestCapture] Screenshot saved to: {screenshotPath}");
                }

                if (frameCount == 90)
                {
                    EditorApplication.isPlaying = false;
                    EditorApplication.update -= OnUpdate;
                    registered = false;
                    Debug.Log("[PlaytestCapture] Done. Exiting play mode.");

                    if (Application.isBatchMode)
                        EditorApplication.Exit(0);
                }
            }

            if (!registered)
            {
                EditorApplication.update += OnUpdate;
                registered = true;
            }

            EditorApplication.isPlaying = true;
            Debug.Log("[PlaytestCapture] Entering play mode on BattleMap...");
        }
    }
}
