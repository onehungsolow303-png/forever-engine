using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

/// <summary>
/// Menu: Forever Engine → Populate Biome Material Catalog.
/// Walks all project materials, matches each biome against substring
/// hints, assigns the first hit. Idempotent — re-running overwrites with
/// fresh discoveries. Prints a summary; missing biomes stay null and
/// TerrainGenerator falls back to solid color for them.
/// </summary>
public static class PopulateBiomeMaterialCatalog
{
    // Biome → substring hints. First material whose asset path contains
    // any hint (case-insensitive) wins. Order the hints most-specific-first.
    private static readonly (BiomeType biome, string[] hints)[] Rules =
    {
        (BiomeType.TemperateForest,   new[] { "Forest/Materials/Ground", "ForestGround", "GrassForest" }),
        (BiomeType.BorealForest,      new[] { "Forest/Materials/Ground", "ForestGround" }),
        (BiomeType.Mountain,          new[] { "Mountain/Materials/Rock", "MountainRock", "CliffRock" }),
        (BiomeType.Grassland,         new[] { "Medieval/Materials/Grass", "Grass_", "GrassGround" }),
        (BiomeType.Savanna,           new[] { "Savanna/Materials/Ground", "DryGrass" }),
        (BiomeType.Desert,            new[] { "Desert/Materials/Sand", "Sand_", "SandGround" }),
        (BiomeType.Tundra,            new[] { "Tundra/Materials/Snow", "Snow_", "IceGround" }),
        (BiomeType.IceSheet,          new[] { "Ice/Materials", "IceSheet", "Glacier" }),
        (BiomeType.Beach,             new[] { "Beach/Materials/Sand", "BeachSand" }),
        (BiomeType.Ocean,             new[] { "Water/Materials", "OceanWater" }),
    };

    [MenuItem("Forever Engine/Populate Biome Material Catalog")]
    public static void Populate()
    {
        const string path = "Assets/Resources/BiomeMaterialCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<BiomeMaterialCatalog>(path);
        if (catalog == null)
        {
            Debug.LogError($"[PopulateBiomeMaterialCatalog] No catalog at {path}. Run `Create Biome Material Catalog Asset` first.");
            return;
        }

        var allMaterialGuids = AssetDatabase.FindAssets("t:Material");
        var matPaths = new List<string>(allMaterialGuids.Length);
        foreach (var guid in allMaterialGuids)
            matPaths.Add(AssetDatabase.GUIDToAssetPath(guid));

        var entries = new List<BiomeMaterialEntry>();
        int found = 0, missing = 0;
        foreach (var rule in Rules)
        {
            Material hit = null;
            foreach (var hint in rule.hints)
            {
                foreach (var p in matPaths)
                {
                    if (p.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hit = AssetDatabase.LoadAssetAtPath<Material>(p);
                        if (hit != null) break;
                    }
                }
                if (hit != null) break;
            }

            if (hit != null)
            {
                entries.Add(new BiomeMaterialEntry { Biome = rule.biome, Material = hit });
                Debug.Log($"[PopulateBiomeMaterialCatalog]   {rule.biome} → {AssetDatabase.GetAssetPath(hit)}");
                found++;
            }
            else
            {
                Debug.LogWarning($"[PopulateBiomeMaterialCatalog]   {rule.biome} → (no match among {rule.hints.Length} hints; fallback to solid color)");
                missing++;
            }
        }

        catalog.Entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PopulateBiomeMaterialCatalog] Populated {found}/{Rules.Length} biomes ({missing} missing).");
    }
}
