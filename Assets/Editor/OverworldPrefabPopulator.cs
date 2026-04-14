#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that auto-discovers tree, building, and dungeon gate prefabs
/// from installed asset packs and populates the OverworldPrefabMapper SO.
/// Run from: Forever Engine → Populate Overworld Prefabs.
/// </summary>
public static class OverworldPrefabPopulator
{
    // ── Asset pack paths ─────────────────────────────────────────────────

    private static readonly string TreeDir =
        "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Beech Trees/Prefabs";

    private static readonly string[] BuildingDirs =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/_Building Sets",
    };

    private static readonly string[] GatePrefabPaths =
    {
        "Assets/Lordenfel/Prefabs/Architecture/Towers/SM_Gate_02.prefab",
        "Assets/Lordenfel/Prefabs/Architecture/DoorsAndBars/SM_Wooden_Gate_01.prefab",
    };

    private static readonly string[] GrassMaterialPaths =
    {
        "Assets/Lordenfel/Source/Materials/MI_Grass_03.mat",
        "Assets/Lordenfel/Source/Materials/MI_Mountain_01_Grass.mat",
        "Assets/Lordenfel/Source/Materials/MI_Mountain_02_Grass.mat",
    };

    // ── Location marker prefab paths ────────────────────────────────────

    private static readonly string CampFirePath =
        "Assets/Lordenfel/Prefabs/Props/SM_Brazier_02_Lit.prefab";

    private static readonly string ShrinePath =
        "Assets/Eternal Temple/Prefabs/Props/Furniture/Stone_Altar_01.prefab";

    private static readonly string GladePath =
        "Assets/Eternal Temple/Prefabs/Arch Alley/Arch_Alley_01.prefab";

    private static readonly string FortressPath =
        "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Buiding Parts/Tower1a.prefab";

    private static readonly string CastlePath =
        "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Buiding Parts/Tower6a.prefab";

    private static readonly string[] RuinsPaths =
    {
        "Assets/Eternal Temple/Prefabs/Damaged/Building_Ruins_01.prefab",
        "Assets/Eternal Temple/Prefabs/Damaged/Building_Ruins_02.prefab",
        "Assets/Eternal Temple/Prefabs/Damaged/Building_Ruins_03.prefab",
        "Assets/Eternal Temple/Prefabs/Damaged/Building_Ruins_04.prefab",
        "Assets/Eternal Temple/Prefabs/Damaged/Building_Ruins_05.prefab",
    };

    // ── Menu entry ────────────────────────────────────────────────────────

    [MenuItem("Forever Engine/Populate Overworld Prefabs")]
    public static void Populate()
    {
        const string assetPath = "Assets/Resources/OverworldPrefabMap.asset";

        var mapper = AssetDatabase.LoadAssetAtPath<ForeverEngine.Demo.Overworld.OverworldPrefabMapper>(assetPath);
        if (mapper == null)
        {
            mapper = ScriptableObject.CreateInstance<ForeverEngine.Demo.Overworld.OverworldPrefabMapper>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(mapper, assetPath);
            Debug.Log("[OverworldPrefabPopulator] Created new OverworldPrefabMap.asset");
        }

        // ── Trees (ForestScatter + ForestPrefabs) ────────────────────────

        var trees = new System.Collections.Generic.List<GameObject>();
        string[] treeGuids = AssetDatabase.FindAssets("t:Prefab", new[] { TreeDir });
        foreach (string guid in treeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Skip Vegetation Studio variants — they need VS plugin
            if (path.Contains("Vegetation Studio")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                trees.Add(prefab);
        }

        // Use trees for both ForestPrefabs (one per hex) and ForestScatter (multiple)
        mapper.ForestPrefabs = trees.ToArray();
        mapper.ForestScatter = trees.ToArray();
        Debug.Log($"[OverworldPrefabPopulator] Found {trees.Count} tree prefabs");

        // ── Buildings (TownPrefab + scatter) ─────────────────────────────

        var buildings = new System.Collections.Generic.List<GameObject>();
        foreach (string dir in BuildingDirs)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    buildings.Add(prefab);
            }
        }

        // First building becomes TownPrefab, rest become RuinsScatter
        if (buildings.Count > 0)
        {
            // Pick ExteriorBuilding types for town, BasicBuilding for camp
            GameObject town = null, camp = null;
            foreach (var b in buildings)
            {
                if (town == null && b.name.Contains("Exterior")) town = b;
                if (camp == null && b.name.Contains("Basic")) camp = b;
            }
            mapper.TownPrefab = town ?? buildings[0];
            mapper.CampPrefab = camp ?? (buildings.Count > 1 ? buildings[1] : buildings[0]);
            mapper.RuinsPrefabs = buildings.ToArray();
            mapper.RuinsScatter = buildings.Count > 3
                ? buildings.GetRange(0, 3).ToArray()
                : buildings.ToArray();
            mapper.PlainsScatter = buildings.Count > 2
                ? new[] { buildings[buildings.Count - 1] }
                : new GameObject[0];
        }
        Debug.Log($"[OverworldPrefabPopulator] Found {buildings.Count} building prefabs");

        // ── Dungeon gate ─────────────────────────────────────────────────

        foreach (string path in GatePrefabPaths)
        {
            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (gate != null)
            {
                mapper.DungeonEntrancePrefab = gate;
                Debug.Log($"[OverworldPrefabPopulator] Dungeon entrance: {gate.name}");
                break;
            }
        }

        // ── Ground materials ─────────────────────────────────────────────

        foreach (string path in GrassMaterialPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                if (mapper.PlainsGround == null) mapper.PlainsGround = mat;
                else if (mapper.ForestGround == null) mapper.ForestGround = mat;
                else if (mapper.MountainGround == null) mapper.MountainGround = mat;
            }
        }

        // Fallback: create simple colored materials for biomes without pack materials
        if (mapper.WaterGround == null)
        {
            mapper.WaterGround = CreateSimpleMaterial(
                new Color(0.15f, 0.25f, 0.5f), "WaterGround", 0.8f);
        }
        if (mapper.RuinsGround == null)
        {
            mapper.RuinsGround = CreateSimpleMaterial(
                new Color(0.4f, 0.35f, 0.3f), "RuinsGround", 0.15f);
        }

        // ── Mountain scatter (use tree variants for now) ─────────────────

        if (trees.Count > 3)
            mapper.MountainScatter = new[] { trees[0], trees[trees.Count / 2] };

        // ── Location marker prefabs ─────────────────────────────────────

        int locationCount = 0;

        var campFire = AssetDatabase.LoadAssetAtPath<GameObject>(CampFirePath);
        if (campFire != null) { mapper.CampFirePrefab = campFire; locationCount++; }
        else Debug.LogWarning($"[OverworldPrefabPopulator] CampFire prefab not found: {CampFirePath}");

        var shrine = AssetDatabase.LoadAssetAtPath<GameObject>(ShrinePath);
        if (shrine != null) { mapper.ShrinePrefab = shrine; locationCount++; }
        else Debug.LogWarning($"[OverworldPrefabPopulator] Shrine prefab not found: {ShrinePath}");

        var glade = AssetDatabase.LoadAssetAtPath<GameObject>(GladePath);
        if (glade != null) { mapper.GladePrefab = glade; locationCount++; }
        else Debug.LogWarning($"[OverworldPrefabPopulator] Glade prefab not found: {GladePath}");

        var fortress = AssetDatabase.LoadAssetAtPath<GameObject>(FortressPath);
        if (fortress != null) { mapper.FortressPrefab = fortress; locationCount++; }
        else Debug.LogWarning($"[OverworldPrefabPopulator] Fortress prefab not found: {FortressPath}");

        var castle = AssetDatabase.LoadAssetAtPath<GameObject>(CastlePath);
        if (castle != null) { mapper.CastlePrefab = castle; locationCount++; }
        else Debug.LogWarning($"[OverworldPrefabPopulator] Castle prefab not found: {CastlePath}");

        var ruinsList = new System.Collections.Generic.List<GameObject>();
        foreach (string ruinPath in RuinsPaths)
        {
            var ruin = AssetDatabase.LoadAssetAtPath<GameObject>(ruinPath);
            if (ruin != null) ruinsList.Add(ruin);
        }
        if (ruinsList.Count > 0)
        {
            mapper.LocationRuinsPrefabs = ruinsList.ToArray();
            locationCount += ruinsList.Count;
        }
        else
        {
            Debug.LogWarning("[OverworldPrefabPopulator] No ruin prefabs found in Eternal Temple/Prefabs/Damaged/");
        }

        Debug.Log($"[OverworldPrefabPopulator] Assigned {locationCount} location marker prefabs");

        EditorUtility.SetDirty(mapper);
        AssetDatabase.SaveAssets();

        int total = trees.Count + buildings.Count + locationCount;
        Debug.Log($"[OverworldPrefabPopulator] Done — {total} total prefabs assigned to mapper");
    }

    private static Material CreateSimpleMaterial(Color color, string name, float smoothness)
    {
        string path = $"Assets/Resources/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        mat.name = name;

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
#endif
