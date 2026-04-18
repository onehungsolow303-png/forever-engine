using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World
{
    public class BiomeMaterialCatalogTests
    {
        [Test]
        public void GetMaterial_NoEntries_ReturnsNull()
        {
            var catalog = ScriptableObject.CreateInstance<BiomeMaterialCatalog>();
            Assert.IsNull(catalog.GetMaterial(BiomeType.TemperateForest));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetMaterial_AssignedBiome_ReturnsMaterial()
        {
            var catalog = ScriptableObject.CreateInstance<BiomeMaterialCatalog>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            catalog.Entries = new[]
            {
                new BiomeMaterialEntry { Biome = BiomeType.TemperateForest, Material = mat },
            };
            Assert.AreSame(mat, catalog.GetMaterial(BiomeType.TemperateForest));
            Assert.IsNull(catalog.GetMaterial(BiomeType.Mountain));
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(mat);
        }
    }
}
