#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Procedural.Editor
{
    public static class AddAssetVarietyOverlay
    {
        private const string ScenePath = "Assets/Scenes/GaiaWorld_Coniferous_Forest_Medium.unity";
        private const float Spacing = 6f;
        private const string ParentName = "AssetVarietyOverlay";

        private struct Source
        {
            public string Label;
            public string[] Roots;
            public int Take;
        }

        // 2026-04-29 PM full unlock — all 12 packs.
        // Fixes shipped: Forst CTI URP HLSL (10-arg CTI_AnimateVertexSG_float overload,
        // _WindPower/_WindStrength declarations in BillboardVertex*.hlsl), TFP URP_17
        // shadergraphs extracted (TFP mats reference Forst's via shared GUID 59b526d6),
        // 99 URP/Lit material stubs synthesized, 128 missing-shader mats forced to URP/Lit,
        // SeedMesh Vegetation Shaders pkg extracted, MatFixer extended to all 12 packs.
        private static readonly Source[] Sources = new[]
        {
            new Source { Label = "PW Trees",       Roots = new[]{ "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees" }, Take = 10 },
            new Source { Label = "PW Plants",      Roots = new[]{ "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Plants" }, Take = 10 },
            new Source { Label = "PW Stones",      Roots = new[]{ "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Prefabs/Stones" }, Take = 6 },
            new Source { Label = "NM Forest",      Roots = new[]{ "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature" }, Take = 8 },
            new Source { Label = "NM Mountain",    Roots = new[]{ "Assets/NatureManufacture Assets/Mountain Environment" }, Take = 8 },
            new Source { Label = "NM Coast",       Roots = new[]{ "Assets/NatureManufacture Assets/Coast Environment" }, Take = 6 },
            new Source { Label = "Forst Conifers", Roots = new[]{ "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs" }, Take = 4 },
            new Source { Label = "TFP Tropical",   Roots = new[]{ "Assets/TFP/2_Prefabs" }, Take = 8 },
            new Source { Label = "Swamp Bundle",   Roots = new[]{ "Assets/_SwampBundle" }, Take = 8 },
            new Source { Label = "G-Star Dead",    Roots = new[]{ "Assets/G-Star/QualityDeadForest/URP/Prefabs" }, Take = 6 },
            new Source { Label = "SeedMesh",       Roots = new[]{ "Assets/SeedMesh/Jungle-Tropical Vegetation/Vegetation" }, Take = 6 },
            new Source { Label = "Hivemind Veg",   Roots = new[]{ "Assets/Hivemind/Art/Prefabs" }, Take = 4 },
        };

        private static readonly string[] KeepKeywords = new[]
        {
            "tree","pine","spruce","birch","oak","maple","sequoia","palm","banana","kapok","fir","conifer",
            "bush","plant","fern","grass","flower","mushroom","moss","stump","root","weed","shrub","leaf",
            "snag","tussock","cone","needle","sapling",
            "stone","rock","boulder","pebble"
        };

        // Note: don't reject "_lod" wholesale — PW Trees/Plants ALL ship as *_LOD2 / *_LOD3 standalone prefabs.
        // Reject low-LODs (LOD0/LOD1 typically lowest detail in PW convention).
        private static readonly string[] RejectKeywords = new[]
        {
            "billboard","_atlas","tutorial","readme","preview","icon","_demo_","manager","controller",
            "_lod0","_lod1",
            "house","wall","floor","door","fence","crate","barrel","weapon","sword","cart","wagon","ladder","stair",
            "fi_vil_","camp_","castle","sm_metal","sm_wood","sm_stone_kit","pot",
            "scene","example","test","sample",
            // PW Spruce + Sequoia: known unrenderable per gaia Bug #1 / project_pw_spruce_deferred.md
            "spruce","sequoia",
        };

        public static void Run()
        {
            Debug.Log("[AssetVariety] === starting ===");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new System.Exception($"Couldn't open {ScenePath}");

            var collected = new List<(string label, GameObject prefab)>();
            foreach (var src in Sources)
            {
                int taken = 0;
                var seenPaths = new HashSet<string>();
                var seenFamilies = new HashSet<string>(); // strip _LODn suffix so we don't take 3 LODs of one model
                foreach (var root in src.Roots)
                {
                    if (!AssetDatabase.IsValidFolder(root))
                    {
                        Debug.LogWarning($"[AssetVariety] root missing: {root}");
                        continue;
                    }
                    var guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                    // Sort so LOD3 / LOD2 (more detailed in PW convention) come before LOD0/LOD1
                    var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g))
                                     .OrderByDescending(p => p) // alphabetical desc happens to put _LOD3 before _LOD0
                                     .ToList();
                    foreach (var path in paths)
                    {
                        if (taken >= src.Take) break;
                        if (seenPaths.Contains(path)) continue;
                        var name = Path.GetFileNameWithoutExtension(path).ToLower();
                        if (RejectKeywords.Any(k => name.Contains(k))) continue;
                        if (!KeepKeywords.Any(k => name.Contains(k))) continue;
                        // Family = name with _LODn suffix removed (so PW_Tree_Birch_01_LOD3 + LOD2 collapse to one family)
                        var family = System.Text.RegularExpressions.Regex.Replace(name, @"_lod\d+$", "");
                        if (seenFamilies.Contains(family)) continue;
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab == null) continue;
                        if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                        collected.Add((src.Label, prefab));
                        seenPaths.Add(path);
                        seenFamilies.Add(family);
                        taken++;
                    }
                    if (taken >= src.Take) break;
                }
                Debug.Log($"[AssetVariety] '{src.Label}': took {taken}");
            }
            Debug.Log($"[AssetVariety] total {collected.Count} unique prefabs collected");

            if (collected.Count == 0)
                throw new System.Exception("No prefabs collected — keyword filter rejected everything");

            // Find the terrain that contains world origin (0,0)
            var terrains = Terrain.activeTerrains;
            if (terrains.Length == 0)
                throw new System.Exception("No active terrains in scene — bake first?");
            Terrain target = null;
            foreach (var t in terrains)
            {
                var p = t.transform.position;
                var s = t.terrainData.size;
                if (0 >= p.x && 0 < p.x + s.x && 0 >= p.z && 0 < p.z + s.z) { target = t; break; }
            }
            if (target == null) target = terrains[0];
            Debug.Log($"[AssetVariety] using terrain '{target.name}' at {target.transform.position}, size {target.terrainData.size}");

            // Remove any prior overlay so re-runs are idempotent
            var existing = GameObject.Find(ParentName);
            if (existing != null) Object.DestroyImmediate(existing);
            var parent = new GameObject(ParentName).transform;

            // Place in grid centered on world (0, 0)
            int side = (int)System.Math.Ceiling(System.Math.Sqrt(collected.Count));
            float half = side * Spacing / 2f;
            int placed = 0;
            for (int i = 0; i < collected.Count; i++)
            {
                int gx = i % side;
                int gz = i / side;
                float wx = gx * Spacing - half + Spacing / 2f;
                float wz = gz * Spacing - half + Spacing / 2f;
                // Sample using terrain-local coords; SampleHeight expects world-space input that will be remapped
                float wy = target.SampleHeight(new Vector3(wx, 0, wz)) + target.transform.position.y;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(collected[i].prefab);
                if (inst == null) continue;
                inst.transform.SetParent(parent, worldPositionStays: false);
                inst.transform.position = new Vector3(wx, wy, wz);
                inst.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                inst.name = $"{collected[i].label} :: {collected[i].prefab.name}";
                placed++;
            }
            Debug.Log($"[AssetVariety] placed {placed} prefab instances at world origin ±{half:F1}m on terrain '{target.name}'");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AssetVariety] scene saved");

            Debug.Log("[AssetVariety] === re-baking props.bin via MacroBakeTool ===");
            MacroBakeTool.BakeAllTilesInSceneOrThrow();
            Debug.Log("[AssetVariety] === MacroBakeTool done ===");

            Debug.Log("[AssetVariety] === refreshing BakedAssetRegistry ===");
            BakedAssetRegistryPopulator.PopulateOrThrow();
            Debug.Log("[AssetVariety] === BakedAssetRegistry done ===");

            Debug.Log("[AssetVariety] === DONE ===");
        }
    }
}
#endif
