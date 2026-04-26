#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Builds a minimal standalone with ONLY the Sub C visual-test scene as the
    /// entry point. Used to verify Sub C runtime-bake-extension rendering
    /// without the full multiplayer stack. Output goes to a separate dir
    /// (Builds/SubCVisualTest/) so it doesn't clobber the production build.
    ///
    ///   Unity.exe -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Procedural.Editor.SubCStandaloneBuild.Build \
    ///     -quit -logFile -
    ///
    /// Run SubCVisualTestSceneBuilder.Run first to (re)create the scene.
    /// </summary>
    public static class SubCStandaloneBuild
    {
        public const string BuildDir = "Builds/SubCVisualTest";
        public const string BuildExePath = BuildDir + "/SubCVisualTest.exe";
        private const string ScenePath = "Assets/Scenes/SubCVisualTest.unity";

        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new Exception($"[SubCBuild] missing {ScenePath} — run SubCVisualTestSceneBuilder.Run first");

            EnsureURPTerrainShaderIncluded();
            ForceCleanBuildDir();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache,
            };

            Debug.Log($"[SubCBuild] Building to {BuildExePath}...");
            var buildStartUtc = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Warning)
                            Debug.LogError($"  {msg.content}");
                throw new Exception($"[SubCBuild] Build failed: {report.summary.result}");
            }

            Debug.Log($"[SubCBuild] Build succeeded! Size: {report.summary.totalSize / (1024 * 1024)} MB → {BuildExePath}");
            if (!File.Exists(BuildExePath))
                throw new Exception($"[SubCBuild] reported success but {BuildExePath} does not exist");
            var writeTimeUtc = File.GetLastWriteTimeUtc(BuildExePath);
            if (writeTimeUtc < buildStartUtc)
                throw new Exception($"[SubCBuild] {BuildExePath} predates build start — silent no-op");
        }

        private static void EnsureURPTerrainShaderIncluded()
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[SubCBuild] could not locate URP terrain shader; runtime will fall back");
                return;
            }
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            UnityEngine.Object gs = null;
            foreach (var a in assets) if (a != null) { gs = a; break; }
            if (gs == null)
            {
                Debug.LogWarning("[SubCBuild] could not load GraphicsSettings.asset");
                return;
            }
            var so = new SerializedObject(gs);
            var prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null)
            {
                Debug.LogWarning("[SubCBuild] m_AlwaysIncludedShaders property not found on GraphicsSettings");
                return;
            }
            for (int i = 0; i < prop.arraySize; i++)
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            prop.arraySize++;
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SubCBuild] added '{shader.name}' to Always Included Shaders");
        }

        private static void ForceCleanBuildDir()
        {
            if (!Directory.Exists(BuildDir)) return;
            try { Directory.Delete(BuildDir, recursive: true); }
            catch (IOException e)
            {
                throw new Exception($"[SubCBuild] Could not delete {BuildDir}. Running exe? ({e.Message})", e);
            }
        }
    }
}
#endif
