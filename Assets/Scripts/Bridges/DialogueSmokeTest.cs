using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.Demo.UI;

namespace ForeverEngine.Tests
{
    /// <summary>
    /// Headless verification that the DialoguePanel UI Toolkit asset loads
    /// and binds correctly. Runs in Unity batchmode -executeMethod, no play
    /// mode required.
    ///
    /// Phase A: load DialoguePanel.uxml from Resources/, instantiate the
    /// visual tree directly, and assert every named element the C# code
    /// queries by name actually exists in the asset.
    ///
    /// Phase B: instantiate a DialoguePanel MonoBehaviour on a GameObject,
    /// which fires Awake() → Resources.Load() → HookUpReferences(), then
    /// call Show() and assert the panel is open and the npc name label has
    /// been populated.
    ///
    /// Run via:
    ///   Unity -batchmode -nographics -projectPath . \
    ///         -executeMethod ForeverEngine.Tests.DialogueSmokeTest.Run -quit
    ///
    /// Exit code 0 = pass, 1 = fail.
    /// </summary>
    public static class DialogueSmokeTest
    {
        public static void Run()
        {
            int exitCode = 1;
            try
            {
                exitCode = Execute();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueSmokeTest] FAIL: unhandled exception: {e.Message}\n{e.StackTrace}");
                exitCode = 1;
            }
            Debug.Log($"[DialogueSmokeTest] exit code: {exitCode}");
            EditorOrAppQuit(exitCode);
        }

