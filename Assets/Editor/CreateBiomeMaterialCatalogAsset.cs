#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

/// <summary>
/// Creates an empty BiomeMaterialCatalog ScriptableObject at Assets/Resources/BiomeMaterialCatalog.asset.
/// Run from: Forever Engine → Create Biome Material Catalog Asset
/// Or via batchmode: -executeMethod CreateBiomeMaterialCatalogAsset.Create
/// </summary>
public static class CreateBiomeMaterialCatalogAsset
{
    [MenuItem("Forever Engine/Create Biome Material Catalog Asset")]
    public static void Create()
    {
        const string path = "Assets/Resources/BiomeMaterialCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<BiomeMaterialCatalog>(path) != null)
        {
            Debug.Log($"[BiomeMaterialCatalog] Already exists at {path}");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = ScriptableObject.CreateInstance<BiomeMaterialCatalog>();
        AssetDatabase.CreateAsset(catalog, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BiomeMaterialCatalog] Created empty catalog at {path}");
    }
}
#endif
