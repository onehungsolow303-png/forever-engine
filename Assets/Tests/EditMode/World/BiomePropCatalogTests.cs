using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World
{
    public class BiomePropCatalogTests
    {
        [Test]
        public void GetRules_NoEntries_ReturnsEmpty()
        {
            var catalog = ScriptableObject.CreateInstance<BiomePropCatalog>();
            var rules = catalog.GetRules(BiomeType.TemperateForest);
            Assert.IsNotNull(rules);
            Assert.AreEqual(0, rules.Length);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetRules_AssignedBiome_ReturnsMatchingRules()
        {
            var catalog = ScriptableObject.CreateInstance<BiomePropCatalog>();
            var prefab = new GameObject("FakePrefab");
            catalog.Rules = new[]
            {
                new BiomePropRule { Biome = BiomeType.TemperateForest, Prefabs = new[] { prefab }, Count = 5, MinSpacing = 10f, BaseScale = 2f },
                new BiomePropRule { Biome = BiomeType.Mountain, Prefabs = new[] { prefab }, Count = 3 },
            };
            var forestRules = catalog.GetRules(BiomeType.TemperateForest);
            Assert.AreEqual(1, forestRules.Length);
            Assert.AreEqual(5, forestRules[0].Count);

            var desertRules = catalog.GetRules(BiomeType.Desert);
            Assert.AreEqual(0, desertRules.Length);

            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(catalog);
        }
    }
}
