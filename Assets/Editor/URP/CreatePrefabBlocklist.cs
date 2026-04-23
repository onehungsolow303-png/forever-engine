#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Editor.URP
{
    /// <summary>
    /// Batchmode helper: creates an empty PrefabBlocklist.asset at Assets/Resources/.
    /// Run headless: Unity.exe -batchmode -executeMethod
    ///   ForeverEngine.Editor.URP.CreatePrefabBlocklist.Run -quit -logFile -
    /// </summary>
    public static class CreatePrefabBlocklist
    {
        private const string AssetPath = "Assets/Resources/PrefabBlocklist.asset";

        [MenuItem("Forever Engine/URP/Create PrefabBlocklist Asset")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var existing = AssetDatabase.LoadAssetAtPath<PrefabBlocklist>(AssetPath);
            if (existing != null)
            {
                Debug.Log($"[CreatePrefabBlocklist] {AssetPath} already exists — not overwriting.");
                return;
            }

            var bl = ScriptableObject.CreateInstance<PrefabBlocklist>();
            AssetDatabase.CreateAsset(bl, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CreatePrefabBlocklist] Created {AssetPath}.");
        }
    }
}
#endif
