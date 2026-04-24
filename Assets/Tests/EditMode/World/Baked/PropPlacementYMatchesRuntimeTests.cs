using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;
using ForeverEngine.Procedural.Editor;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Tests.World.Baked
{
    // Invariant: every baked prop's WorldY equals what the runtime computes when it
    // asks for terrain Y at (WorldX, WorldZ). A drift of even a fraction of a meter
    // stacks across cells and produces the "floating props" bug.
    //
    // This is the T12 contract test after the 2026-04-23 architectural refactor.
    // Previous bug: PropPlacementSampler used its own cell-centered bilinear +
    // had no detail noise; BakedElevationSynth used corner-sampled bilinear + 3m
    // simplex detail. Props landed up to ~30m off rendered terrain.
    [TestFixture]
    public class PropPlacementYMatchesRuntimeTests
    {
        [Test]
        public void BakedPlacement_WorldY_MatchesBakedElevationSynthExactly()
        {
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[] {
                new AssetPackBiomeEntry {
                    PackName = "Test",
                    SuitableBiomes = new[] { BiomeType.Grassland },
                    TreePrefabs = new[] { new GameObject("Tree1") },
                    RockPrefabs = System.Array.Empty<GameObject>(),
                    BushPrefabs = System.Array.Empty<GameObject>(),
                    StructurePrefabs = System.Array.Empty<GameObject>(),
                },
            };

            int w = 16, h = 16;
            int cells = w * h;
            float cellSize = 64f;
            float worldMinX = 0f, worldMinZ = 0f;
            byte layerId = 0;

            // Non-trivial heightmap: varies enough to expose the half-cell-offset bug
            // if it ever regresses. Use a diagonal ramp so every cell has a different Y.
            var heights = new float[cells];
            var biome = new byte[cells];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    heights[z * w + x] = x * 20f + z * 15f;
                    biome[z * w + x] = (byte)BiomeType.Grassland;
                }

            var placements = PropPlacementSampler.Sample(
                worldMinX, worldMinZ, cellSize, w, h, heights, biome, catalog,
                seed: 12345, layerId: layerId);

            Assert.Greater(placements.Length, 10, "Test needs non-trivial placement count.");

            // Reconstruct the macro data the runtime will have on disk, then call
            // BakedElevationSynth.Sample at each prop's XZ and compare to WorldY.
            var header = new BakedLayerHeader(
                Magic: "FEW1",
                FormatVersion: BakedFormatConstants.FormatVersion,
                LayerId: layerId,
                WorldMinX: worldMinX, WorldMinZ: worldMinZ,
                WorldMaxX: worldMinX + w * cellSize,
                WorldMaxZ: worldMinZ + h * cellSize,
                MacroCellSizeMeters: cellSize,
                MacroWidthCells: w, MacroHeightCells: h,
                BiomeTableChecksum: 0, BakedAtUnixSeconds: 0, TileX: 0, TileZ: 0);
            var macro = new BakedMacroData(header, heights, biome,
                System.Array.Empty<byte>(), System.Array.Empty<byte>(),
                System.Array.Empty<BakedPropPlacement>());

            foreach (var p in placements)
            {
                float runtimeY = BakedElevationSynth.Sample(p.WorldX, p.WorldZ, macro, layerId);
                Assert.AreEqual(runtimeY, p.WorldY, 0.001f,
                    $"Prop at ({p.WorldX:F2},{p.WorldZ:F2}) has WorldY={p.WorldY:F3} but runtime " +
                    $"BakedElevationSynth.Sample returns {runtimeY:F3} (delta {p.WorldY - runtimeY:F3}m). " +
                    $"Bake and runtime samplers have drifted.");
            }
        }

        [Test]
        public void BakedPlacement_WorldY_UsesMacroOrigin()
        {
            // Same test as above but with a non-zero worldMin — catches any bug where
            // PropPlacementSampler accidentally passes localX/Z instead of worldX/Z
            // into BakedElevationSynth.
            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = new[] {
                new AssetPackBiomeEntry {
                    PackName = "Test",
                    SuitableBiomes = new[] { BiomeType.Grassland },
                    TreePrefabs = new[] { new GameObject("Tree2") },
                    RockPrefabs = System.Array.Empty<GameObject>(),
                    BushPrefabs = System.Array.Empty<GameObject>(),
                    StructurePrefabs = System.Array.Empty<GameObject>(),
                },
            };

            int w = 8, h = 8;
            int cells = w * h;
            float cellSize = 64f;
            float worldMinX = 1024f, worldMinZ = -2048f;
            byte layerId = 0;

            var heights = new float[cells];
            var biome = new byte[cells];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    heights[z * w + x] = 100f + x * 10f;
                    biome[z * w + x] = (byte)BiomeType.Grassland;
                }

            var placements = PropPlacementSampler.Sample(
                worldMinX, worldMinZ, cellSize, w, h, heights, biome, catalog,
                seed: 777, layerId: layerId);

            Assert.Greater(placements.Length, 0);

            var header = new BakedLayerHeader(
                Magic: "FEW1", FormatVersion: BakedFormatConstants.FormatVersion,
                LayerId: layerId,
                WorldMinX: worldMinX, WorldMinZ: worldMinZ,
                WorldMaxX: worldMinX + w * cellSize, WorldMaxZ: worldMinZ + h * cellSize,
                MacroCellSizeMeters: cellSize, MacroWidthCells: w, MacroHeightCells: h,
                BiomeTableChecksum: 0, BakedAtUnixSeconds: 0, TileX: 0, TileZ: 0);
            var macro = new BakedMacroData(header, heights, biome,
                System.Array.Empty<byte>(), System.Array.Empty<byte>(),
                System.Array.Empty<BakedPropPlacement>());

            foreach (var p in placements)
            {
                Assert.GreaterOrEqual(p.WorldX, worldMinX);
                Assert.GreaterOrEqual(p.WorldZ, worldMinZ);
                float runtimeY = BakedElevationSynth.Sample(p.WorldX, p.WorldZ, macro, layerId);
                Assert.AreEqual(runtimeY, p.WorldY, 0.001f,
                    "Prop WorldY must match runtime elevation at its world XZ, even with non-zero tile origin.");
            }
        }
    }
}
