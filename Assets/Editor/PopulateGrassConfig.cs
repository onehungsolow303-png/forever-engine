using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

/// <summary>
/// Menu: Forever Engine → Populate Grass Config.
/// Finds a NatureManufacture Mountain pack grass mesh + material and assigns
/// them to `Assets/Resources/GrassConfig.asset`. Mesh is read from a prefab
/// under `Mountain Environment/Foliage and Grass/Prefabs/Prefab_grass_01_1`
/// so we inherit the artist-tuned scale and orientation on the base quad.
/// </summary>
public static class PopulateGrassConfig
{
    // Prefer pack prefabs so we pick up the artist's mesh+material pairing.
    // First hit wins; each path is a substring matched against prefab asset paths.
    private static readonly string[] GrassPrefabHints =
    {
        "Mountain Environment/Foliage and Grass/Prefabs/Prefab_grass_01_1",
        "Mountain Environment/Foliage and Grass/Prefabs/Prefab_grass_02_A_1",
        "Forest Environment Dynamic Nature/Foliage and Grass/Prefabs/Prefab_grass_beech_forest_01_1",
    };

    [MenuItem("Forever Engine/Populate Grass Config")]
    public static void Populate()
    {
        const string path = "Assets/Resources/GrassConfig.asset";
        var cfg = AssetDatabase.LoadAssetAtPath<GrassConfig>(path);
        if (cfg == null)
        {
            Debug.LogError($"[PopulateGrassConfig] No asset at {path}. Run `Create Grass Config Asset` first.");
            return;
        }

        GameObject prefab = null;
        foreach (var hint in GrassPrefabHints)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (prefab != null) break;
                }
            }
            if (prefab != null) break;
        }

        if (prefab == null)
        {
            Debug.LogError("[PopulateGrassConfig] No grass prefab matched. Check NatureManufacture packs are imported.");
            return;
        }

        var filter = prefab.GetComponentInChildren<MeshFilter>();
        var renderer = prefab.GetComponentInChildren<MeshRenderer>();
        if (filter == null || renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null)
        {
            Debug.LogError($"[PopulateGrassConfig] Prefab {prefab.name} missing mesh or material on any child.");
            return;
        }

        cfg.GrassMesh = filter.sharedMesh;
        cfg.GrassMaterial = renderer.sharedMaterial;
        // Force material to support GPU instancing. Idempotent.
        if (!cfg.GrassMaterial.enableInstancing)
        {
            cfg.GrassMaterial.enableInstancing = true;
            EditorUtility.SetDirty(cfg.GrassMaterial);
        }

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PopulateGrassConfig] Mesh={cfg.GrassMesh.name} Material={cfg.GrassMaterial.name} (instancing on).");
    }
}
