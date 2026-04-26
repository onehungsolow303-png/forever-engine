#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;
using ForeverEngine.Procedural;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Reads the on-disk BakedLayerIndex (.shared/baked/planet/layer_0/index.json)
    /// and resolves every SplatLayerGuid + TreePrototypeGuid to its concrete
    /// TerrainLayer / GameObject prefab via AssetDatabase. Writes the result to
    /// Assets/Resources/BakedAssetRegistry.asset so runtime code can resolve the
    /// same GUIDs without AssetDatabase.
    ///
    /// Run from menu (Forever Engine → Bake → Populate Asset Registry) after a
    /// fresh bake, or via batchmode -executeMethod.
    /// </summary>
    public static class BakedAssetRegistryPopulator
    {
        private const string LayerDir = "C:/Dev/.shared/baked/planet/layer_0";
        private const string RegistryPath = "Assets/Resources/BakedAssetRegistry.asset";

        [MenuItem("Forever Engine/Bake/Populate Asset Registry")]
        public static void PopulateMenu()
        {
            try { PopulateOrThrow(); EditorUtility.DisplayDialog("Asset Registry", $"Populated {RegistryPath}.", "OK"); }
            catch (Exception e) { EditorUtility.DisplayDialog("Asset Registry", e.Message, "OK"); }
        }

        public static void Run()
        {
            try { PopulateOrThrow(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError($"[AssetRegistry] FAIL: {e}"); EditorApplication.Exit(1); }
        }

        public static void PopulateOrThrow()
        {
            var indexPath = Path.Combine(LayerDir, "index.json");
            if (!File.Exists(indexPath))
                throw new InvalidOperationException($"missing {indexPath} — run a bake first");

            var index = BakedWorldReader.LoadLayerIndex(LayerDir);
            var splatGuids = index.SplatLayerGuids ?? Array.Empty<string>();
            var treeGuids  = index.TreePrototypeGuids ?? Array.Empty<string>();
            Debug.Log($"[AssetRegistry] index has {splatGuids.Length} splat layers + {treeGuids.Length} tree prototypes");

            Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
            var registry = AssetDatabase.LoadAssetAtPath<BakedAssetRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BakedAssetRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            var layerEntries = new List<BakedAssetRegistry.TerrainLayerEntry>(splatGuids.Length);
            int layersResolved = 0;
            foreach (var guid in splatGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var layer = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer != null) layersResolved++;
                else Debug.LogWarning($"[AssetRegistry] could not resolve TerrainLayer for guid {guid} (path '{path}')");
                layerEntries.Add(new BakedAssetRegistry.TerrainLayerEntry { Guid = guid, Layer = layer });
            }

            var prefabEntries = new List<BakedAssetRegistry.PrefabEntry>(treeGuids.Length);
            int prefabsResolved = 0;
            foreach (var guid in treeGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) prefabsResolved++;
                else Debug.LogWarning($"[AssetRegistry] could not resolve GameObject prefab for guid {guid} (path '{path}')");
                prefabEntries.Add(new BakedAssetRegistry.PrefabEntry { Guid = guid, Prefab = prefab });
            }

            // Scan every tile's props.bin for the union of prop prefab GUIDs
            // (Gaia GameObject placements). Resolve each via AssetDatabase so
            // the runtime prop renderer can instantiate without AssetDatabase.
            var propGuids = CollectPropGuids(LayerDir, index);
            var propEntries = new List<BakedAssetRegistry.PrefabEntry>(propGuids.Count);
            int propsResolved = 0;
            foreach (var guid in propGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) propsResolved++;
                else Debug.LogWarning($"[AssetRegistry] could not resolve prop prefab for guid {guid} (path '{path}')");
                propEntries.Add(new BakedAssetRegistry.PrefabEntry { Guid = guid, Prefab = prefab });
            }

            registry.TerrainLayers = layerEntries.ToArray();
            registry.TreePrefabs   = prefabEntries.ToArray();
            registry.PropPrefabs   = propEntries.ToArray();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AssetRegistry] wrote {RegistryPath}: layers {layersResolved}/{splatGuids.Length}, " +
                      $"trees {prefabsResolved}/{treeGuids.Length}, " +
                      $"props {propsResolved}/{propGuids.Count}");
        }

        private static HashSet<string> CollectPropGuids(string layerDir, BakedLayerIndex index)
        {
            var seen = new HashSet<string>();
            foreach (var t in index.Tiles ?? Array.Empty<BakedLayerTileEntry>())
            {
                var tileDir = Path.Combine(layerDir, t.Path);
                if (!Directory.Exists(tileDir)) continue;
                BakedMacroData macro;
                try { macro = BakedWorldReader.LoadMacro(tileDir); }
                catch (Exception e) { Debug.LogWarning($"[AssetRegistry] LoadMacro({tileDir}) failed: {e.Message}"); continue; }
                if (macro.Props == null) continue;
                foreach (var p in macro.Props)
                    if (!string.IsNullOrEmpty(p.PrefabGuid)) seen.Add(p.PrefabGuid);
            }
            return seen;
        }
    }
}
#endif
