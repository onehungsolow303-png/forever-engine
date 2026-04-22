using System.IO;
using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Tests.World.Baked
{
    public class BakedChunkSourceRuntimeTests
    {
        [Test]
        public void Client_Sampling_MatchesCoreSampling_ForSameInput()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "baked_rt_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                int w = 4, h = 4;
                float tileSize = w * 64f; // 256m
                var heights = new float[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160 };
                var header = new BakedLayerHeader(
                    Magic: "FEW1", FormatVersion: BakedFormatConstants.FormatVersion,
                    LayerId: 3,
                    WorldMinX: 0, WorldMinZ: 0,
                    WorldMaxX: tileSize, WorldMaxZ: tileSize,
                    MacroCellSizeMeters: 64f,
                    MacroWidthCells: w, MacroHeightCells: h,
                    BiomeTableChecksum: 0, BakedAtUnixSeconds: 0,
                    TileX: 0, TileZ: 0);

                // Write tile data into tile_0_0 subdirectory (multi-tile layout).
                var tileDir = Path.Combine(tmp, "tile_0_0");
                BakedWorldWriter.WriteMacro(tileDir, header,
                    heights, new byte[w*h], new byte[w*h*4], new byte[w*h],
                    System.Array.Empty<BakedPropPlacement>());
                BakedWorldWriter.WriteHeroManifest(tileDir, System.Array.Empty<BakedHeroZone>());

                // Write the index.json so BakedChunkSource.Load finds the tile.
                var index = new BakedLayerIndex(
                    LayerId: 3,
                    TileSize: tileSize,
                    CellSize: 64f,
                    Origin: new BakedLayerOrigin(0f, 0f),
                    Grid: new BakedLayerGrid(0, 0, 0, 0),
                    Tiles: new[] { new BakedLayerTileEntry(0, 0, "tile_0_0") });
                BakedWorldWriter.WriteLayerIndex(tmp, index);

                var coreSource = BakedChunkSource.Load(tmp, layerId: 3);
                BakedChunkSourceRuntime._SetForTest(coreSource);

                var points = new[] { (32f, 32f), (100f, 50f), (200f, 250f) };
                foreach (var (x, z) in points)
                {
                    // Use TryGetTile(0, 0) to get the single tile for comparison.
                    Assert.IsTrue(coreSource.TryGetTile(0, 0, out var macroTile),
                        "Expected tile (0,0) to be present in loaded source");
                    float coreHeight = BakedElevationSynth.Sample(x, z, macroTile, 3);

                    float actualMeters = TerrainGenerator.SampleHeightAt(
                        x, z, BiomeType.Grassland, skeleton: null, worldSeed: 0);

                    Assert.AreEqual(coreHeight, actualMeters, 1e-3f,
                        $"At ({x},{z}): core={coreHeight}m, got {actualMeters}m");
                }
            }
            finally
            {
                BakedChunkSourceRuntime._Reset();
                Directory.Delete(tmp, recursive: true);
            }
        }
    }
}
