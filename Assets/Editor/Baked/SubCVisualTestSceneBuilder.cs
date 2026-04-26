#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.Procedural;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Builds the Sub C visual-test scene: VoxelWorldManager configured to
    /// retain all baked tiles on Start, a fly camera at 200m elevation looking
    /// down at the 2×2-tile patch, a directional light, and a sky.
    /// Saves to Assets/Scenes/SubCVisualTest.unity. Doesn't build standalone
    /// — that's StandaloneBuild.Build.
    ///
    ///   Unity.exe -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Procedural.Editor.SubCVisualTestSceneBuilder.Run \
    ///     -quit -logFile -
    /// </summary>
    public static class SubCVisualTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SubCVisualTest.unity";

        [MenuItem("Forever Engine/Bake/Build Sub C Visual Test Scene")]
        public static void BuildMenu() => BuildOrThrow();

        public static void Run()
        {
            try { BuildOrThrow(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError($"[SubCScene] FAIL: {e}"); EditorApplication.Exit(1); }
        }

        public static void BuildOrThrow()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var vwmGo = new GameObject("VoxelWorld");
            var vwm = vwmGo.AddComponent<VoxelWorldManager>();
            vwm.RenderBakedTiles = true;
            // Exercise BakedRegionTracker (gap 4): tracker auto-attaches in Start
            // and retains/releases tiles by camera distance. 3000 m radius is
            // generous enough that all 4 tiles in this 2x2 patch stay loaded
            // from any reasonable cam position — verifies "no regression vs
            // load-all" while routing through the production codepath.
            vwm.LoadAllBakedTilesOnStart = false;
            vwm.BakedTileRetainRadius = 3000f;

            var camGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            if (camGo.GetComponent<Camera>() == null) camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            // Cam high above the world looking down at the center so any
            // upright trees show as silhouettes / dots even from altitude.
            // Y=900 m guarantees we're above the 422 m peak on tile (1,0)
            // and not buried inside terrain. Use WASD+RMB to fly closer.
            camGo.transform.position = new Vector3(0f, 900f, -1400f);
            camGo.transform.LookAt(new Vector3(0f, 280f, 0f));
            var cam = camGo.GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = 70f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 8192f);
            }
            if (camGo.GetComponent<SubCVisualTestFlyCam>() == null)
                camGo.AddComponent<SubCVisualTestFlyCam>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes;
            bool exists = false;
            foreach (var s in scenes) if (s.path == ScenePath) { exists = true; break; }
            if (!exists)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            else
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                list.RemoveAll(s => s.path == ScenePath);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log($"[SubCScene] saved {ScenePath} and pinned as scene 0 in Build Settings");
        }
    }
}
#endif
