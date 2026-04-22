using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Editor commands for baking Unity Terrains into the shared baked-planet
    /// directory as macro-tier tiles. "Tile (Active Terrain)" bakes one Terrain
    /// and upserts its entry in layer_N/index.json. "All Tiles (Scene)" bakes
    /// every Terrain in the scene and rewrites index.json fresh.
    /// Grid-alignment is validated: a terrain's world position must equal
    /// origin + (tileX, tileZ) * tileSize to be accepted.
    /// </summary>
    public static class MacroBakeTool
    {
        private const string OutputRoot = "C:/Dev/.shared/baked/planet";
        private const float DefaultCellSizeMeters = 64f;
        private const float MaxHeightMeters = 1000f;
        private const int DefaultSeed = 0x0F03_EE3E;
        private const byte DefaultLayerId = 0;
        private const float GridAlignmentToleranceMeters = 0.01f;

        [MenuItem("Forever Engine/Bake/Tile (Active Terrain)")]
        public static void BakeTileFromActiveTerrain()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Macro Bake", "No active Terrain in scene.", "OK");
                return;
            }
            BakeTerrainsAsTiles(new[] { terrain }, fullRewrite: false, allowDialogs: true);
        }

        [MenuItem("Forever Engine/Bake/All Tiles (Scene)")]
        public static void BakeAllTilesInScene()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Macro Bake", "No Terrains found in scene.", "OK");
                return;
            }
            BakeTerrainsAsTiles(terrains, fullRewrite: true, allowDialogs: true);
        }

        /// <summary>
        /// Batchmode entry point. Throws InvalidOperationException on validation
        /// failure (catalog missing, size mismatch, grid misalignment) so callers
        /// like BatchBakeAll can fail loudly instead of swallowing the error.
        /// </summary>
        public static void BakeAllTilesInSceneOrThrow()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
                throw new System.InvalidOperationException("[MacroBake] No Terrains found in scene.");
            BakeTerrainsAsTiles(terrains, fullRewrite: true, allowDialogs: false);
        }

        private static void BakeTerrainsAsTiles(Terrain[] terrains, bool fullRewrite, bool allowDialogs = true)
        {
            var catalog = AssetPackBiomeCatalog.Load();
            if (catalog == null || catalog.Entries == null || catalog.Entries.Length == 0)
            {
                var errorMessage = "No AssetPackBiomeCatalog found. Run Forever Engine → Bake → Categorize Asset Packs first.";
                if (allowDialogs)
                {
                    EditorUtility.DisplayDialog("Macro Bake", errorMessage, "OK");
                    return;
                }
                throw new System.InvalidOperationException($"[MacroBake] {errorMessage}");
            }

            byte layerId = DefaultLayerId;
            float cellSizeMeters = DefaultCellSizeMeters;
            var layerDir = Path.Combine(OutputRoot, $"layer_{layerId}");

            BakedLayerIndex? existingIndex = null;
            if (!fullRewrite && File.Exists(Path.Combine(layerDir, "index.json")))
            {
                existingIndex = BakedWorldReader.LoadLayerIndex(layerDir);
            }

            Terrain anchor = PickAnchor(terrains);
            float tileSize = anchor.terrainData.size.x;

            float originX, originZ;
            if (existingIndex.HasValue)
            {
                originX = existingIndex.Value.Origin.X;
                originZ = existingIndex.Value.Origin.Z;
                if (!Mathf.Approximately(existingIndex.Value.TileSize, tileSize) ||
                    !Mathf.Approximately(existingIndex.Value.CellSize, cellSizeMeters))
                {
                    var errorMessage = $"Existing index.json tileSize/cellSize ({existingIndex.Value.TileSize}/{existingIndex.Value.CellSize}) " +
                        $"disagrees with terrain size/cell ({tileSize}/{cellSizeMeters}). " +
                        "Delete layer_0/ and re-run All Tiles to reset, or adjust the authoring.";
                    if (allowDialogs)
                    {
                        EditorUtility.DisplayDialog("Macro Bake", errorMessage, "OK");
                        return;
                    }
                    throw new System.InvalidOperationException($"[MacroBake] {errorMessage}");
                }
            }
            else
            {
                originX = anchor.transform.position.x;
                originZ = anchor.transform.position.z;
            }

            // Pre-validate grid alignment for all terrains before writing anything.
            foreach (var t in terrains)
            {
                if (!Mathf.Approximately(t.terrainData.size.x, tileSize) ||
                    !Mathf.Approximately(t.terrainData.size.z, tileSize))
                {
                    var errorMessage = $"Terrain '{t.name}' size {t.terrainData.size} != tile size {tileSize}. " +
                        "All tiles must be the same size.";
                    if (allowDialogs)
                    {
                        EditorUtility.DisplayDialog("Macro Bake", errorMessage, "OK");
                        return;
                    }
                    throw new System.InvalidOperationException($"[MacroBake] {errorMessage}");
                }
                float dx = (t.transform.position.x - originX) / tileSize;
                float dz = (t.transform.position.z - originZ) / tileSize;
                int tileX = Mathf.RoundToInt(dx);
                int tileZ = Mathf.RoundToInt(dz);
                if (Mathf.Abs(dx - tileX) * tileSize > GridAlignmentToleranceMeters ||
                    Mathf.Abs(dz - tileZ) * tileSize > GridAlignmentToleranceMeters)
                {
                    var errorMessage2 = $"Terrain '{t.name}' position {t.transform.position} is not aligned to origin " +
                        $"({originX},{originZ}) + (tileX,tileZ) * {tileSize}. Reposition or bake with " +
                        "'All Tiles (Scene)' which resets the origin.";
                    if (allowDialogs)
                    {
                        EditorUtility.DisplayDialog("Macro Bake", errorMessage2, "OK");
                        return;
                    }
                    throw new System.InvalidOperationException($"[MacroBake] {errorMessage2}");
                }
            }

            var tileEntries = new Dictionary<(int, int), BakedLayerTileEntry>();
            if (!fullRewrite && existingIndex.HasValue)
            {
                foreach (var e in existingIndex.Value.Tiles)
                    tileEntries[(e.TileX, e.TileZ)] = e;
            }

            foreach (var terrain in terrains)
            {
                int tileX = Mathf.RoundToInt((terrain.transform.position.x - originX) / tileSize);
                int tileZ = Mathf.RoundToInt((terrain.transform.position.z - originZ) / tileSize);
                BakeOneTerrainAsTile(terrain, layerId, layerDir, originX, originZ, tileSize, cellSizeMeters, tileX, tileZ, catalog);
                tileEntries[(tileX, tileZ)] = new BakedLayerTileEntry(tileX, tileZ, $"tile_{tileX}_{tileZ}");
            }

            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var e in tileEntries.Values)
            {
                if (e.TileX < minX) minX = e.TileX;
                if (e.TileZ < minZ) minZ = e.TileZ;
                if (e.TileX > maxX) maxX = e.TileX;
                if (e.TileZ > maxZ) maxZ = e.TileZ;
            }
            var entriesArray = new BakedLayerTileEntry[tileEntries.Count];
            tileEntries.Values.CopyTo(entriesArray, 0);

            var index = new BakedLayerIndex(
                LayerId: layerId,
                TileSize: tileSize,
                CellSize: cellSizeMeters,
                Origin: new BakedLayerOrigin(originX, originZ),
                Grid: new BakedLayerGrid(minX, minZ, maxX, maxZ),
                Tiles: entriesArray);

            Directory.CreateDirectory(layerDir);
            BakedWorldWriter.WriteLayerIndex(layerDir, index);

            Debug.Log($"[MacroBake] Wrote {entriesArray.Length} tile(s) + index.json to {layerDir} (origin=({originX:F1},{originZ:F1}))");
            if (allowDialogs)
            {
                EditorUtility.DisplayDialog("Macro Bake",
                    $"Baked {terrains.Length} terrain(s) into {entriesArray.Length} tile(s).\n" +
                    $"Origin locked to ({originX:F1}, {originZ:F1}). All future tiles in this " +
                    $"layer must align to origin + (tileX,tileZ) * {tileSize:F0}m.\n" +
                    $"Grid: ({minX},{minZ})-({maxX},{maxZ}). Layer dir: {layerDir}",
                    "OK");
            }
        }

        private static Terrain PickAnchor(Terrain[] terrains)
        {
            Terrain best = terrains[0];
            foreach (var t in terrains)
            {
                if (t.transform.position.x < best.transform.position.x ||
                    (Mathf.Approximately(t.transform.position.x, best.transform.position.x) &&
                     t.transform.position.z < best.transform.position.z))
                {
                    best = t;
                }
            }
            return best;
        }

        private static void BakeOneTerrainAsTile(
            Terrain terrain, byte layerId, string layerDir,
            float originX, float originZ, float tileSize, float cellSizeMeters,
            int tileX, int tileZ, AssetPackBiomeCatalog catalog)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(tileSize / cellSizeMeters));
            int h = w;
            float worldMinX = originX + tileX * tileSize;
            float worldMinZ = originZ + tileZ * tileSize;

            Debug.Log($"[MacroBake] Baking tile ({tileX},{tileZ}) size {tileSize}m, cells {w}x{h} @ {cellSizeMeters}m");

            var layers = terrain.terrainData.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
                Debug.Log($"[MacroBake]   Splat layer {i}: {(layers[i] != null ? layers[i].name : "<null>")}");

            var heights = UnityTerrainSampler.SampleHeightmap(terrain, w, h, MaxHeightMeters);
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < heights.Length; i++)
            { if (heights[i] < minH) minH = heights[i]; if (heights[i] > maxH) maxH = heights[i]; }
            Debug.Log($"[MacroBake]   Heightmap range: {minH:F1}m .. {maxH:F1}m");

            var splatLayerToBiome = new[] {
                BiomeType.Grassland, BiomeType.TemperateForest, BiomeType.Mountain, BiomeType.Desert
            };
            var biome = UnityTerrainSampler.SampleBiome(terrain, w, h, splatLayerToBiome);

            var biomeCounts = new int[14];
            for (int i = 0; i < biome.Length; i++) if (biome[i] < 14) biomeCounts[biome[i]]++;
            for (int b = 0; b < 14; b++)
                if (biomeCounts[b] > 0)
                    Debug.Log($"[MacroBake]   Biome {(BiomeType)b} ({b}): {biomeCounts[b]} cells");

            var splat = UnityTerrainSampler.SampleSplat(terrain, w, h);
            var features = new byte[w * h];

            var props = PropPlacementSampler.Sample(
                worldMinX: worldMinX, worldMinZ: worldMinZ,
                cellSizeMeters: cellSizeMeters,
                widthCells: w, heightCells: h,
                heightmap: heights, biome: biome,
                catalog: catalog,
                seed: DefaultSeed, layerId: layerId);

            var header = new BakedLayerHeader(
                Magic: "FEW1",
                FormatVersion: BakedFormatConstants.FormatVersion,
                LayerId: layerId,
                WorldMinX: worldMinX, WorldMinZ: worldMinZ,
                WorldMaxX: worldMinX + tileSize, WorldMaxZ: worldMinZ + tileSize,
                MacroCellSizeMeters: cellSizeMeters,
                MacroWidthCells: w, MacroHeightCells: h,
                BiomeTableChecksum: 0,
                BakedAtUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TileX: tileX, TileZ: tileZ);

            var tileDir = Path.Combine(layerDir, $"tile_{tileX}_{tileZ}");
            BakedWorldWriter.WriteMacro(tileDir, header, heights, biome, splat, features, props);
        }
    }
}
