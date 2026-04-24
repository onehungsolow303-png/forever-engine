using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    public class AssetPackCategorizationWindow : EditorWindow
    {
        private DiscoveredPack[] _packs;
        private Dictionary<string, List<BiomeType>> _assignments = new();
        private Dictionary<string, ClassificationResult> _classifications = new();
        private Vector2 _scroll;
        private const string CatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";

        [MenuItem("Forever Engine/Bake/Categorize Asset Packs")]
        public static void Open() => GetWindow<AssetPackCategorizationWindow>("Pack Categorization");

        private void OnEnable() => Rescan();

        private void Rescan()
        {
            _packs = AssetPackScanner.ScanRoot(Application.dataPath);
            _assignments.Clear();
            _classifications.Clear();

            foreach (var p in _packs)
                _classifications[p.Name] = PackBiomeHeuristics.Classify(p.Name);

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

        // Discard any user-edited assignments and re-apply heuristic suggestions.
        // Safe to run on re-import of new packs — leaves existing catalog unchanged
        // until Save is pressed.
        private void ResetToHeuristicDefaults()
        {
            foreach (var p in _packs)
            {
                var classification = _classifications[p.Name];
                _assignments[p.Name] = new List<BiomeType>(classification.SuggestedBiomes);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(80))) Rescan();
            if (GUILayout.Button("Reset All to Heuristic Defaults", EditorStyles.toolbarButton, GUILayout.Width(220)))
                ResetToHeuristicDefaults();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Discovered {_packs.Length} packs under Assets/");
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var p in _packs)
            {
                var classification = _classifications[p.Name];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(p.Name, EditorStyles.boldLabel);
                DrawRoleLabel(classification.Role);

                // Packs with no biome assignment path get no toggles — they're either
                // excluded entirely or consumed by a different pipeline (Stamper, Tool).
                if (classification.Role == PackRole.OutdoorBiomeContent ||
                    classification.Role == PackRole.Unknown)
                {
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
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Save Catalog")) SaveCatalog();
        }

        private static void DrawRoleLabel(PackRole role)
        {
            var prev = GUI.color;
            string text;
            switch (role)
            {
                case PackRole.OutdoorBiomeContent:
                    GUI.color = new Color(0.6f, 1f, 0.6f);
                    text = "Outdoor biome content";
                    break;
                case PackRole.IndoorExcluded:
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    text = "EXCLUDED — DungeonArchitect-scope, not outdoor biomes";
                    break;
                case PackRole.StamperOnly:
                    GUI.color = new Color(1f, 0.95f, 0.5f);
                    text = "Stamper-only — no prop spawning";
                    break;
                case PackRole.Tool:
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    text = "Tool (not content)";
                    break;
                case PackRole.Creatures:
                    GUI.color = new Color(1f, 0.7f, 1f);
                    text = "Creatures — monsters/NPCs, not world props";
                    break;
                default:
                    text = "Unknown — please curate manually";
                    break;
            }
            EditorGUILayout.LabelField(text);
            GUI.color = prev;
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

            int skippedByRole = 0;
            var entries = new List<AssetPackBiomeEntry>();
            foreach (var p in _packs)
            {
                var classification = _classifications[p.Name];
                if (classification.Role == PackRole.IndoorExcluded ||
                    classification.Role == PackRole.StamperOnly ||
                    classification.Role == PackRole.Tool ||
                    classification.Role == PackRole.Creatures)
                {
                    skippedByRole++;
                    continue;
                }

                var assigned = _assignments[p.Name];
                if (assigned.Count == 0)
                {
                    Debug.LogWarning($"[PackCategorization] Pack '{p.Name}' has no biomes assigned — skipping.");
                    continue;
                }

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
            Debug.Log($"[PackCategorization] Saved {entries.Count} categorized packs to {CatalogPath}. Skipped {skippedByRole} by role (Indoor/Stamper/Tool).");
        }
    }
}
