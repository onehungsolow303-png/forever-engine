using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    public class AssetPackCategorizationWindow : EditorWindow
    {
        private DiscoveredPack[] _packs;
        private Dictionary<string, List<BiomeType>> _assignments = new();
        private Vector2 _scroll;
        private const string CatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";

        [MenuItem("Forever Engine/Bake/Categorize Asset Packs")]
        public static void Open() => GetWindow<AssetPackCategorizationWindow>("Pack Categorization");

        private void OnEnable() => Rescan();

        private void Rescan()
        {
            _packs = AssetPackScanner.ScanRoot(Application.dataPath);
            _assignments.Clear();

            var existing = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(CatalogPath);
            if (existing != null)
            {
                foreach (var e in existing.Entries)
                    _assignments[e.PackName] = new List<BiomeType>(e.SuitableBiomes);
            }

            foreach (var p in _packs)
            {
                if (!_assignments.ContainsKey(p.Name))
                    _assignments[p.Name] = new List<BiomeType>(p.SuggestedBiomes);
            }
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Rescan")) Rescan();

            EditorGUILayout.LabelField($"Discovered {_packs.Length} packs under Assets/");
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var p in _packs)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(p.Name, EditorStyles.boldLabel);

                var assigned = _assignments[p.Name];
                var biomeValues = (BiomeType[])System.Enum.GetValues(typeof(BiomeType));
                foreach (var b in biomeValues)
                {
                    bool was = assigned.Contains(b);
                    bool now = EditorGUILayout.ToggleLeft(b.ToString(), was);
                    if (now != was)
                    {
                        if (now) assigned.Add(b); else assigned.Remove(b);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Save Catalog")) SaveCatalog();
        }

        private void SaveCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = CreateInstance<AssetPackBiomeCatalog>();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CatalogPath)!);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new List<AssetPackBiomeEntry>();
            foreach (var p in _packs)
            {
                var assigned = _assignments[p.Name];
                if (assigned.Count == 0) continue;
                var entry = new AssetPackBiomeEntry
                {
                    PackName = p.Name,
                    SuitableBiomes = assigned.ToArray(),
                };
                PackPrefabHarvester.Harvest(p.AbsolutePath, entry);
                entries.Add(entry);
            }
            catalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PackCategorization] Saved {entries.Count} categorized packs to {CatalogPath}");
        }
    }
}
