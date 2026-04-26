#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gaia;
using ForeverEngine.Procedural;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// One-click setup for the GaiaWorld_*.unity scenes:
    ///   - Ensures Main Camera exists, tagged MainCamera
    ///   - Adds AudioListener (silences the "no audio listener" warning)
    ///   - Adds SubCVisualTestFlyCam (WASD + right-click look)
    ///   - Adds Gaia FloatingPointFix (silences the 4 NREs from terrain FloatingPointFixMember)
    ///   - Positions camera above terrain center, looking down
    ///   - Saves the scene
    ///
    /// Idempotent: safe to re-run if you tweak something.
    /// </summary>
    public static class GaiaSceneFlyCamSetup
    {
        [MenuItem("Forever Engine/Gaia/Setup Fly Camera in current scene")]
        public static void SetupFlyCamera()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[GaiaSceneFlyCamSetup] Stop play mode first.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[GaiaSceneFlyCamSetup] No active scene. Open a GaiaWorld_*.unity scene first.");
                return;
            }

            // Find or create Main Camera
            var camGo = GameObject.FindGameObjectWithTag("MainCamera");
            bool created = false;
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                created = true;
                Debug.Log("[GaiaSceneFlyCamSetup] Created new Main Camera GameObject.");
            }

            // Camera component
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 4000f; // see far over the 1km world
            cam.fieldOfView = 60f;

            // AudioListener — silences "no audio listener" warning
            if (camGo.GetComponent<AudioListener>() == null)
                camGo.AddComponent<AudioListener>();

            // FlyCam controller
            if (camGo.GetComponent<SubCVisualTestFlyCam>() == null)
                camGo.AddComponent<SubCVisualTestFlyCam>();

            // Gaia FloatingPointFix — anchor for the 4 terrain FloatingPointFixMember components
            if (camGo.GetComponent<FloatingPointFix>() == null)
                camGo.AddComponent<FloatingPointFix>();

            // Position above center of the 1km world, looking forward + slightly down
            // World tiles are at (-512..512), terrain peaks ~440m, sea level ~25
            camGo.transform.position = new Vector3(0f, 200f, -400f);
            camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            // Mark dirty + save
            EditorUtility.SetDirty(camGo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[GaiaSceneFlyCamSetup] DONE. Camera {(created ? "created" : "updated")} " +
                      $"in '{scene.name}'. Position: {camGo.transform.position}, " +
                      $"Rotation: {camGo.transform.eulerAngles}. Scene saved.");
            Debug.Log("[GaiaSceneFlyCamSetup] Press Play. Right-click + drag to look. WASD to move. Shift = sprint.");
        }
    }
}
#endif
