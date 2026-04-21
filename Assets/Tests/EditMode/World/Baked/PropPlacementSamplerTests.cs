using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;
using ForeverEngine.Procedural.Editor;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Tests.World.Baked
{
    public class PropPlacementSamplerTests
    {
        [Test]
        public void Sample_IsDeterministic_ForSameSeed()
        {
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[] {
                new AssetPackBiomeEntry {
                    PackName = "Test",
                    SuitableBiomes = new[] { BiomeType.Grassland },
                    TreePrefabs = new[] { new GameObject("Tree1") },
                    RockPrefabs = new GameObject[0],
                    BushPrefabs = new GameObject[0],
                    StructurePrefabs = new GameObject[0],
                },
            };

            // 8x8 cell area at 64m = 512m x 512m, all grassland, height 0
            int cells = 64;
            var biome = new byte[cells];
            var heights = new float[cells];
            for (int i = 0; i < cells; i++) biome[i] = (byte)BiomeType.Grassland;

            var placements1 = PropPlacementSampler.Sample(
                worldMinX: 0f, worldMinZ: 0f,
                cellSizeMeters: 64f, widthCells: 8, heightCells: 8,
                heightmap: heights, biome: biome,
                catalog: catalog, seed: 12345, layerId: 0);
            var placements2 = PropPlacementSampler.Sample(
                worldMinX: 0f, worldMinZ: 0f,
                cellSizeMeters: 64f, widthCells: 8, heightCells: 8,
                heightmap: heights, biome: biome,
                catalog: catalog, seed: 12345, layerId: 0);

            Assert.AreEqual(placements1.Length, placements2.Length);
            for (int i = 0; i < placements1.Length; i++)
                Assert.AreEqual(placements1[i], placements2[i]);
        }

        [Test]
        public void Sample_EmptyBiome_ReturnsNoPlacements()
        {
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = System.Array.Empty<AssetPackBiomeEntry>();
            var biome = new byte[64];
            var heights = new float[64];
            var placements = PropPlacementSampler.Sample(
                0, 0, 64, 8, 8, heights, biome, catalog, seed: 1, layerId: 0);
            Assert.AreEqual(0, placements.Length);
        }
    }
}
