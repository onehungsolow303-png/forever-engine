#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Builds a standalone Windows player. Configures scenes automatically.
    /// Usage: Unity -batchmode -executeMethod ForeverEngine.Editor.StandaloneBuild.Build -quit
    ///
    /// Force-clean: deletes Builds/ShatteredKingdom/ before building and asserts
    /// the produced exe timestamp is within the last 60 seconds. Unity's
    /// incremental-build can silently no-op when it thinks nothing changed,
    /// leaving a stale exe from days prior while appearing to succeed (this
    /// actually shipped broken worlds on 2026-04-23). Failing loudly here
    /// turns silent no-ops into actionable errors.
    /// </summary>
    public static class StandaloneBuild
    {
        public const string BuildDir = "Builds/ShatteredKingdom";
        public const string BuildExePath = BuildDir + "/ShatteredKingdom.exe";

        /// <summary>
        /// How fresh the produced exe must be. Budget generously — a large
        /// project build can run minutes. We only care about catching silent
        /// no-ops which return in seconds.
        /// </summary>
        private const double FreshnessToleranceSeconds = 900.0;

        [MenuItem("Forever Engine/Build Standalone (Windows)")]
        public static void Build()
        {
            ForceCleanBuildDir();

            // S (server-streamed runtime): MainMenu → ClientBoot → World →
            // WorldBootstrap reads chunks streamed from ForeverEngine.Server.
            // GaiaWorld_*.unity is kept in the build settings (last index) so
            // the Gaia-built tile assets / TerrainData are still shipped, but
            // the scene flow doesn't open it directly — the runtime gets its
            // world from the server's baked-chunk stream.
            var scenePaths = new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/World.unity",
                "Assets/Scenes/BattleMap.unity",
                "Assets/Scenes/DungeonExploration.unity",
                "Assets/Scenes/GaiaWorld_Coniferous_Forest_Medium.unity",
            };
            EditorBuildSettings.scenes = Array.ConvertAll(
                scenePaths, p => new EditorBuildSettingsScene(p, true));

            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = BuildExePath,
                target = BuildTarget.StandaloneWindows64,
                // CleanBuildCache ensures Unity doesn't shortcut the player build
                // step. Paired with ForceCleanBuildDir, this closes the two common
                // silent-no-op paths we've seen.
                options = BuildOptions.CleanBuildCache,
            };

            Debug.Log($"[StandaloneBuild] Building to {BuildExePath}...");
            var buildStartUtc = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
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
                throw new Exception($"[StandaloneBuild] Build failed: {report.summary.result}");
            }

            Debug.Log($"[StandaloneBuild] Build succeeded! Size: {report.summary.totalSize / (1024 * 1024)} MB");
            Debug.Log($"[StandaloneBuild] Output: {BuildExePath}");

            AssertExeFreshness(buildStartUtc);
        }

        private static void ForceCleanBuildDir()
        {
            if (!Directory.Exists(BuildDir))
            {
                Debug.Log($"[StandaloneBuild] {BuildDir} does not exist — nothing to clean.");
                return;
            }
            Debug.Log($"[StandaloneBuild] Force-cleaning {BuildDir}/ before build.");
            try
            {
                Directory.Delete(BuildDir, recursive: true);
            }
            catch (IOException e)
            {
                // Common cause: a running client instance holds the exe open.
                throw new Exception(
                    $"[StandaloneBuild] Could not delete {BuildDir}. " +
                    $"Is ShatteredKingdom.exe still running? ({e.Message})",
                    e);
            }
        }

        private static void AssertExeFreshness(DateTime buildStartUtc)
        {
            if (!File.Exists(BuildExePath))
            {
                throw new Exception(
                    $"[StandaloneBuild] Build reported success but {BuildExePath} does not exist.");
            }

            var writeTimeUtc = File.GetLastWriteTimeUtc(BuildExePath);
            var age = DateTime.UtcNow - writeTimeUtc;

            Debug.Log($"[StandaloneBuild] exe write time: {writeTimeUtc:o} (age {age.TotalSeconds:F1}s)");

            if (writeTimeUtc < buildStartUtc)
            {
                throw new Exception(
                    $"[StandaloneBuild] {BuildExePath} last-write time ({writeTimeUtc:o}) predates " +
                    $"build start ({buildStartUtc:o}). Unity silently skipped the build — " +
                    $"you're about to ship a stale exe.");
            }

            if (age.TotalSeconds > FreshnessToleranceSeconds)
            {
                throw new Exception(
                    $"[StandaloneBuild] {BuildExePath} is {age.TotalSeconds:F0}s old, exceeding the " +
                    $"{FreshnessToleranceSeconds}s freshness tolerance. Likely a silent no-op build.");
            }
        }
    }
}
#endif
