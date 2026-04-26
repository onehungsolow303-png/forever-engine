#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Force-reimports every .compute asset in the project. Recovery tool for the
    /// "Kernel 'CSMain' not found" failure where Unity batchmode emits a compute
    /// binary missing its kernels even though the source #pragma is intact.
    /// Equivalent to right-click → Reimport in the editor, but covers all compute
    /// shaders in one batchmode pass.
    ///
    ///   Unity.exe -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Editor.ComputeShaderForceReimport.Run \
    ///     -quit -logFile -
    /// </summary>
    public static class ComputeShaderForceReimport
    {
        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:ComputeShader");
            Debug.Log($"[ComputeShaderForceReimport] Found {guids.Length} compute shaders");

            int reimported = 0;
            int failed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    try
                    {
                        AssetDatabase.ImportAsset(path,
                            ImportAssetOptions.ForceUpdate |
                            ImportAssetOptions.ForceSynchronousImport);
                        reimported++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ComputeShaderForceReimport] FAILED {path}: {e.Message}");
                        failed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[ComputeShaderForceReimport] Done. reimported={reimported} failed={failed}");
            if (failed > 0)
                EditorApplication.Exit(1);
        }
    }
}
#endif
