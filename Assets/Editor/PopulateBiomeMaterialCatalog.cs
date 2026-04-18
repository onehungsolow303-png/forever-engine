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
    // Tuned 2026-04-18 against actual asset pack contents — Lordenfel for
    // grass/rock/mountain, NatureManufacture for ground/sand/soil variants,
    // Eternal Temple for dry-grass, Dragon Catacomb for snow.
    private static readonly (BiomeType biome, string[] hints)[] Rules =
    {
        (BiomeType.TemperateForest,   new[] { "M_ground_beech_forest_leaves", "M_ground_beech_forest_plants", "MI_Grass_03" }),
        (BiomeType.BorealForest,      new[] { "M_ground_beech_forest_Moss", "M_ground_beech_forest_moss", "M_ground_beech_forest_roots" }),
        (BiomeType.Taiga,             new[] { "M_ground_beech_forest_roots", "M_ground_beech_forest_soil" }),
        (BiomeType.Mountain,          new[] { "MI_Mountain_01.mat", "MI_Cliff_01.mat", "MI_Rock_Pile" }),
        (BiomeType.Grassland,         new[] { "MI_Grass_03.mat", "Grass_Material_01.mat" }),
        (BiomeType.Savanna,           new[] { "MI_Grass_03_Dry.mat", "Grass_Dry_Material_01.mat" }),
        (BiomeType.AridSteppe,        new[] { "M_ground_beech_forest_soil", "MI_Grass_03_Dry" }),
        (BiomeType.TropicalRainforest,new[] { "M_ground_beech_forest_plants", "M_ground_beech_forest_leaves" }),
        (BiomeType.Desert,            new[] { "M_ground_beech_forest_sand_rocks", "M_ground_beech_forest_sand.mat" }),
        (BiomeType.Tundra,            new[] { "_snow.mat", "MI_Brick_snow", "M_ground_beech_forest_sand grey" }),
        (BiomeType.IceSheet,          new[] { "_snow.mat", "MI_Brick_snow" }),
        (BiomeType.Beach,             new[] { "M_ground_beech_forest_sand.mat", "MatMarioSand" }),
        (BiomeType.Ocean,             new[] { "Water.mat", "M_ground_beech_forest_sand_rocks_grey" }),
        (BiomeType.River,             new[] { "Water.mat" }),
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
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            // Skip embedded sub-asset materials inside mesh files (.obj/.fbx).
            // Only standalone .mat assets carry usable shader+texture setups.
            if (p.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                matPaths.Add(p);
        }

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
