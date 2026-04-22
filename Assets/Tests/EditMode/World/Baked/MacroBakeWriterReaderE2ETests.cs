using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;
using ForeverEngine.Procedural.Editor;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Tests.World.Baked
{
    public class MacroBakeWriterReaderE2ETests
    {
        [Test]
        public void FullBake_FromSyntheticTerrain_ProducesReadableOutput()
        {
            // Unity Mono lacks .NET 7's Directory.CreateTempSubdirectory; follow the
            // Path.GetTempPath + Guid pattern used by AssetPackScannerTests (B3).
            var tmp = Path.Combine(Path.GetTempPath(), "e2e_bake_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);

            var catalog = ScriptableObject.CreateInstance<AssetPackBiomeCatalog>();
            catalog.Entries = Array.Empty<AssetPackBiomeEntry>();

            var terrainGO = SyntheticTerrainBuilder.Build(sizeMeters: 4096f, resolutionCells: 65);
            try
            {
                const float cellSizeMeters = 64f;
                int w = 64, h = 64;
                var heights = UnityTerrainSampler.SampleHeightmap(terrainGO.GetComponent<Terrain>(), w, h, 1000f);
                var biome = new byte[w * h];
                for (int i = 0; i < biome.Length; i++) biome[i] = (byte)BiomeType.Grassland;
                var splat = new byte[w * h * 4];
                var features = new byte[w * h];
                var props = Array.Empty<BakedPropPlacement>();

                var header = new BakedLayerHeader(
                    Magic: "FEW1", FormatVersion: BakedFormatConstants.FormatVersion,
                    LayerId: 0,
                    WorldMinX: 0f, WorldMinZ: 0f,
                    WorldMaxX: 4096f, WorldMaxZ: 4096f,
                    MacroCellSizeMeters: cellSizeMeters,
                    MacroWidthCells: w, MacroHeightCells: h,
                    BiomeTableChecksum: 0,
                    BakedAtUnixSeconds: 1776_000_000,
                    TileX: 0, TileZ: 0);

                BakedWorldWriter.WriteMacro(tmp, header, heights, biome, splat, features, props);

                var loaded = BakedWorldReader.LoadMacro(tmp);
                Assert.AreEqual(header, loaded.Header);
                Assert.AreEqual(w * h, loaded.Heightmap.Length);
                Assert.Greater(loaded.Heightmap[w * h - 1], loaded.Heightmap[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(terrainGO);
                if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
            }
        }
    }
}
