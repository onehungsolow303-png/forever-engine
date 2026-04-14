#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Builds a standalone Windows player. Configures scenes automatically.
    /// Usage: Unity -batchmode -executeMethod ForeverEngine.Editor.StandaloneBuild.Build -quit
    /// </summary>
    public static class StandaloneBuild
    {
        [MenuItem("Forever Engine/Build Standalone (Windows)")]
        public static void Build()
        {
            // Configure scenes
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Overworld.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Overworld3D.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/BattleMap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/DungeonExploration.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true),
            };

            string buildPath = "Builds/ShatteredKingdom/ShatteredKingdom.exe";

            var options = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/MainMenu.unity",
                    "Assets/Scenes/Overworld.unity",
                    "Assets/Scenes/Overworld3D.unity",
                    "Assets/Scenes/BattleMap.unity",
                    "Assets/Scenes/DungeonExploration.unity",
                    "Assets/Scenes/Game.unity",
                },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[StandaloneBuild] Building to {buildPath}...");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[StandaloneBuild] Build succeeded! Size: {report.summary.totalSize / (1024 * 1024)} MB");
                Debug.Log($"[StandaloneBuild] Output: {buildPath}");
            }
            else
            {
                Debug.LogError($"[StandaloneBuild] Build failed: {report.summary.result}");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Warning)
                            Debug.LogError($"  {msg.content}");
                    }
                }
            }
        }
    }
}
#endif
