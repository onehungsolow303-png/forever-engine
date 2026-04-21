using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Editor utility to create the 3D test scene that proves the URP pipeline
    /// works with the purchased asset packs.
    ///
    /// Menu: Forever Engine / Create 3D Test Scene
    ///
    /// The scene contains:
    ///   - Ground plane with URP/Lit material
    ///   - Multistory Dungeons 2 Base_01 prefab (fallback: placeholder cubes)
    ///   - Perspective camera with PerspectiveCameraController + orbit/zoom
    ///   - Capsule player stand-in wired as camera follow target
    ///   - Directional light (sun): warm, 50° elevation, soft shadows
    ///   - Point light (torch): warm orange, range 10
    ///   - Ambient: dark purple-ish (dark dungeon feel)
    ///
    /// Saved to Assets/Scenes/3DTest.unity
    /// </summary>
    public static class SceneBuilder3D
    {
        private const string DungeonPrefabPath =
            "Assets/Multistory Dungeons 2/Prefabs/Base/Base_01.prefab";

        private const string SceneSavePath = "Assets/Scenes/3DTest.unity";

        [MenuItem("Forever Engine/Create 3D Test Scene")]
        public static void Create3DTestScene()
        {
            // ── New empty scene ─────────────────────────────────────────────
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Ground plane ────────────────────────────────────────────────
            var groundGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGO.name = "Ground";
            groundGO.transform.localScale = new Vector3(5f, 1f, 5f); // 50×50 units

            // Apply URP/Lit material so the ground catches shadows and light
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.25f, 0.22f, 0.20f); // dark stone colour
            groundGO.GetComponent<Renderer>().sharedMaterial = groundMat;

            // ── Dungeon prefab (or placeholder cubes) ───────────────────────
            var dungeonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DungeonPrefabPath);
            if (dungeonPrefab != null)
            {
                var dungeon = (GameObject)PrefabUtility.InstantiatePrefab(dungeonPrefab);
                dungeon.name = "DungeonBase_01";
                dungeon.transform.position = Vector3.zero;
                Debug.Log("[SceneBuilder3D] Dungeon prefab instantiated from " + DungeonPrefabPath);
            }
            else
            {
                Debug.LogWarning(
                    "[SceneBuilder3D] Dungeon prefab not found at: " + DungeonPrefabPath +
                    " — spawning placeholder cubes instead.");
                CreatePlaceholderCubes();
            }

            // ── Player stand-in capsule ─────────────────────────────────────
            var playerGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGO.name = "PlayerStandIn";
            playerGO.transform.position = new Vector3(0f, 1f, 0f);

            var playerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            playerMat.color = new Color(0.2f, 0.5f, 0.9f); // blue hero stand-in
            playerGO.GetComponent<Renderer>().sharedMaterial = playerMat;

            // ── Perspective camera ──────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;

            // URP camera data — ensures the camera renders with the URP pipeline
            camGO.AddComponent<UniversalAdditionalCameraData>();

            // Orbit controller — wired with the player capsule as follow target
            var cameraController =
                camGO.AddComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();

            // Wire FollowTarget via SerializedObject so the field is saved in the scene
            var camSO = new SerializedObject(cameraController);
            camSO.FindProperty("FollowTarget").objectReferenceValue = playerGO.transform;
            camSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Directional light (sun) ─────────────────────────────────────
            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            // Warm late-afternoon colour
            sun.color = new Color(1.0f, 0.92f, 0.75f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            // 50° elevation, 45° horizontal
            sunGO.transform.rotation = Quaternion.Euler(50f, 45f, 0f);

            // Attach URP additional light data for shadow settings
            var sunUrpData = sunGO.AddComponent<UniversalAdditionalLightData>();
            sunUrpData.usePipelineSettings = true;

            // ── Point light (torch) ─────────────────────────────────────────
            var torchGO = new GameObject("Torch");
            var torch = torchGO.AddComponent<Light>();
            torch.type = LightType.Point;
            // Warm orange flame colour
            torch.color = new Color(1.0f, 0.55f, 0.1f);
            torch.intensity = 3f;
            torch.range = 10f;
            torch.shadows = LightShadows.Soft;
            // Position near the dungeon entrance, slightly elevated
            torchGO.transform.position = new Vector3(3f, 2.5f, 3f);

            var torchUrpData = torchGO.AddComponent<UniversalAdditionalLightData>();
            torchUrpData.usePipelineSettings = true;

            // ── Ambient light — dark purple-ish dungeon atmosphere ───────────
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.05f, 0.12f);

            // ── Save scene ──────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, SceneSavePath);
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder3D] 3D Test scene saved to " + SceneSavePath);
            Debug.Log("[SceneBuilder3D] Open the scene and press Play to verify the URP pipeline.");
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a simple arrangement of cubes to stand in for the dungeon
        /// prefab when the Multistory Dungeons 2 pack isn't present.
        /// </summary>
        private static void CreatePlaceholderCubes()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.45f, 0.4f, 0.35f); // stone-ish grey-brown

            // Floor of the room
            SpawnCube("Placeholder_Floor", mat, Vector3.zero,
                new Vector3(10f, 0.2f, 10f));

            // Four walls
            SpawnCube("Placeholder_WallN", mat, new Vector3(0f, 1.5f, 5f),
                new Vector3(10f, 3f, 0.3f));
            SpawnCube("Placeholder_WallS", mat, new Vector3(0f, 1.5f, -5f),
                new Vector3(10f, 3f, 0.3f));
            SpawnCube("Placeholder_WallE", mat, new Vector3(5f, 1.5f, 0f),
                new Vector3(0.3f, 3f, 10f));
            SpawnCube("Placeholder_WallW", mat, new Vector3(-5f, 1.5f, 0f),
                new Vector3(0.3f, 3f, 10f));
        }

        private static void SpawnCube(string name, Material mat,
            Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

    }
}
