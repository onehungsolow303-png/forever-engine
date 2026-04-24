#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Batchmode combo: NatureManufactureMatFixer.Run → BakeWithCuratedCatalog.Run →
    /// StandaloneBuild.Build. One Unity session, avoids ~60s of editor-startup overhead.
    ///
    /// Invoke: Unity.exe -batchmode -projectPath "C:/Dev/Forever engine"
    ///                   -executeMethod ForeverEngine.Procedural.Editor.FullRebakeWithMatFix.Run
    ///                   -logFile -
    ///
    /// Note: does NOT call -quit itself — BakeWithCuratedCatalog.Run calls
    /// EditorApplication.Exit(0) on success. If adjusting the chain, ensure the
    /// final step terminates the editor.
    /// </summary>
    public static class FullRebakeWithMatFix
    {
        public static void Run()
        {
            try
            {
                Debug.Log("[FullRebake] Step 1/3: NatureManufactureMatFixer.Run()");
                ForeverEngine.Editor.NatureManufactureMatFixer.Run();
                AssetDatabase.SaveAssets();

                Debug.Log("[FullRebake] Step 2/3: StandaloneBuild.Build()");
                ForeverEngine.Editor.StandaloneBuild.Build();

                Debug.Log("[FullRebake] Step 3/3: BakeWithCuratedCatalog.Run()");
                // This calls EditorApplication.Exit(0) on success — last step.
                BakeWithCuratedCatalog.Run();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FullRebake] FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
