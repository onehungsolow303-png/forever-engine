// Forever engine/Assets/Editor/DesertBeachCaveTestBuild.cs
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Standalone Windows player using GaiaWorld_DesertBeachCave.unity as the
    /// only/start scene. Adds FOREVER_TEST_BUILD scripting define so
    /// ConnectionManager autoboot is suppressed (no multiplayer client init).
    ///
    /// Invocation:
    ///   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Editor.DesertBeachCaveTestBuild.Build \
    ///     -quit -logFile /c/tmp/standalone-build-desertbeachcave.log
    /// </summary>
    public static class DesertBeachCaveTestBuild
    {
        [MenuItem("Forever Engine/Build Desert Beach Cave Test (Windows)")]
        public static void Build()
        {
            const string scenePath = "Assets/Scenes/GaiaWorld_DesertBeachCave.unity";
            const string outputDir = "Builds/DesertBeachCaveTest";
            const string outputExe = outputDir + "/DesertBeachCaveTest.exe";

            if (!File.Exists(scenePath))
                throw new Exception($"Start scene missing: {scenePath}. Run BuildDesertBeachCave first.");

            Directory.CreateDirectory(outputDir);

            // Add FOREVER_TEST_BUILD scripting define for the StandaloneWindows64 target,
            // restoring the prior define set after the build so subsequent normal
            // builds aren't polluted.
            var group = NamedBuildTarget.Standalone;
            string priorDefines = PlayerSettings.GetScriptingDefineSymbols(group);
            string testDefines = string.IsNullOrEmpty(priorDefines)
                ? "FOREVER_TEST_BUILD"
                : priorDefines + ";FOREVER_TEST_BUILD";
            PlayerSettings.SetScriptingDefineSymbols(group, testDefines);

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = outputExe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception($"Build failed: {report.summary.result}");

                Debug.Log($"[DesertBeachCaveTestBuild] OK -> {outputExe} ({report.summary.totalSize / (1024*1024)} MB)");
            }
            finally
            {
                // Always restore prior defines, even on build failure.
                PlayerSettings.SetScriptingDefineSymbols(group, priorDefines);
            }
        }
    }
}
#endif
