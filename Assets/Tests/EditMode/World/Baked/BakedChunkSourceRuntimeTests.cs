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
                var heights = new float[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160 };
                var header = new BakedLayerHeader(
                    Magic: "FEW1", FormatVersion: 0x0002,
                    LayerId: 3,
                    WorldMinX: 0, WorldMinZ: 0,
                    WorldMaxX: w * 64f, WorldMaxZ: h * 64f,
                    MacroCellSizeMeters: 64f,
                    MacroWidthCells: w, MacroHeightCells: h,
                    BiomeTableChecksum: 0, BakedAtUnixSeconds: 0);
                BakedWorldWriter.WriteMacro(tmp, header,
                    heights, new byte[w*h], new byte[w*h*4], new byte[w*h],
                    System.Array.Empty<BakedPropPlacement>());
                BakedWorldWriter.WriteHeroManifest(tmp, System.Array.Empty<BakedHeroZone>());

                var coreSource = BakedChunkSource.Load(tmp, layerId: 3);
                BakedChunkSourceRuntime._SetForTest(coreSource);

                var points = new[] { (32f, 32f), (100f, 50f), (200f, 250f) };
                foreach (var (x, z) in points)
                {
                    float coreHeight = BakedElevationSynth.Sample(x, z, coreSource.Macro, 3);
                    float expectedNormalized = Mathf.Clamp01(coreHeight / TerrainGenerator.MaxHeight);

                    float actualNormalized = TerrainGenerator.SampleHeightAt(
                        x, z, BiomeType.Grassland, skeleton: null, worldSeed: 0);

                    Assert.AreEqual(expectedNormalized, actualNormalized, 1e-5f,
                        $"At ({x},{z}): core={coreHeight}m, expected norm={expectedNormalized}, got {actualNormalized}");
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
