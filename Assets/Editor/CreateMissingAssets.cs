#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates all missing ScriptableObject assets that code expects in Resources/.
/// Run from: Forever Engine → Create Missing Assets
/// </summary>
public static class CreateMissingAssets
{
    [MenuItem("Forever Engine/Create Missing Assets")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        CreateIfMissing<ForeverEngine.GameConfig>("Assets/Resources/GameConfig.asset");
        CreateIfMissing<ForeverEngine.RoomCatalog>("Assets/Resources/RoomCatalog.asset");

        // DungeonNPCConfig
        var npcConfigType = System.Type.GetType("ForeverEngine.Demo.Dungeon.DungeonNPCConfig, ForeverEngine");
        if (npcConfigType != null)
        {
            string path = "Assets/Resources/DungeonNPCConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) == null)
            {
                var asset = ScriptableObject.CreateInstance(npcConfigType);
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[CreateMissingAssets] Created {path}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CreateMissingAssets] Done — all missing assets created.");
    }

    private static void CreateIfMissing<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
        {
            Debug.Log($"[CreateMissingAssets] Already exists: {path}");
            return;
        }
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[CreateMissingAssets] Created {path}");
    }
}
#endif
