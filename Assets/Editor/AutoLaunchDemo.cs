using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ForeverEngine.Editor
{
    [InitializeOnLoad]
    public static class AutoLaunchDemo
    {
        static AutoLaunchDemo()
        {
            string flagPath = System.IO.Path.Combine(Application.dataPath, "..", "tests", "launch-demo.flag");
            if (System.IO.File.Exists(flagPath))
            {
                System.IO.File.Delete(flagPath);
                EditorApplication.delayCall += () =>
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                    EditorApplication.delayCall += () =>
                    {
                        EditorApplication.isPlaying = true;
                        Debug.Log("[AutoLaunch] Demo started! MainMenu scene in Play mode.");
                    };
                };
            }
        }
    }
}
