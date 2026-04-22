using NUnit.Framework;
using ForeverEngine.Procedural;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Tests.World.Baked
{
    /// <summary>
    /// Anchor tests that pin BakedBiomeLookup.Names directly to the BiomeType enum.
    ///
    /// These live in Unity EditMode (not Core.Tests) because the BiomeType enum is
    /// defined in the Unity Assembly-CSharp (namespace ForeverEngine.Procedural),
    /// and Core has no reference to it — Core only stores the byte-id.
    ///
    /// Any reorder of BiomeType or of BakedBiomeLookup.Names breaks these tests
    /// immediately, instead of silently shifting lookups the way the pre-dffe84e
    /// drift did (Names[5] = "ice_sheet" while (byte)BiomeType.Grassland == 5,
    /// producing wrong ground colors in playtest). The hard-coded [InlineData(5,
    /// "ice_sheet")] tests in Core.Tests still pass against the wrong lookup; THESE
    /// tests fail because they reference the enum directly.
    ///
    /// See feedback_baked_lookup_enum_drift.md for the incident.
    /// </summary>
    public class BakedBiomeLookupTests
    {
        [Test]
        public void Anchor_BiomeType_Ocean_MapsToOcean()
        {
            Assert.AreEqual("ocean", BakedBiomeLookup.Name((byte)BiomeType.Ocean));
        }

        [Test]
        public void Anchor_BiomeType_Beach_MapsToBeach()
        {
            Assert.AreEqual("beach", BakedBiomeLookup.Name((byte)BiomeType.Beach));
        }

        [Test]
        public void Anchor_BiomeType_Desert_MapsToDesert()
        {
            Assert.AreEqual("desert", BakedBiomeLookup.Name((byte)BiomeType.Desert));
        }

        [Test]
        public void Anchor_BiomeType_AridSteppe_MapsToAridSteppe()
        {
            Assert.AreEqual("arid_steppe", BakedBiomeLookup.Name((byte)BiomeType.AridSteppe));
        }

        [Test]
        public void Anchor_BiomeType_Savanna_MapsToSavanna()
        {
            Assert.AreEqual("savanna", BakedBiomeLookup.Name((byte)BiomeType.Savanna));
        }

        [Test]
        public void Anchor_BiomeType_Grassland_MapsToGrassland()
        {
            Assert.AreEqual("grassland", BakedBiomeLookup.Name((byte)BiomeType.Grassland));
        }

        [Test]
        public void Anchor_BiomeType_TemperateForest_MapsToTemperateForest()
        {
            Assert.AreEqual("temperate_forest", BakedBiomeLookup.Name((byte)BiomeType.TemperateForest));
        }

        [Test]
        public void Anchor_BiomeType_TropicalRainforest_MapsToTropicalRainforest()
        {
            Assert.AreEqual("tropical_rainforest", BakedBiomeLookup.Name((byte)BiomeType.TropicalRainforest));
        }

        [Test]
        public void Anchor_BiomeType_BorealForest_MapsToBorealForest()
        {
            Assert.AreEqual("boreal_forest", BakedBiomeLookup.Name((byte)BiomeType.BorealForest));
        }

        [Test]
        public void Anchor_BiomeType_Taiga_MapsToTaiga()
        {
            Assert.AreEqual("taiga", BakedBiomeLookup.Name((byte)BiomeType.Taiga));
        }

        [Test]
        public void Anchor_BiomeType_Tundra_MapsToTundra()
        {
            Assert.AreEqual("tundra", BakedBiomeLookup.Name((byte)BiomeType.Tundra));
        }

        [Test]
        public void Anchor_BiomeType_IceSheet_MapsToIceSheet()
        {
            Assert.AreEqual("ice_sheet", BakedBiomeLookup.Name((byte)BiomeType.IceSheet));
        }

        [Test]
        public void Anchor_BiomeType_Mountain_MapsToMountain()
        {
            Assert.AreEqual("mountain", BakedBiomeLookup.Name((byte)BiomeType.Mountain));
        }

        [Test]
        public void Anchor_BiomeType_River_MapsToRiver()
        {
            Assert.AreEqual("river", BakedBiomeLookup.Name((byte)BiomeType.River));
        }
    }
}
