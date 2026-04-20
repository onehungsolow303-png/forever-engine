#if UNITY_EDITOR
using System.Collections.Generic;
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

    // Finished-building directories across packs. Per user 2026-04-19:
    // overworld buildings mix-and-match across packs the same way room props
    // do. 3DForge's "fe_vil_*" / "fi_vil_*" naming flags modular kit pieces
    // that must be filtered out — see BuildingPieceSkipPrefixes below.
    private static readonly string[] BuildingDirs =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/_Building Sets",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Base/Templates",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Buildings/CountrySide",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Buildings/Farm",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Buildings/ForestVillage",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Buildings/MedievalVillage",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Buildings/Mountain Side",
        "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/Buildings/Vikings",
    };

    // Prefab-name prefixes that mark modular kit pieces inside a "Buildings"
    // directory (e.g., 19 of Farm's 22 prefabs are "fe_vil_farm_silo_roof_*"
    // etc.). Registering these as standalone buildings looks broken — only
    // assembled houses should flow into the town/ruins scatter.
    private static readonly string[] BuildingPieceSkipPrefixes =
    {
        "fe_vil_",
        "fi_vil_",
    };

    private static readonly string[] BuildingPieceSkipFragments =
    {
        "_part_",
        "_piece_",
    };

    private static bool LooksLikeKitPiece(string fileName)
    {
        foreach (var p in BuildingPieceSkipPrefixes)
            if (fileName.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var f in BuildingPieceSkipFragments)
            if (fileName.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

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
        int kitPiecesSkipped = 0;
        foreach (string dir in BuildingDirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogWarning($"[OverworldPrefabPopulator] BuildingDirs entry missing: {dir}");
                continue;
            }
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (LooksLikeKitPiece(fileName)) { kitPiecesSkipped++; continue; }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    buildings.Add(prefab);
            }
        }
        if (kitPiecesSkipped > 0)
            Debug.Log($"[OverworldPrefabPopulator] Skipped {kitPiecesSkipped} modular kit pieces (fe_vil_*/fi_vil_*/_part_/_piece_)");

        // Collect ALL Exterior/Basic matches for variety — each town/camp picks
        // a random prefab from its array at spawn. Keeps the single-slot fields
        // populated as a fallback for consumers that haven't migrated yet.
        if (buildings.Count > 0)
        {
            var towns = new List<GameObject>();
            var camps = new List<GameObject>();
            foreach (var b in buildings)
            {
                if (b.name.Contains("Exterior")) towns.Add(b);
                if (b.name.Contains("Basic")) camps.Add(b);
            }
            // Fallback — if zero Exterior matches, treat any building as a town candidate.
            if (towns.Count == 0) towns.AddRange(buildings);
            if (camps.Count == 0)
                camps.AddRange(buildings.Count > 1 ? buildings.GetRange(1, buildings.Count - 1) : buildings);

            mapper.TownPrefab = towns[0];
            mapper.CampPrefab = camps[0];
            mapper.TownPrefabs = towns.ToArray();
            mapper.CampPrefabs = camps.ToArray();
            mapper.RuinsPrefabs = buildings.ToArray();
            mapper.RuinsScatter = buildings.Count > 3
                ? buildings.GetRange(0, 3).ToArray()
                : buildings.ToArray();
            mapper.PlainsScatter = buildings.Count > 2
                ? new[] { buildings[buildings.Count - 1] }
                : new GameObject[0];
            Debug.Log($"[OverworldPrefabPopulator] TownPrefabs={towns.Count}, CampPrefabs={camps.Count}");
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
