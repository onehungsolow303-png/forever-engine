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
    // Tuned 2026-04-18 — favor NatureManufacture Mountain Environment pack
    // for natural outdoor ground (pine needles / moss / flat rock / heather
    // / stones) over the Forest pack's sand-grey which is actually a
    // flagstone path, not a natural surface.
    private static readonly (BiomeType biome, string[] hints)[] Rules =
    {
        // Forested biomes — Mountain pack has real pine-needle/moss grounds.
        (BiomeType.TemperateForest,   new[] { "Mountain Environment/Ground/Materials/M_ground_moss_01", "Mountain Environment/Ground/Materials/M_ground_pine_needles_01" }),
        (BiomeType.BorealForest,      new[] { "Mountain Environment/Ground/Materials/M_ground_pine_needles_02", "Mountain Environment/Ground/Materials/M_ground_pine_needles_03" }),
        (BiomeType.Taiga,             new[] { "Mountain Environment/Ground/Materials/M_ground_roots_01", "Mountain Environment/Ground/Materials/M_ground_pine_needles_01" }),
        (BiomeType.TropicalRainforest,new[] { "Mountain Environment/Ground/Materials/M_ground_moss_01", "Mountain Environment/Ground/Materials/M_ground_grass_02" }),
        // Grasslands — Mountain pack grass feels natural; dry variant for arid.
        (BiomeType.Grassland,         new[] { "Mountain Environment/Ground/Materials/M_ground_grass_01.mat", "Mountain Environment/Ground/Materials/M_ground_grass_02" }),
        (BiomeType.Savanna,           new[] { "Mountain Environment/Ground/Materials/M_ground_grass_01_dry", "Mountain Environment/Ground/Materials/M_ground_heather_01" }),
        (BiomeType.AridSteppe,        new[] { "Mountain Environment/Ground/Materials/M_ground_soil_01.mat", "Mountain Environment/Ground/Materials/M_ground_grass_01_dry" }),
        // Cold + bare — flat rock reads as tundra plateau, stones for glaciers.
        (BiomeType.Tundra,            new[] { "Mountain Environment/Ground/Materials/M_Ground_Flat_rock_01", "Mountain Environment/Ground/Materials/M_ground_stones_01" }),
        (BiomeType.IceSheet,          new[] { "Mountain Environment/Ground/Materials/M_ground_stones_01", "Mountain Environment/Ground/Materials/M_Ground_Flat_rock_01" }),
        // Mountain — reuse flat-rock + Lordenfel cliff as backup.
        (BiomeType.Mountain,          new[] { "Mountain Environment/Ground/Materials/M_Ground_Flat_rock_01", "MI_Mountain_01.mat", "MI_Cliff_01.mat" }),
        // Hot/dry — Forest pack sand_rocks works well.
        (BiomeType.Desert,            new[] { "M_ground_beech_forest_sand_rocks.mat", "M_ground_beech_forest_sand.mat" }),
        (BiomeType.Beach,             new[] { "M_ground_beech_forest_sand.mat" }),
        // Water.
        (BiomeType.Ocean,             new[] { "Water.mat" }),
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