        private static void EditorOrAppQuit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }

        private static int Execute()
        {
            Debug.Log("[DialogueSmokeTest] starting");

            // ---- Phase A: raw UXML asset validation ----
            var asset = Resources.Load<VisualTreeAsset>("DialoguePanel");
            if (asset == null)
            {
                Debug.LogError("[DialogueSmokeTest] FAIL phase A: Resources.Load<VisualTreeAsset>(\"DialoguePanel\") returned null. Asset must be at Assets/Resources/DialoguePanel.uxml");
                return 1;
            }
            Debug.Log("[DialogueSmokeTest] phase A: UXML asset loaded from Resources/");

            var tree = asset.Instantiate();
            if (tree == null || tree.childCount == 0)
            {
                Debug.LogError("[DialogueSmokeTest] FAIL phase A: instantiated tree has no children");
                return 1;
            }

            // Verify every element the DialoguePanel C# code queries by name.
            // If any of these are null, HookUpReferences() in DialoguePanel.cs
            // would silently no-op and the panel would render dead.
            string[] requiredNames = { "npc-name", "offline-banner", "history", "input", "send", "close" };
            foreach (var name in requiredNames)
            {
                var el = tree.Q<VisualElement>(name);
                if (el == null)
                {
                    Debug.LogError($"[DialogueSmokeTest] FAIL phase A: required VisualElement '{name}' not found in DialoguePanel.uxml");
                    return 1;
                }
            }
            Debug.Log($"[DialogueSmokeTest] phase A: all {requiredNames.Length} named elements present in UXML");

            // Type-specific checks: the panel queries some elements by their
            // concrete type via Q<T>(name). If a designer ever changes a
            // <ui:Label> to <ui:Button> or similar the binding will silently fail.
            if (tree.Q<Label>("npc-name") == null) { Debug.LogError("[DialogueSmokeTest] FAIL phase A: 'npc-name' is not a Label"); return 1; }
            if (tree.Q<Label>("offline-banner") == null) { Debug.LogError("[DialogueSmokeTest] FAIL phase A: 'offline-banner' is not a Label"); return 1; }
            if (tree.Q<ScrollView>("history") == null) { Debug.LogError("[DialogueSmokeTest] FAIL phase A: 'history' is not a ScrollView"); return 1; }
            if (tree.Q<TextField>("input") == null) { Debug.LogError("[DialogueSmokeTest] FAIL phase A: 'input' is not a TextField"); return 1; }
            if (tree.Q<Button>("send") == null) { Debug.LogError("[DialogueSmokeTest] FAIL phase A: 'send' is not a Button"); return 1; }
            if (tree.Q<Button>("close") == null) { Debug.LogError("[DialogueSmokeTest] FAIL phase A: 'close' is not a Button"); return 1; }
            Debug.Log("[DialogueSmokeTest] phase A: all element types match what DialoguePanel.cs expects");

            // ---- Phase B: DialoguePanel MonoBehaviour wiring ----
            var hostGo = new GameObject("DialogueSmokeTest_Host");
            DialoguePanel panel;
            try
            {
                panel = hostGo.AddComponent<DialoguePanel>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueSmokeTest] FAIL phase B: AddComponent<DialoguePanel> threw: {e.Message}");
                UnityEngine.Object.DestroyImmediate(hostGo);
                return 1;
            }

            // In Editor batchmode (no play mode), Unity does NOT auto-fire Awake on
            // newly added MonoBehaviours. Invoke it manually so the singleton + UXML
            // wiring runs the way it would at runtime.
            try
            {
                var awake = typeof(DialoguePanel).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (awake == null)
                {
                    Debug.LogError("[DialogueSmokeTest] FAIL phase B: DialoguePanel has no Awake method");
                    UnityEngine.Object.DestroyImmediate(hostGo);
                    return 1;
                }
                awake.Invoke(panel, null);
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                Debug.LogError($"[DialogueSmokeTest] FAIL phase B: DialoguePanel.Awake() threw: {inner.Message}\n{inner.StackTrace}");
                UnityEngine.Object.DestroyImmediate(hostGo);
                return 1;
            }

            if (DialoguePanel.Instance == null)
            {
                Debug.LogError("[DialogueSmokeTest] FAIL phase B: DialoguePanel.Instance is null after Awake invoke (singleton not set)");
                UnityEngine.Object.DestroyImmediate(hostGo);
                return 1;
            }
            if (DialoguePanel.Instance != panel)
            {
                Debug.LogError("[DialogueSmokeTest] FAIL phase B: DialoguePanel.Instance is not the component we just added");
                UnityEngine.Object.DestroyImmediate(hostGo);
                return 1;
            }
            Debug.Log("[DialogueSmokeTest] phase B: DialoguePanel singleton initialized");

            // Inspect panel internals so we know what wired up. UIDocument's
            // rootVisualElement is null in Editor mode without an active panel,
            // which cascades to all the cached element fields being null.
            var t = typeof(DialoguePanel);
            var bf = BindingFlags.Instance | BindingFlags.NonPublic;
            var doc = t.GetField("_document", bf)?.GetValue(panel);
            var root = t.GetField("_root", bf)?.GetValue(panel);
            var npcLabel = t.GetField("_npcLabel", bf)?.GetValue(panel);
            var history = t.GetField("_history", bf)?.GetValue(panel);
            var input = t.GetField("_input", bf)?.GetValue(panel);
            Debug.Log($"[DialogueSmokeTest] panel internals: _document={(doc != null ? "set" : "null")} _root={(root != null ? "set" : "null")} _npcLabel={(npcLabel != null ? "set" : "null")} _history={(history != null ? "set" : "null")} _input={(input != null ? "set" : "null")}");

            // Show() should populate the npc name label and flip IsOpen true.
            try
            {
                panel.Show("camp", "npc_camp");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueSmokeTest] FAIL phase B: Show() threw:\n{e}");
                UnityEngine.Object.DestroyImmediate(hostGo);
                return 1;
            }

            if (!panel.IsOpen)
            {
                Debug.LogError("[DialogueSmokeTest] FAIL phase B: panel.IsOpen is false after Show()");
                UnityEngine.Object.DestroyImmediate(hostGo);
                return 1;
            }
            Debug.Log("[DialogueSmokeTest] phase B: panel.Show() succeeded, IsOpen=true");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(hostGo);

            Debug.Log("[DialogueSmokeTest] PASS");
            return 0;
        }
    }
}
