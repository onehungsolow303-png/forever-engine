using System;
using System.IO;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

// ============================================================================
// MIRROR COPY. Canonical version lives at:
//   C:/Dev/ForeverEngine.Core.Repo/ForeverEngine.Core.Tests/World/Baked/BakedTestFixtures.cs
// A second mirror exists at:
//   C:/Dev/ForeverEngine.Server.Repo/ForeverEngine.Server.Tests/BakedTestFixtures.cs
// Any change here MUST be applied to the other two copies. The duplication
// exists because the canonical copy is `internal` to a test-only project;
// a shared test-helpers project is deferred work (tracked in Phase 4A memory).
// ============================================================================

namespace ForeverEngine.Tests.EditMode.World.Baked
{
    /// <summary>
    /// Test fixture helpers for writing synthetic baked layers.
    /// </summary>
    public static class BakedTestFixtures
    {
        public struct TileSpec
        {
            public int TileX;
            public int TileZ;
            public float SignatureHeight;
            public byte BiomeByte;

            public TileSpec(int tileX, int tileZ, float signatureHeight, byte biomeByte)
            {
                TileX = tileX;
                TileZ = tileZ;
                SignatureHeight = signatureHeight;
                BiomeByte = biomeByte;
            }
        }

        /// <summary>
        /// Write a synthetic baked layer to a temporary directory and return the path.
        /// Creates index.json + tile subdirectories with macro data for each tile.
        /// All tiles have uniform height (SignatureHeight) and uniform biome (BiomeByte).
        /// </summary>
        public static string WriteSyntheticLayer(
            int layerId,
            float tileSize,
            float cellSize,
            float originX,
            float originZ,
            TileSpec[] tiles)
        {
            var layerDir = Path.Combine(Path.GetTempPath(), "baked_syn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(layerDir);

            // Infer grid bounds from tiles
            int minTileX = int.MaxValue, maxTileX = int.MinValue;
            int minTileZ = int.MaxValue, maxTileZ = int.MinValue;
            foreach (var t in tiles)
            {
                minTileX = Mathf.Min(minTileX, t.TileX);
                maxTileX = Mathf.Max(maxTileX, t.TileX);
                minTileZ = Mathf.Min(minTileZ, t.TileZ);
                maxTileZ = Mathf.Max(maxTileZ, t.TileZ);
            }

            // Compute cells per tile
            int cellsPerTile = (int)(tileSize / cellSize);
            int widthCells = cellsPerTile;
            int heightCells = cellsPerTile;

            // Write each tile
            var tileEntries = new BakedLayerTileEntry[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                var spec = tiles[i];
                string tileSubDir = $"tile_{spec.TileX}_{spec.TileZ}";
                string tilePath = Path.Combine(layerDir, tileSubDir);
                Directory.CreateDirectory(tilePath);

                // Create uniform height and biome arrays
                var heights = new float[widthCells * heightCells];
                var biome = new byte[widthCells * heightCells];
                var splat = new byte[widthCells * heightCells * 4];
                var features = new byte[widthCells * heightCells];

                for (int j = 0; j < widthCells * heightCells; j++)
                {
                    heights[j] = spec.SignatureHeight;
                    biome[j] = spec.BiomeByte;
                    // Splat: RGBA per cell, all zero for simplicity
                    splat[j * 4 + 0] = 0;
                    splat[j * 4 + 1] = 0;
                    splat[j * 4 + 2] = 0;
                    splat[j * 4 + 3] = 0;
                    features[j] = 0;
                }

                // Compute world bounds for this tile
                float worldMinX = originX + spec.TileX * tileSize;
                float worldMinZ = originZ + spec.TileZ * tileSize;
                float worldMaxX = worldMinX + tileSize;
                float worldMaxZ = worldMinZ + tileSize;

                var header = new BakedLayerHeader(
                    Magic: "FEW1",
                    FormatVersion: BakedFormatConstants.FormatVersion,
                    LayerId: (byte)layerId,
                    WorldMinX: worldMinX,
                    WorldMinZ: worldMinZ,
                    WorldMaxX: worldMaxX,
                    WorldMaxZ: worldMaxZ,
                    MacroCellSizeMeters: cellSize,
                    MacroWidthCells: widthCells,
                    MacroHeightCells: heightCells,
                    BiomeTableChecksum: 0,
                    BakedAtUnixSeconds: (long)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    TileX: spec.TileX,
                    TileZ: spec.TileZ);

                BakedWorldWriter.WriteMacro(
                    tilePath, header,
                    heights, biome, splat, features,
                    Array.Empty<BakedPropPlacement>());

                BakedWorldWriter.WriteHeroManifest(tilePath, Array.Empty<BakedHeroZone>());

                tileEntries[i] = new BakedLayerTileEntry(spec.TileX, spec.TileZ, tileSubDir);
            }

            // Write index.json
            var index = new BakedLayerIndex(
                LayerId: (byte)layerId,
                TileSize: tileSize,
                CellSize: cellSize,
                Origin: new BakedLayerOrigin(originX, originZ),
                Grid: new BakedLayerGrid(minTileX, minTileZ, maxTileX, maxTileZ),
                Tiles: tileEntries);

            BakedWorldWriter.WriteLayerIndex(layerDir, index);

            return layerDir;
        }
    }
}
