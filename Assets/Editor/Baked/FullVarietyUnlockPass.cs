#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Single batchmode entry point that orchestrates a full asset-variety unlock pass:
    ///   1. Refresh AssetDatabase (pick up Python-extracted TFP URP files)
    ///   2. Run NatureManufactureMatFixer.Run() (extended folder list)
    ///   3. Run AddAssetVarietyOverlay.Run() (re-pull all 12 packs, re-bake props.bin, refresh BakedAssetRegistry)
    ///   4. Run CaptureAssetVarietyScreenshots.Run() (7 angles to /c/tmp/asset-showcase/)
    /// </summary>
    public static class FullVarietyUnlockPass
    {
        public static void Run()
        {
            Debug.Log("[FullUnlock] === Step 1/4: AssetDatabase.Refresh ===");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Debug.Log("[FullUnlock] === Step 2/4: NatureManufactureMatFixer.Run ===");
            // NM MatFixer is in the global Editor namespace (Assets/Editor/NatureManufactureMatFixer.cs).
            // Invoke via reflection so this script (in ForeverEngine.Editor.Baked.dll) doesn't need
            // an asmdef ref to Assembly-CSharp-Editor.
            var fixerType = System.Type.GetType("ForeverEngine.Editor.NatureManufactureMatFixer, Assembly-CSharp-Editor")
                         ?? FindTypeByName("NatureManufactureMatFixer");
            if (fixerType != null)
            {
                var m = fixerType.GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                m?.Invoke(null, null);
                Debug.Log("[FullUnlock]   MatFixer.Run completed");
            }
            else
            {
                Debug.LogWarning("[FullUnlock]   NatureManufactureMatFixer type not found — skipping");
            }

            Debug.Log("[FullUnlock] === Step 3/4: AddAssetVarietyOverlay.Run ===");
            AddAssetVarietyOverlay.Run();

            Debug.Log("[FullUnlock] === Step 4/4: CaptureAssetVarietyScreenshots.Run ===");
            CaptureAssetVarietyScreenshots.Run();

            Debug.Log("[FullUnlock] === DONE ===");
        }

        private static System.Type FindTypeByName(string name)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == name) return t;
                }
            }
            return null;
        }
    }
}
#endif
