using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;
using ForeverEngine.Procedural.Editor;

namespace ForeverEngine.Tests.World.Baked
{
    // Asserts the PopulatePrefabRegistry contract: every prefab GUID referenced
    // by AssetPackBiomeCatalog (Tree/Rock/Bush/Structure) ends up in the registry
    // entry list. This is the GUID-coverage guarantee called out in T3 — if this
    // ever regresses, the client will fail to resolve baked prop GUIDs at runtime
    // and world will render empty.
    [TestFixture]
    public class PopulatePrefabRegistryTests
    {
        private readonly List<string> _scratchAssets = new();

        [TearDown]
        public void CleanupScratchAssets()
        {
            foreach (var p in _scratchAssets)
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
            _scratchAssets.Clear();
            AssetDatabase.Refresh();
        }

        [Test]
        public void CollectEntries_EveryCatalogPrefabGuid_PresentInRegistry()
        {
            var tree = CreateScratchPrefab("T3_Tree");
            var rock = CreateScratchPrefab("T3_Rock");
            var bush = CreateScratchPrefab("T3_Bush");
            var structure = CreateScratchPrefab("T3_Struct");

            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[]
            {
                new AssetPackBiomeEntry
                {
                    PackName = "TestPack",
                    SuitableBiomes = new[] { BiomeType.TemperateForest },
                    TreePrefabs = new[] { tree },
                    RockPrefabs = new[] { rock },
                    BushPrefabs = new[] { bush },
                    StructurePrefabs = new[] { structure },
                }
            };

            var entries = PopulatePrefabRegistry.CollectEntries(catalog, blocklist: null, out int skipped);
            Assert.AreEqual(0, skipped);
            Assert.AreEqual(4, entries.Count, "All four prefab categories must produce registry entries.");

            foreach (var prefab in new[] { tree, rock, bush, structure })
            {
                var expectedGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
                Assert.IsTrue(
                    entries.Exists(e => e.Guid == expectedGuid && e.Prefab == prefab),
                    $"Registry missing GUID for {prefab.name} ({expectedGuid}).");
            }

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void CollectEntries_DuplicatesDeduped()
        {
            var tree = CreateScratchPrefab("T3_DupTree");

            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[]
            {
                new AssetPackBiomeEntry { PackName = "A", SuitableBiomes = new[] { BiomeType.TemperateForest }, TreePrefabs = new[] { tree } },
                new AssetPackBiomeEntry { PackName = "B", SuitableBiomes = new[] { BiomeType.BorealForest }, TreePrefabs = new[] { tree } },
            };

            var entries = PopulatePrefabRegistry.CollectEntries(catalog, blocklist: null, out _);
            Assert.AreEqual(1, entries.Count, "Same prefab across two entries must produce a single registry entry.");

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void CollectEntries_EmptyCatalog_ReturnsEmpty()
        {
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = System.Array.Empty<AssetPackBiomeEntry>();
            var entries = PopulatePrefabRegistry.CollectEntries(catalog, blocklist: null, out int skipped);
            Assert.AreEqual(0, entries.Count);
            Assert.AreEqual(0, skipped);
            Object.DestroyImmediate(catalog);
        }

        private GameObject CreateScratchPrefab(string name)
        {
            var folder = "Assets/BakedTestResources";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "BakedTestResources");

            var go = new GameObject(name);
            var path = $"{folder}/{name}_{System.Guid.NewGuid():N}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            _scratchAssets.Add(path);
            return prefab;
        }
    }
}
