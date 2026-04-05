using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Editor utility to create the initial game scene with all required GameObjects.
    /// Run from menu: Forever Engine > Create Game Scene
    /// </summary>
    public static class SceneBuilder
    {
        [MenuItem("Forever Engine/Create Game Scene")]
        public static void CreateGameScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraGO = new GameObject("Main Camera");
            var cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraGO.transform.position = new Vector3(0, 0, -10);
            cameraGO.tag = "MainCamera";
            var cameraController = cameraGO.AddComponent<ForeverEngine.MonoBehaviour.Camera.CameraController>();

            // --- Grid + Tilemap (Terrain) ---
            var gridGO = new GameObject("Grid");
            gridGO.AddComponent<Grid>();

            var terrainGO = new GameObject("Terrain");
            terrainGO.transform.SetParent(gridGO.transform);
            var tilemap = terrainGO.AddComponent<Tilemap>();
            var tilemapRenderer = terrainGO.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = 0;
            var tileRenderer = terrainGO.AddComponent<ForeverEngine.MonoBehaviour.Rendering.TileRenderer>();

            // --- Fog Overlay ---
            var fogGO = new GameObject("FogOverlay");
            fogGO.transform.position = new Vector3(0, 0, -0.5f);
            var fogSprite = fogGO.AddComponent<SpriteRenderer>();
            fogSprite.sortingOrder = 10;
            var fogRenderer = fogGO.AddComponent<ForeverEngine.MonoBehaviour.Rendering.FogRenderer>();
            // Wire fogSprite via serialized field
            var fogSO = new SerializedObject(fogRenderer);
            fogSO.FindProperty("_fogSprite").objectReferenceValue = fogSprite;
            fogSO.ApplyModifiedPropertiesWithoutUndo();

            // --- Entity Container ---
            var entitiesGO = new GameObject("Entities");
            var entityRenderer = entitiesGO.AddComponent<ForeverEngine.MonoBehaviour.Rendering.EntityRenderer>();

            // --- Creature Prefab (simple colored circle) ---
            var prefabGO = new GameObject("CreatureToken");
            var prefabSR = prefabGO.AddComponent<SpriteRenderer>();
            prefabSR.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            prefabSR.sortingOrder = 5;
            prefabGO.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // Save as prefab
            string prefabPath = "Assets/Prefabs/CreatureToken.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabGO, prefabPath);
            Object.DestroyImmediate(prefabGO);

            // Wire prefab to EntityRenderer
            var erSO = new SerializedObject(entityRenderer);
            erSO.FindProperty("_creaturePrefab").objectReferenceValue = prefab;
            erSO.ApplyModifiedPropertiesWithoutUndo();

            // --- Input Manager ---
            var inputGO = new GameObject("InputManager");
            inputGO.AddComponent<ForeverEngine.MonoBehaviour.Input.InputManager>();
            inputGO.AddComponent<ForeverEngine.MonoBehaviour.Input.PlayerMovement>();

            // --- Game Bootstrap ---
            var bootstrapGO = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGO.AddComponent<ForeverEngine.MonoBehaviour.Bootstrap.GameBootstrap>();

            // Wire references
            var bsSO = new SerializedObject(bootstrap);
            bsSO.FindProperty("CameraController").objectReferenceValue = cameraController;
            bsSO.FindProperty("TileRenderer").objectReferenceValue = tileRenderer;
            bsSO.FindProperty("FogRenderer").objectReferenceValue = fogRenderer;
            bsSO.FindProperty("EntityRenderer").objectReferenceValue = entityRenderer;

            // Set default map path (Map Generator output)
            string defaultMap = System.IO.Path.Combine(
                Application.dataPath, "Resources", "Maps", "test_dungeon", "map_data.json");
            bsSO.FindProperty("MapDataPath").stringValue = defaultMap;
            bsSO.ApplyModifiedPropertiesWithoutUndo();

            // --- Camera follows player (set target after spawn via script) ---

            // --- Save Scene ---
            string scenePath = "Assets/Scenes/Game.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] Game scene created at {scenePath}");
            Debug.Log("[SceneBuilder] Press Play to load the test dungeon!");
        }
    }
}
