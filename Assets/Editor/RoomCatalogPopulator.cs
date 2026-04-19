#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that auto-discovers prop prefabs from installed asset packs
/// and populates a <see cref="ForeverEngine.RoomCatalog"/> ScriptableObject.
/// Run from: Forever Engine → Populate Room Catalog.
/// </summary>
public static class RoomCatalogPopulator
{
    // ── Pack scan directories ─────────────────────────────────────────────

    // Prop source directories are intentionally mix-and-match across all
    // installed packs. Per user 2026-04-19: the game benefits from cross-pack
    // variety in a single room rather than one-pack-per-room homogeneity.
    private static readonly string[] PropDirs =
    {
        // Lordenfel / Multistory / Eternal Temple — original catalog
        "Assets/Lordenfel/Prefabs/Props",
        "Assets/Multistory Dungeons 2/Prefabs/Props",
        "Assets/Eternal Temple/Prefabs/Props",
        "Assets/Eternal Temple/Prefabs/Lanterns",
        // 3DForge Cave Adventure kit — lots of dungeon/cave decor
        "Assets/3DForge/Cave Adventure kit/Prefabs/Props",
        "Assets/3DForge/Cave Adventure kit/Prefabs/Wooden",
        "Assets/3DForge/Cave Adventure kit/Prefabs/Special",
        "Assets/3DForge/Cave Adventure kit/Prefabs/Plants",
        // 3DForge Catacombs — bones, skulls, corpses (good Lighting/Debris/Decorative)
        "Assets/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Catacombs/catacombs_props",
        // WaltWW Cave Dungeon Toolkit — purpose-built dungeon props
        "Assets/WaltWW/CaveDungeonToolkit/Prefabs/Props",
        "Assets/WaltWW/CaveDungeonToolkit/Prefabs/VFX",
        // Realistic Natural Cave 2 — small stalagmites work as room-floor debris
        "Assets/Realistic Natural Cave 2/Prefabs",
        // NAKED_SINGULARITY skeletons — great Debris for any dungeon
        "Assets/NAKED_SINGULARITY/DRAGON_CATACOMB/PREFABS/Decor/Skeleton_Bone",
    };

    // ── Name-based classification rules ──────────────────────────────────

