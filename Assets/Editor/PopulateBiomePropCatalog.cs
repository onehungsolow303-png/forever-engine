using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

/// <summary>
/// Menu: Forever Engine → Populate Biome Prop Catalog.
/// Walks all project prefabs, matches filenames against substring hints
/// per biome, caps at MaxPrefabsPerRule per rule. Missing biomes fall
/// through to SurfaceDecorator's primitive fallback.
/// </summary>
public static class PopulateBiomePropCatalog
{
    // (biome, filename-hints, count-per-chunk, min-spacing-m, base-scale)
    private static readonly (BiomeType biome, string[] hints, int count, float minSpacing, float baseScale)[] Config =
    {
        (BiomeType.TemperateForest,    new[] { "Tree_", "BroadleafTree", "OakTree" },             20, 18f, 1.0f),
        (BiomeType.TemperateForest,    new[] { "Bush_", "Shrub" },                                10, 20f, 0.7f),
        (BiomeType.TemperateForest,    new[] { "Rock_", "Stone_" },                               5,  35f, 1.0f),
        (BiomeType.BorealForest,       new[] { "dwarf_pine", "Prefab_pine", "Pine_LOD" },          25, 16f, 1.0f),
        (BiomeType.BorealForest,       new[] { "Rock_", "Stone_" },                               8,  25f, 1.0f),
        (BiomeType.Mountain,           new[] { "Rock_", "Boulder", "Cliff_" },                    20, 18f, 1.0f),
        (BiomeType.Mountain,           new[] { "dwarf_pine", "Prefab_pine" },                     4,  40f, 0.9f),
        (BiomeType.Grassland,          new[] { "Tree_", "OakTree" },                              3,  50f, 1.0f),
        (BiomeType.Grassland,          new[] { "Rock_", "Stone_" },                               3,  40f, 0.8f),
        (BiomeType.Desert,             new[] { "Cactus_", "DesertPlant" },                        4,  45f, 1.0f),
        (BiomeType.Savanna,            new[] { "AcaciaTree", "SavannaTree", "Tree_" },            4,  50f, 1.2f),
        (BiomeType.TropicalRainforest, new[] { "Palm_", "JungleTree", "Tree_" },                  30, 12f, 1.0f),
        (BiomeType.Tundra,             new[] { "dwarf_pine", "Prefab_pine", "Pine_LOD" },          6,  35f, 0.8f),
        (BiomeType.Tundra,             new[] { "Rock_", "Stone_" },                               12, 22f, 1.0f),
    };

    private const int MaxPrefabsPerRule = 12;

    [MenuItem("Forever Engine/Populate Biome Prop Catalog")]
    public static void Populate()
    {
        const string path = "Assets/Resources/BiomePropCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<BiomePropCatalog>(path);
        if (catalog == null)
        {
            Debug.LogError($"[PopulateBiomePropCatalog] No catalog at {path}. Run `Create Biome Prop Catalog Asset` first.");
            return;
        }

        var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab");
        var prefabPaths = new List<string>(allPrefabGuids.Length);
        foreach (var guid in allPrefabGuids)
            prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));

        var rules = new List<BiomePropRule>();
        foreach (var cfg in Config)
        {
            var matched = new List<GameObject>();
            foreach (var hint in cfg.hints)
            {
                foreach (var p in prefabPaths)
                {
                    if (matched.Count >= MaxPrefabsPerRule) break;
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(p);
                    if (fileName.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                        if (go != null && !matched.Contains(go)) matched.Add(go);
                    }
                }
                if (matched.Count >= MaxPrefabsPerRule) break;
            }

            if (matched.Count > 0)
            {
                rules.Add(new BiomePropRule
                {
                    Biome = cfg.biome,
                    Prefabs = matched.ToArray(),
                    Count = cfg.count,
                    MinSpacing = cfg.minSpacing,
                    BaseScale = cfg.baseScale,
                });
                Debug.Log($"[PopulateBiomePropCatalog]   {cfg.biome} <- {matched.Count} prefabs matching [{string.Join(", ", cfg.hints)}]");
            }
            else
            {
                Debug.LogWarning($"[PopulateBiomePropCatalog]   {cfg.biome} <- (no prefab filenames matched {string.Join(", ", cfg.hints)})");
            }
        }

        catalog.Rules = rules.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PopulateBiomePropCatalog] Populated {rules.Count}/{Config.Length} rules.");
    }
}
