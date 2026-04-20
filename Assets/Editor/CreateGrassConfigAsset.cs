using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

public static class CreateGrassConfigAsset
{
    [MenuItem("Forever Engine/Create Grass Config Asset")]
    public static void Create()
    {
        const string path = "Assets/Resources/GrassConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<GrassConfig>(path) != null)
        {
            Debug.Log($"[CreateGrassConfigAsset] Already exists at {path}");
            return;
        }
        var so = ScriptableObject.CreateInstance<GrassConfig>();
        System.IO.Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CreateGrassConfigAsset] Created {path}. Run `Populate Grass Config` next.");
    }
}
