using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

public static class CreateBiomePropCatalogAsset
{
    [MenuItem("Forever Engine/Create Biome Prop Catalog Asset")]
    public static void Create()
    {
        const string path = "Assets/Resources/BiomePropCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<BiomePropCatalog>(path) != null)
        {
            Debug.Log($"[BiomePropCatalog] Already exists at {path}");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = ScriptableObject.CreateInstance<BiomePropCatalog>();
        AssetDatabase.CreateAsset(catalog, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BiomePropCatalog] Created empty catalog at {path}");
    }
}
