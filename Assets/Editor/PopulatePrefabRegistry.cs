using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

/// <summary>
/// Menu: Forever Engine → Populate Prefab Registry.
/// Walks BiomePropCatalog.GetAllRules(), collects every unique prefab reference,
/// captures (GUID, prefab) pairs, and writes to Resources/PrefabRegistry.asset.
/// Re-run after catalog changes or new asset packs. Required because AssetDatabase
/// is editor-only — the runtime resolver (PrefabRegistry.Instance) needs this asset.
/// </summary>
public static class PopulatePrefabRegistry
{
    private const string RegistryAssetPath = "Assets/Resources/PrefabRegistry.asset";
    private const string BiomePropCatalogPath = "Assets/Resources/BiomePropCatalog.asset";

    [MenuItem("Forever Engine/Populate Prefab Registry")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var catalog = AssetDatabase.LoadAssetAtPath<BiomePropCatalog>(BiomePropCatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[PopulatePrefabRegistry] Could not load {BiomePropCatalogPath}. Aborting.");
            return;
        }

        var entries = new List<PrefabRegistry.Entry>();
        var seenGuids = new HashSet<string>();
        var blocklist = AssetDatabase.LoadAssetAtPath<PrefabBlocklist>("Assets/Resources/PrefabBlocklist.asset");
        int skippedByBlocklist = 0;

        foreach (var rule in catalog.GetAllRules())
        {
            if (rule.Prefabs == null) continue;
            foreach (var prefab in rule.Prefabs)
            {
                if (prefab == null) continue;
                string path = AssetDatabase.GetAssetPath(prefab);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                if (!seenGuids.Add(guid)) continue;
                if (blocklist != null && blocklist.Contains(guid)) { skippedByBlocklist++; continue; }
                entries.Add(new PrefabRegistry.Entry { Guid = guid, Prefab = prefab });
            }
        }

        var registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(RegistryAssetPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<PrefabRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryAssetPath);
        }

        registry.SetEntries_Editor(entries);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PopulatePrefabRegistry] Wrote {entries.Count} entries to {RegistryAssetPath} (skipped {skippedByBlocklist} by PrefabBlocklist).");
    }
}
