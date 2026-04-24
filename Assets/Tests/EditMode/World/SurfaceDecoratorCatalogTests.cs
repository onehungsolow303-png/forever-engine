using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World
{
    // Verifies the fallback catalog path in SurfaceDecorator reads from
    // AssetPackBiomeCatalog (the single source of truth also consumed by the
    // baked pipeline via PropPlacementSampler + PopulatePrefabRegistry).
    [TestFixture]
    public class SurfaceDecoratorCatalogTests
    {
        [Test]
        public void PickPrefabFromEntry_TreeBiome_PrefersTreePrefabs()
        {
            var tree = new GameObject("TreeOnly");
            var entry = new AssetPackBiomeEntry
            {
                PackName = "TestPack",
                SuitableBiomes = new[] { BiomeType.TemperateForest },
                TreePrefabs = new[] { tree },
                RockPrefabs = System.Array.Empty<GameObject>(),
                BushPrefabs = System.Array.Empty<GameObject>(),
            };

            // Tree-weighted biome + only tree prefabs → must return the tree.
            var rng = new System.Random(1234);
            int treeHits = 0;
            for (int i = 0; i < 20; i++)
            {
                var picked = SurfaceDecorator.PickPrefabFromEntry(entry, BiomeType.TemperateForest, rng);
                if (picked == tree) treeHits++;
            }

            Assert.AreEqual(20, treeHits, "All picks must come from TreePrefabs when no other pools exist.");
            Object.DestroyImmediate(tree);
        }

        [Test]
        public void PickPrefabFromEntry_EmptyEntry_ReturnsNull()
        {
            var entry = new AssetPackBiomeEntry
            {
                PackName = "Empty",
                SuitableBiomes = new[] { BiomeType.Grassland },
                TreePrefabs = System.Array.Empty<GameObject>(),
                RockPrefabs = System.Array.Empty<GameObject>(),
                BushPrefabs = System.Array.Empty<GameObject>(),
            };
            var rng = new System.Random(42);
            Assert.IsNull(SurfaceDecorator.PickPrefabFromEntry(entry, BiomeType.Grassland, rng));
        }

        [Test]
        public void AssetPackBiomeCatalog_GetEntriesForBiome_FiltersOnSuitableBiomes()
        {
            var tree = new GameObject("Tree");
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[]
            {
                new AssetPackBiomeEntry
                {
                    PackName = "Forest",
                    SuitableBiomes = new[] { BiomeType.TemperateForest },
                    TreePrefabs = new[] { tree },
                },
                new AssetPackBiomeEntry
                {
                    PackName = "Desert",
                    SuitableBiomes = new[] { BiomeType.Desert },
                    RockPrefabs = new[] { tree },
                },
            };

            var forestEntries = catalog.GetEntriesForBiome(BiomeType.TemperateForest);
            Assert.AreEqual(1, forestEntries.Length);
            Assert.AreEqual("Forest", forestEntries[0].PackName);

            var tundraEntries = catalog.GetEntriesForBiome(BiomeType.Tundra);
            Assert.AreEqual(0, tundraEntries.Length);

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(tree);
        }
    }
}
