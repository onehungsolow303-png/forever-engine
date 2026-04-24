using NUnit.Framework;
using ForeverEngine.Procedural.Editor;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World.Baked
{
    [TestFixture]
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

        [Test]
        public void Classify_NatureManufactureMountain_IsOutdoorMountainBiomes()
        {
            var r = PackBiomeHeuristics.Classify("NatureManufacture Assets");
            Assert.That(r.Role, Is.EqualTo(PackRole.OutdoorBiomeContent));
            CollectionAssert.Contains(r.SuggestedBiomes, BiomeType.Mountain);
            // AlpineMeadow is a Gaia spawner preset, not an engine BiomeType —
            // closest analog for NatureManufacture is Taiga (cold-temperate forest).
            CollectionAssert.Contains(r.SuggestedBiomes, BiomeType.Taiga);
        }

        [Test]
        public void Classify_Lordenfel_IsIndoorExcluded()
        {
            var r = PackBiomeHeuristics.Classify("Lordenfel");
            Assert.That(r.Role, Is.EqualTo(PackRole.IndoorExcluded));
            Assert.That(r.SuggestedBiomes.Length, Is.EqualTo(0));
        }

        [Test]
        public void Classify_ThreeDForge_IsIndoorExcluded()
        {
            var r = PackBiomeHeuristics.Classify("3DForge");
            Assert.That(r.Role, Is.EqualTo(PackRole.IndoorExcluded));
        }

        [Test]
        public void Classify_MegaStampBundle_IsStamperOnly()
        {
            var r = PackBiomeHeuristics.Classify("Procedural Worlds Mega Stamp Bundle");
            Assert.That(r.Role, Is.EqualTo(PackRole.StamperOnly));
            Assert.That(r.SuggestedBiomes.Length, Is.EqualTo(0));
        }

        [Test]
        public void Classify_EternalTemple_IsIndoorExcluded()
        {
            var r = PackBiomeHeuristics.Classify("Eternal Temple");
            Assert.That(r.Role, Is.EqualTo(PackRole.IndoorExcluded));
            Assert.That(r.SuggestedBiomes.Length, Is.EqualTo(0));
        }

        [Test]
        public void Classify_EternalTempleNoSpace_IsIndoorExcluded()
        {
            var r = PackBiomeHeuristics.Classify("EternalTemple");
            Assert.That(r.Role, Is.EqualTo(PackRole.IndoorExcluded));
        }

        [Test]
        public void Classify_MagicPig_IsCreatures()
        {
            var r = PackBiomeHeuristics.Classify("Magic Pig Games (Infinity PBR)");
            Assert.That(r.Role, Is.EqualTo(PackRole.Creatures));
            Assert.That(r.SuggestedBiomes.Length, Is.EqualTo(0));
        }

        [Test]
        public void Classify_GeneratedModels_IsCreatures()
        {
            var r = PackBiomeHeuristics.Classify("GeneratedModels");
            Assert.That(r.Role, Is.EqualTo(PackRole.Creatures));
        }

        [Test]
        public void Classify_GaiaUserData_IsStamperOnly()
        {
            var r = PackBiomeHeuristics.Classify("Gaia User Data");
            Assert.That(r.Role, Is.EqualTo(PackRole.StamperOnly));
        }

        [Test]
        public void Classify_ProceduralWorlds_IsStamperOnly()
        {
            var r = PackBiomeHeuristics.Classify("Procedural Worlds");
            Assert.That(r.Role, Is.EqualTo(PackRole.StamperOnly));
        }

        [Test]
        public void Classify_UnknownPack_DefaultsToUnknown()
        {
            var r = PackBiomeHeuristics.Classify("Some-Random-Pack-We-Havent-Seen");
            Assert.That(r.Role, Is.EqualTo(PackRole.Unknown));
        }

        [Test]
        public void Classify_DungeonArchitect_IsTool()
        {
            var r = PackBiomeHeuristics.Classify("CodeRespawn DungeonArchitect");
            Assert.That(r.Role, Is.EqualTo(PackRole.Tool));
        }

        [Test]
        public void Classify_Hivemind_IsTool()
        {
            var r = PackBiomeHeuristics.Classify("Hivemind");
            Assert.That(r.Role, Is.EqualTo(PackRole.Tool));
        }

        [Test]
        public void Classify_NatureManufacture_HasBroadBiomeCoverage()
        {
            // NatureManufacture ships Meadow/Winter Forest/Summer Forest/Mountain —
            // the one outdoor pack should cover the common biomes so at least
            // one matches whatever tile biome the sampler picks.
            var r = PackBiomeHeuristics.Classify("NatureManufacture Assets");
            Assert.That(r.Role, Is.EqualTo(PackRole.OutdoorBiomeContent));
            CollectionAssert.Contains(r.SuggestedBiomes, BiomeType.Grassland);
            CollectionAssert.Contains(r.SuggestedBiomes, BiomeType.TemperateForest);
            CollectionAssert.Contains(r.SuggestedBiomes, BiomeType.BorealForest);
        }
    }
}