    private static readonly (string pattern, ForeverEngine.PropCategory cat, bool wallMounted, float yOffset)[] Rules =
    {
        // Lighting (Lordenfel/Eternal Temple/Magic Pig classics)
        ("Torch",       ForeverEngine.PropCategory.Lighting,    true,  1.8f),
        ("torch",       ForeverEngine.PropCategory.Lighting,    true,  1.8f),
        ("Candle",      ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("candle",      ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("Candelabr",   ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("Brazier",     ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("brazier",     ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("Lantern",     ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("lantern",     ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("Lamp",        ForeverEngine.PropCategory.Lighting,    false, 0f),
        ("lamp",        ForeverEngine.PropCategory.Lighting,    false, 0f),
        // Lighting (3DForge Cave Adventure kit VFX)
        ("candle_flame",   ForeverEngine.PropCategory.Lighting, false, 0f),
        ("Godray",         ForeverEngine.PropCategory.Lighting, true,  2.5f),
        ("vent_gas",       ForeverEngine.PropCategory.Lighting, false, 0f),
        ("fog_spirit",     ForeverEngine.PropCategory.Lighting, false, 0f),

        // Containers
        ("Barrel",      ForeverEngine.PropCategory.Container,   false, 0f),
        ("barrel",      ForeverEngine.PropCategory.Container,   false, 0f),
        ("Crate",       ForeverEngine.PropCategory.Container,   false, 0f),
        ("crate",       ForeverEngine.PropCategory.Container,   false, 0f),
        ("Chest",       ForeverEngine.PropCategory.Container,   false, 0f),
        ("chest",       ForeverEngine.PropCategory.Container,   false, 0f),
        ("Bucket",      ForeverEngine.PropCategory.Container,   false, 0f),
        ("Basket",      ForeverEngine.PropCategory.Container,   false, 0f),

        // Furniture
        ("Table",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("table",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("Chair",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("chair",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("Bench",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("bench",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("Cupboard",    ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("Shelf",       ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("Bed",         ForeverEngine.PropCategory.Furniture,   false, 0f),
        ("Ladder",      ForeverEngine.PropCategory.Furniture,   true,  0f),

        // Debris
        ("Bone",         ForeverEngine.PropCategory.Debris,     false, 0f),
        ("bone",         ForeverEngine.PropCategory.Debris,     false, 0f),
        ("Skeleton",     ForeverEngine.PropCategory.Debris,     false, 0f),
        ("Rubble",       ForeverEngine.PropCategory.Debris,     false, 0f),
        ("rubble",       ForeverEngine.PropCategory.Debris,     false, 0f),
        ("Debris",       ForeverEngine.PropCategory.Debris,     false, 0f),
        ("Stalagmit",    ForeverEngine.PropCategory.Debris,     false, 0f),
        ("Stalagnat",    ForeverEngine.PropCategory.Debris,     true,  2.0f),
        ("CaveRock",     ForeverEngine.PropCategory.Debris,     false, 0f),

        // Decorative (catch-all for remaining props)
        ("Goblet",      ForeverEngine.PropCategory.Decorative,  false, 0f),
        ("Jug",         ForeverEngine.PropCategory.Decorative,  false, 0f),
        ("Potion",      ForeverEngine.PropCategory.Decorative,  false, 0f),
        ("Pike",        ForeverEngine.PropCategory.Decorative,  true,  0f),
        ("pike",        ForeverEngine.PropCategory.Decorative,  true,  0f),
        ("Ring",        ForeverEngine.PropCategory.Decorative,  true,  1.5f),
        ("Banner",      ForeverEngine.PropCategory.Decorative,  true,  2f),
    };

    // ── Default decoration presets ────────────────────────────────────────

    private static ForeverEngine.RoomDecorPreset[] DefaultPresets => new[]
    {
        new ForeverEngine.RoomDecorPreset
        {
            Name = "Corridor", ForCorridor = true,
            LightingCount = 2, FurnitureCount = 0, ContainerCount = 0,
            DebrisCount = 1, DecorativeCount = 0
        },
        new ForeverEngine.RoomDecorPreset
        {
            Name = "Chamber T1", MinTier = 0, MaxTier = 1,
            LightingCount = 3, FurnitureCount = 1, ContainerCount = 2,
            DebrisCount = 1, DecorativeCount = 1
        },
        new ForeverEngine.RoomDecorPreset
        {
            Name = "Chamber T2", MinTier = 2, MaxTier = 2,
            LightingCount = 3, FurnitureCount = 2, ContainerCount = 2,
            DebrisCount = 2, DecorativeCount = 2
        },
        new ForeverEngine.RoomDecorPreset
        {
            Name = "Chamber T3", MinTier = 3, MaxTier = 3,
            LightingCount = 4, FurnitureCount = 2, ContainerCount = 3,
            DebrisCount = 2, DecorativeCount = 3
        },
        new ForeverEngine.RoomDecorPreset
        {
            Name = "Boss Chamber", ForBoss = true,
            LightingCount = 6, FurnitureCount = 2, ContainerCount = 1,
            DebrisCount = 3, DecorativeCount = 4
        },
    };

    // ── Menu entry ────────────────────────────────────────────────────────

    [MenuItem("Forever Engine/Populate Room Catalog")]
    public static void Populate()
    {
        const string assetPath = "Assets/Resources/RoomCatalog.asset";

        var catalog = AssetDatabase.LoadAssetAtPath<ForeverEngine.RoomCatalog>(assetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ForeverEngine.RoomCatalog>();
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(catalog, assetPath);
            Debug.Log("[RoomCatalogPopulator] Created new RoomCatalog.asset");
        }

        catalog.Props.Clear();
        catalog.Presets.Clear();

        int found = 0;
        var seenPaths = new HashSet<string>();

        foreach (string dir in PropDirs)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seenPaths.Add(path)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                string name = prefab.name;
                var entry = Classify(prefab, name);
                if (entry != null)
                {
                    catalog.Props.Add(entry);
                    found++;
                }
            }
        }

        // Add default presets
        foreach (var preset in DefaultPresets)
            catalog.Presets.Add(preset);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RoomCatalogPopulator] Cataloged {found} props from {PropDirs.Length} directories, {catalog.Presets.Count} presets");
    }

    private static ForeverEngine.PropEntry Classify(GameObject prefab, string name)
    {
        foreach (var (pattern, cat, wallMounted, yOffset) in Rules)
        {
            if (name.Contains(pattern))
            {
                // Prefer lit variants for lighting
                if (cat == ForeverEngine.PropCategory.Lighting && name.Contains("Unlit"))
                    return null; // Skip unlit variants — use lit ones

                return new ForeverEngine.PropEntry
                {
                    Prefab = prefab,
                    Category = cat,
                    WallMounted = wallMounted,
                    YOffset = yOffset
                };
            }
        }

        // Unclassified props become decorative
        return new ForeverEngine.PropEntry
        {
            Prefab = prefab,
            Category = ForeverEngine.PropCategory.Decorative,
            WallMounted = false,
            YOffset = 0f
        };
    }
}
#endif
