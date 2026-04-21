using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World.Baked
{
    public class AssetPackBiomeCatalogTests
    {
        [Test]
        public void GetEntriesForBiome_ReturnsMatchingPacks()
        {
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[]
            {
                new AssetPackBiomeEntry {
                    PackName = "NordicForest",
                    SuitableBiomes = new[] { BiomeType.BorealForest, BiomeType.Taiga }
                },
                new AssetPackBiomeEntry {
                    PackName = "AridDesert",
                    SuitableBiomes = new[] { BiomeType.Desert, BiomeType.AridSteppe }
                },
            };

            var boreal = catalog.GetEntriesForBiome(BiomeType.BorealForest);
            Assert.AreEqual(1, boreal.Length);
            Assert.AreEqual("NordicForest", boreal[0].PackName);

            var unknown = catalog.GetEntriesForBiome(BiomeType.Ocean);
            Assert.AreEqual(0, unknown.Length);
        }
    }
}
