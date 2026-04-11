using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using ForeverEngine.Demo.Overworld;

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

        // ── Overworld 3D Scene ──────────────────────────────────────────────

        private const string Overworld3DScenePath = "Assets/Scenes/Overworld3D.unity";

        /// <summary>
        /// Creates the Overworld3D scene, ready for the 3D overworld game loop.
        ///
        /// Scene contents:
        ///   - Perspective camera with PerspectiveCameraController + URP data
        ///   - Directional light (sun): warm white, 50° elevation, soft shadows
        ///   - Ambient: flat mode, dark blue-purple
        ///   - Linear fog: blue-grey, start 40, end 120
        ///   - Overworld3DSetup GameObject (bootstrapper, prefab map wired)
        ///   - OverworldManager GameObject (game loop)
        ///
        /// Menu: Forever Engine / Create Overworld3D Scene
        /// Saved to Assets/Scenes/Overworld3D.unity
        /// </summary>
        [MenuItem("Forever Engine/Create Overworld3D Scene")]
        public static void CreateOverworld3DScene()
        {
            // ── New empty scene ──────────────────────────────────────────────
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Perspective camera ───────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Dark blue-black sky background
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.12f);

            // URP camera data — ensures the camera renders with the URP pipeline
            camGO.AddComponent<UniversalAdditionalCameraData>();

            // Perspective orbit/zoom/follow controller
            camGO.AddComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();

            // ── Directional light (sun) ──────────────────────────────────────
            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            // Warm white outdoor sun
            sun.color = new Color(1.0f, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            // 50° elevation, -30° horizontal (north-west sun angle)
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var sunUrpData = sunGO.AddComponent<UniversalAdditionalLightData>();
            sunUrpData.usePipelineSettings = true;

            // ── Render settings ──────────────────────────────────────────────
            // Ambient: flat, dark blue-purple (overworld dusk/night feel)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.18f, 0.22f);

            // Fog: linear, blue-grey distance haze
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.4f, 0.45f, 0.5f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 120f;

            // ── Overworld3DSetup GameObject ──────────────────────────────────
            var setupGO = new GameObject("Overworld3DSetup");
            var setup = setupGO.AddComponent<Overworld3DSetup>();

            // Wire the prefab map via SerializedObject (field is [SerializeField] private)
            var prefabMap = AssetDatabase.LoadAssetAtPath<OverworldPrefabMapper>(
                "Assets/ScriptableObjects/OverworldPrefabMap.asset");
            if (prefabMap != null)
            {
                var so = new SerializedObject(setup);
                var prop = so.FindProperty("_prefabMap");
                if (prop != null)
                {
                    prop.objectReferenceValue = prefabMap;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogWarning("[SceneBuilder3D] Could not find '_prefabMap' property on Overworld3DSetup.");
                }
            }
            else
            {
                Debug.LogWarning(
                    "[SceneBuilder3D] OverworldPrefabMap.asset not found at " +
                    "Assets/ScriptableObjects/OverworldPrefabMap.asset — " +
                    "run 'Forever Engine / Create Overworld Prefab Map' first.");
            }

            // ── OverworldManager GameObject ──────────────────────────────────
            var managerGO = new GameObject("OverworldManager");
            managerGO.AddComponent<OverworldManager>();

            // ── Save scene ───────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, Overworld3DScenePath);
            AssetDatabase.Refresh();

            // ── Add to build settings if not already present ─────────────────
            AddSceneToBuildSettings(Overworld3DScenePath);

            Debug.Log("[SceneBuilder3D] Overworld3D scene saved to " + Overworld3DScenePath);
        }

        /// <summary>
        /// Adds <paramref name="scenePath"/> to EditorBuildSettings.scenes if it
        /// is not already present. Existing entries are preserved unchanged.
        /// </summary>
        private static void AddSceneToBuildSettings(string scenePath)
        {
            var existing = EditorBuildSettings.scenes;

            foreach (var s in existing)
            {
                if (s.path == scenePath)
                    return; // already present
            }

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;

            Debug.Log($"[SceneBuilder3D] Added '{scenePath}' to build settings (index {existing.Length}).");
        }

        // ── Overworld Prefab Map ─────────────────────────────────────────────

        [MenuItem("Forever Engine/Create Overworld Prefab Map")]
        public static void CreateOverworldPrefabMap()
        {
            var mapper = ScriptableObject.CreateInstance<OverworldPrefabMapper>();
            mapper.HexWorldSize = 4f;
            mapper.ElevationScale = 2f;

            // Primary terrain prefabs (one per tile)
            mapper.ForestPrefabs = LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Beech Trees", 10);
            mapper.MountainPrefabs = LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Mountain Environment", 10);
            mapper.RuinsPrefabs = LoadPrefabsFromPath(
                "Assets/Lordenfel/Prefabs/Architecture", 10);
            mapper.PlainsPrefabs = System.Array.Empty<GameObject>();
            mapper.WaterPrefabs = System.Array.Empty<GameObject>();

            // Scatter prefabs (multiple per tile for density)
            var forestScatter = new System.Collections.Generic.List<GameObject>();
            forestScatter.AddRange(LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Bushes", 6));
            forestScatter.AddRange(LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Rocks", 6));
            forestScatter.AddRange(LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Mushrooms", 4));
            forestScatter.AddRange(LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Foliage and Grass", 6));
            mapper.ForestScatter = forestScatter.ToArray();

            var mountainScatter = new System.Collections.Generic.List<GameObject>();
            mountainScatter.AddRange(LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Mountain Environment/Rocks", 8));
            mountainScatter.AddRange(LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Mountain Environment/Bushes", 6));
            mapper.MountainScatter = mountainScatter.ToArray();

            mapper.PlainsScatter = LoadPrefabsFromPath(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Foliage and Grass", 6);

            mapper.RuinsScatter = LoadPrefabsFromPath(
                "Assets/Lordenfel/Prefabs/Props", 6);

            // Ground materials (PBR textures per biome)
            mapper.PlainsGround = LoadMaterial(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Ground/Materials/M_ground_beech_forest_Moss.mat");
            mapper.ForestGround = LoadMaterial(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Ground/Materials/M_ground_beech_forest_leaves_01.mat");
            mapper.MountainGround = LoadMaterial(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Ground/Materials/M_ground_beech_forest_rocks_01.mat");
            mapper.RuinsGround = LoadMaterial(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Ground/Materials/M_ground_beech_forest_stones_01.mat");
            mapper.WaterGround = null;

            // Player prefab
            mapper.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/NAKED_SINGULARITY/DARK_KNIGHT/PREFABS/SK_DC_Knight_full_RPG.prefab");
            if (mapper.PlayerPrefab == null)
                Debug.LogWarning("[SceneBuilder3D] Dark Knight player prefab not found");

            // Location prefabs
            mapper.TownPrefab = LoadFirstPrefab(
                "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/_Building Sets");
            mapper.CampPrefab = LoadFirstPrefab(
                "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Small Architecture/Bridge/Prefabs");
            mapper.DungeonEntrancePrefab = LoadFirstPrefab(
                "Assets/Lordenfel/Prefabs/Architecture");

            // Save
            const string soPath = "Assets/ScriptableObjects/OverworldPrefabMap.asset";
            AssetDatabase.CreateAsset(mapper, soPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[SceneBuilder3D] OverworldPrefabMap saved to {soPath}\n" +
                $"  Forest: {mapper.ForestPrefabs.Length} prefabs, {mapper.ForestScatter.Length} scatter\n" +
                $"  Mountain: {mapper.MountainPrefabs.Length} prefabs, {mapper.MountainScatter.Length} scatter\n" +
                $"  Ruins: {mapper.RuinsPrefabs.Length} prefabs, {mapper.RuinsScatter.Length} scatter\n" +
                $"  Plains scatter: {mapper.PlainsScatter.Length}\n" +
                $"  Ground mats: P={mapper.PlainsGround != null} F={mapper.ForestGround != null} M={mapper.MountainGround != null} R={mapper.RuinsGround != null}\n" +
                $"  Player: {(mapper.PlayerPrefab != null ? "Dark Knight" : "MISSING")}\n" +
                $"  Locations: Town={mapper.TownPrefab != null} Camp={mapper.CampPrefab != null} Dungeon={mapper.DungeonEntrancePrefab != null}");
        }

        private static GameObject[] LoadPrefabsFromPath(string path, int cap)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
            var results = new System.Collections.Generic.List<GameObject>(cap);
            foreach (string guid in guids)
            {
                if (results.Count >= cap) break;
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null) results.Add(prefab);
            }
            if (results.Count == 0)
                Debug.LogWarning($"[SceneBuilder3D] No prefabs found under: {path}");
            return results.ToArray();
        }

        private static Material LoadMaterial(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                Debug.LogWarning($"[SceneBuilder3D] Material not found: {path}");
            return mat;
        }

        private static GameObject LoadFirstPrefab(string path)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[SceneBuilder3D] No prefab found under: {path}");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
