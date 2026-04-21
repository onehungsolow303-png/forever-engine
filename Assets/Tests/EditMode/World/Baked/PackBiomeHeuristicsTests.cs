using NUnit.Framework;
using ForeverEngine.Procedural.Editor;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World.Baked
{
    public class PackBiomeHeuristicsTests
    {
        [Test]
        public void Suggest_FromNordicName_ReturnsBorealPlusTaiga()
        {
            var biomes = PackBiomeHeuristics.SuggestBiomes("NatureManufacture_NordicForest");
            CollectionAssert.Contains(biomes, BiomeType.BorealForest);
            CollectionAssert.Contains(biomes, BiomeType.Taiga);
        }

        [Test]
        public void Suggest_FromDesertName_ReturnsDesertPlusArid()
        {
            var biomes = PackBiomeHeuristics.SuggestBiomes("AridDesertPack_Vol1");
            CollectionAssert.Contains(biomes, BiomeType.Desert);
            CollectionAssert.Contains(biomes, BiomeType.AridSteppe);
        }

        [Test]
        public void Suggest_FromTropicalRainforest_ReturnsRainforest()
        {
            var biomes = PackBiomeHeuristics.SuggestBiomes("TropicalRainforestSet");
            CollectionAssert.Contains(biomes, BiomeType.TropicalRainforest);
        }

        [Test]
        public void Suggest_FromUnknown_ReturnsEmpty()
        {
            var biomes = PackBiomeHeuristics.SuggestBiomes("RandomPackName42");
            Assert.AreEqual(0, biomes.Length);
        }
    }
}
