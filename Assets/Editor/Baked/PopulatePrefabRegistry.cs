using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Procedural.Editor
{
/// <summary>
/// Menu: Forever Engine → Populate Prefab Registry.
/// Walks AssetPackBiomeCatalog.Entries[] (TreePrefabs/RockPrefabs/BushPrefabs/
/// StructurePrefabs) — the single source of truth also consumed by the baked
/// pipeline's PropPlacementSampler — collects every unique (GUID, prefab) pair,
/// and writes to Resources/PrefabRegistry.asset. Re-run after catalog changes
/// or new asset packs. Required because AssetDatabase is editor-only — the
/// runtime resolver (PrefabRegistry.Instance) needs this asset so baked prop
/// placements (which reference prefabs by GUID) can resolve at client startup.
/// </summary>
public static class PopulatePrefabRegistry
{
    public const string RegistryAssetPath = "Assets/Resources/PrefabRegistry.asset";
    public const string AssetPackBiomeCatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";

    [MenuItem("Forever Engine/Populate Prefab Registry")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var catalog = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(AssetPackBiomeCatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[PopulatePrefabRegistry] Could not load {AssetPackBiomeCatalogPath}. Aborting.");
            return;
        }

        var blocklist = AssetDatabase.LoadAssetAtPath<PrefabBlocklist>("Assets/Resources/PrefabBlocklist.asset");
        var entries = CollectEntries(catalog, blocklist, out int skippedByBlocklist);

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

    /// <summary>
    /// Builds the registry entry list from an AssetPackBiomeCatalog. Public so
    /// tests can assert GUID coverage without invoking the full menu command.
    /// </summary>
    public static List<PrefabRegistry.Entry> CollectEntries(
        AssetPackBiomeCatalog catalog,
        PrefabBlocklist blocklist,
        out int skippedByBlocklist)
    {
        skippedByBlocklist = 0;
        var entries = new List<PrefabRegistry.Entry>();
        var seenGuids = new HashSet<string>();

        if (catalog?.Entries == null) return entries;

        foreach (var packEntry in catalog.Entries)
        {
            if (packEntry == null) continue;
            Harvest(packEntry.TreePrefabs, entries, seenGuids, blocklist, ref skippedByBlocklist);
            Harvest(packEntry.RockPrefabs, entries, seenGuids, blocklist, ref skippedByBlocklist);
            Harvest(packEntry.BushPrefabs, entries, seenGuids, blocklist, ref skippedByBlocklist);
            Harvest(packEntry.StructurePrefabs, entries, seenGuids, blocklist, ref skippedByBlocklist);
        }
        return entries;
    }

    private static void Harvest(
        GameObject[] prefabs,
        List<PrefabRegistry.Entry> entries,
        HashSet<string> seenGuids,
        PrefabBlocklist blocklist,
        ref int skippedByBlocklist)
    {
        if (prefabs == null) return;
        foreach (var prefab in prefabs)
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
}
}
