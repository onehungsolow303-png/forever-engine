#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using ForeverEngine.Demo.Dungeon;
using ForeverEngine.MonoBehaviour.Camera;

/// <summary>
/// Editor utility that generates the DungeonExploration scene and wires it
/// into the build settings. Run from: Forever Engine → Create Dungeon
/// Exploration Scene.
/// </summary>
public static class CreateDungeonScene
{
    [MenuItem("Forever Engine/Create Dungeon Exploration Scene")]
    public static void Create()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ────────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGO.AddComponent<PerspectiveCameraController>();

        // ── Directional light ─────────────────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.5f, 0.5f, 0.55f);
        light.intensity = 0.3f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── Bootstrap ─────────────────────────────────────────────────────────
        var bootstrapGO = new GameObject("DungeonBootstrap");
        bootstrapGO.AddComponent<DungeonSceneSetup>();

        // ── EventSystem ───────────────────────────────────────────────────────
        var eventGO = new GameObject("EventSystem");
        eventGO.AddComponent<EventSystem>();
        eventGO.AddComponent<StandaloneInputModule>();

        // ── Save scene ────────────────────────────────────────────────────────
        const string path = "Assets/Scenes/DungeonExploration.unity";
        EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        Debug.Log($"[CreateDungeonScene] Created {path}");

        // ── Add to build settings ─────────────────────────────────────────────
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        // Avoid duplicates
        bool alreadyPresent = false;
        foreach (var s in scenes)
        {
            if (s.path == path) { alreadyPresent = true; break; }
        }

        if (!alreadyPresent)
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[CreateDungeonScene] Added DungeonExploration to Build Settings.");
        }
        else
        {
            Debug.Log("[CreateDungeonScene] DungeonExploration already present in Build Settings.");
        }
    }
}
#endif
