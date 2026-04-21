using NUnit.Framework;
using ForeverEngine.Procedural.Editor;

namespace ForeverEngine.Tests.World.Baked
{
    public class PackPrefabHarvesterTests
    {
        [Test]
        public void ClassifyPrefabName_Tree_ReturnsTreeCategory()
        {
            Assert.AreEqual(PrefabCategory.Tree, PackPrefabHarvester.Classify("Pine_Tree_01.prefab"));
            Assert.AreEqual(PrefabCategory.Tree, PackPrefabHarvester.Classify("BirchTreeLarge.prefab"));
        }

        [Test]
        public void ClassifyPrefabName_Rock_ReturnsRockCategory()
        {
            Assert.AreEqual(PrefabCategory.Rock, PackPrefabHarvester.Classify("Rock_Mossy_A.prefab"));
            Assert.AreEqual(PrefabCategory.Rock, PackPrefabHarvester.Classify("BoulderGroup.prefab"));
        }

        [Test]
        public void ClassifyPrefabName_Bush_ReturnsBushCategory()
        {
            Assert.AreEqual(PrefabCategory.Bush, PackPrefabHarvester.Classify("Bush_Fern.prefab"));
            Assert.AreEqual(PrefabCategory.Bush, PackPrefabHarvester.Classify("Shrub_A.prefab"));
        }

        [Test]
        public void ClassifyPrefabName_Unknown_ReturnsStructure()
        {
            Assert.AreEqual(PrefabCategory.Structure, PackPrefabHarvester.Classify("Building_Inn.prefab"));
        }
    }
}
