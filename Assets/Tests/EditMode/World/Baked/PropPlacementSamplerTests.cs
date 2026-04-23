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

        [Test]
        public void Sample_SteepTerrain_SkipsCandidatesAboveMaxSlope()
        {
            // Build a very steep ramp: heights jump by ~100m per cell (far exceeds
            // the 45° max-slope threshold). Expected: most candidates are dropped.
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[] {
                new AssetPackBiomeEntry {
                    PackName = "Test",
                    SuitableBiomes = new[] { BiomeType.Mountain },
                    TreePrefabs = new[] { new GameObject("Tree1") },
                    RockPrefabs = new[] { new GameObject("Rock1") },
                    BushPrefabs = new GameObject[0],
                    StructurePrefabs = new GameObject[0],
                },
            };

            int w = 8, h = 8;
            int cells = w * h;
            var biome = new byte[cells];
            var heights = new float[cells];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    biome[z * w + x] = (byte)BiomeType.Mountain;
                    heights[z * w + x] = x * 200f; // cell size 64m → ~72° slope, filtered
                }

            var flatHeights = new float[cells];
            var flatBiome = new byte[cells];
            for (int i = 0; i < cells; i++) flatBiome[i] = (byte)BiomeType.Mountain;

            var steep = PropPlacementSampler.Sample(
                0f, 0f, 64f, w, h, heights, biome, catalog, seed: 7, layerId: 0);
            var flat = PropPlacementSampler.Sample(
                0f, 0f, 64f, w, h, flatHeights, flatBiome, catalog, seed: 7, layerId: 0);

            Assert.Less(steep.Length, flat.Length,
                "Steep terrain must produce fewer placements than flat terrain (slope filter).");
        }

        [Test]
        public void Sample_UsesBilinearYAtExactPoint_NotCellCenter()
        {
            // Heights vary sharply across cells (0, 100, 0, 100…).
            // Placements should land at interpolated Y values between the cell values,
            // not all at the cell-center height (which would produce a bimodal distribution).
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

            int w = 16, h = 16;
            int cells = w * h;
            var biome = new byte[cells];
            var heights = new float[cells];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    biome[z * w + x] = (byte)BiomeType.Grassland;
                    heights[z * w + x] = (x + z) % 2 == 0 ? 0f : 100f;
                }

            var placements = PropPlacementSampler.Sample(
                0f, 0f, 32f, w, h, heights, biome, catalog, seed: 42, layerId: 0);

            // With bilinear, at least some placements should have Y values NOT
            // equal to 0 or 100 (the raw cell values).
            bool anyInterpolated = false;
            foreach (var p in placements)
                if (p.WorldY > 1f && p.WorldY < 99f) { anyInterpolated = true; break; }

            Assert.IsTrue(anyInterpolated, "Bilinear sampling should produce interpolated Y values.");
        }
    }
}
