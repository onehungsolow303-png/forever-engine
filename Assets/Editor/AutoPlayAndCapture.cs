using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Opens the Game scene, enters Play mode, waits for map to load,
    /// captures a screenshot, then exits. All automated — no user interaction.
    /// </summary>
    public static class AutoPlayAndCapture
    {
        private static string _screenshotPath;
        private static int _frameCount;
        private static bool _registered;

        [MenuItem("Forever Engine/Play and Capture Screenshot")]
        public static void Execute()
        {
            _screenshotPath = Path.Combine(Application.dataPath, "..", "tests", "game-screenshot.png");
            _frameCount = 0;

            // Open the game scene
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");

            // Register callback for after play mode starts
            if (!_registered)
            {
                EditorApplication.update += OnUpdate;
                _registered = true;
            }

            // Enter play mode
            EditorApplication.isPlaying = true;
            Debug.Log("[AutoCapture] Entering play mode...");
        }

        [InitializeOnLoadMethod]
        static void InitAutoRun()
        {
            // Check for auto-run flag file
            string flagPath = Path.Combine(Application.dataPath, "..", "tests", "auto-capture.flag");
            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
                EditorApplication.delayCall += () =>
                {
                    // Wait for Unity to fully load
                    EditorApplication.delayCall += Execute;
                };
            }
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            _frameCount++;

            // Wait 60 frames for everything to initialize and render
            if (_frameCount == 60)
            {
                // Capture screenshot
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                Debug.Log($"[AutoCapture] Screenshot saved to: {_screenshotPath}");
            }

            // Wait a few more frames for the file to write, then exit
            if (_frameCount == 90)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.update -= OnUpdate;
                _registered = false;
                Debug.Log("[AutoCapture] Done. Exiting play mode.");

                // If in batch mode, quit
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
        }
    }
}
