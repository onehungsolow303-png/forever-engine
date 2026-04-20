#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForeverEngine.Editor
{
    public static class RepointMainMenu
    {
        public static void Run()
        {
            const string scenePath = "Assets/Scenes/MainMenu.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Use reflection to avoid CS0433 ambiguity between Assembly-CSharp and ForeverEngine asmdef.
            Type demoMenuType = Type.GetType("ForeverEngine.Demo.UI.DemoMainMenu, ForeverEngine");
            Type clientBootType = Type.GetType("ForeverEngine.Demo.Boot.ClientBoot, ForeverEngine");

            if (demoMenuType == null)
            {
                Debug.LogError("[RepointMainMenu] Could not resolve ForeverEngine.Demo.UI.DemoMainMenu — check asmdef.");
                return;
            }
            if (clientBootType == null)
            {
                Debug.LogError("[RepointMainMenu] Could not resolve ForeverEngine.Demo.Boot.ClientBoot — check asmdef.");
                return;
            }

            int swapped = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (Component demo in root.GetComponentsInChildren(demoMenuType, true))
                {
                    var go = demo.gameObject;
                    UnityEngine.Object.DestroyImmediate(demo);
                    if (go.GetComponent(clientBootType) == null)
                        go.AddComponent(clientBootType);
                    swapped++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RepointMainMenu] Swapped {swapped} DemoMainMenu -> ClientBoot in {scenePath}");
            if (swapped == 0)
                Debug.LogWarning("[RepointMainMenu] No DemoMainMenu components found — did the scene change?");
        }
    }
}
#endif
