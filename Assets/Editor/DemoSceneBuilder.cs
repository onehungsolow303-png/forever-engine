using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ForeverEngine.Demo;
using ForeverEngine.Demo.UI;
using ForeverEngine.Demo.Overworld;
using ForeverEngine.Demo.Battle;
using ForeverEngine.Demo.Encounters;
using ForeverEngine.Demo.Locations;

namespace ForeverEngine.Editor
{
    public static class DemoSceneBuilder
    {
        [MenuItem("Forever Engine/Build Demo Scenes")]
        public static void BuildAll()
        {
            BuildMainMenu();
            BuildOverworld();
            BuildBattleMap();
            Debug.Log("[DemoSceneBuilder] All 3 demo scenes created!");
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
    }
}
