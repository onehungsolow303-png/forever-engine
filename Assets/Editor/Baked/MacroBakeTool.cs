using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Editor command that bakes the currently active Terrain into the shared
    /// baked-planet directory as a macro-tier layer (heightmap + biome + splat +
    /// features + props + metadata.json). User runs this once per authored
    /// Terrain; the result is consumed by the runtime baked-world loader.
    /// </summary>
    public static class MacroBakeTool
    {
        private const string OutputRoot = "C:/Dev/.shared/baked/planet";

        [MenuItem("Forever Engine/Bake/Macro (Active Terrain)")]
        public static void BakeActiveTerrain()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Macro Bake", "No active Terrain in scene.", "OK");
                return;
            }

            var catalog = AssetPackBiomeCatalog.Load();
            if (catalog == null || catalog.Entries == null || catalog.Entries.Length == 0)
            {
                EditorUtility.DisplayDialog("Macro Bake",
                    "No AssetPackBiomeCatalog found. Run Forever Engine → Bake → Categorize Asset Packs first.",
                    "OK");
                return;
            }

            byte layerId = 0;
            const float cellSizeMeters = 64f;
            const float maxHeightMeters = 1000f;
            const int seed = 0x0F03_EE3E;

            var tSize = terrain.terrainData.size;
            var tOrigin = terrain.transform.position;
            int w = Mathf.Max(1, Mathf.RoundToInt(tSize.x / cellSizeMeters));
            int h = Mathf.Max(1, Mathf.RoundToInt(tSize.z / cellSizeMeters));

            Debug.Log($"[MacroBake] Sampling {w}x{h} cells @ {cellSizeMeters}m from terrain size {tSize}");

            var layers = terrain.terrainData.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
                Debug.Log($"[MacroBake] Splat layer {i}: {(layers[i] != null ? layers[i].name : "<null>")}");

            var heights = UnityTerrainSampler.SampleHeightmap(terrain, w, h, maxHeightMeters);
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < heights.Length; i++) { if (heights[i] < minH) minH = heights[i]; if (heights[i] > maxH) maxH = heights[i]; }
            Debug.Log($"[MacroBake] Heightmap range: {minH:F1}m .. {maxH:F1}m");

            var splatLayerToBiome = new[] {
                BiomeType.Grassland, BiomeType.TemperateForest, BiomeType.Mountain, BiomeType.Desert
            };
            var biome = UnityTerrainSampler.SampleBiome(terrain, w, h, splatLayerToBiome);

            var biomeCounts = new int[14];
            for (int i = 0; i < biome.Length; i++) if (biome[i] < 14) biomeCounts[biome[i]]++;
            for (int b = 0; b < 14; b++)
                if (biomeCounts[b] > 0)
                    Debug.Log($"[MacroBake] Biome {(BiomeType)b} ({b}): {biomeCounts[b]} cells");
            var splat = UnityTerrainSampler.SampleSplat(terrain, w, h);
            var features = new byte[w * h];

            var props = PropPlacementSampler.Sample(
                worldMinX: tOrigin.x, worldMinZ: tOrigin.z,
                cellSizeMeters: cellSizeMeters,
                widthCells: w, heightCells: h,
                heightmap: heights, biome: biome,
                catalog: catalog,
                seed: seed, layerId: layerId);

            var header = new BakedLayerHeader(
                Magic: "FEW1", FormatVersion: 0x0002,
                LayerId: layerId,
                WorldMinX: tOrigin.x, WorldMinZ: tOrigin.z,
                WorldMaxX: tOrigin.x + tSize.x, WorldMaxZ: tOrigin.z + tSize.z,
                MacroCellSizeMeters: cellSizeMeters,
                MacroWidthCells: w, MacroHeightCells: h,
                BiomeTableChecksum: 0,
                BakedAtUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var layerDir = Path.Combine(OutputRoot, $"layer_{layerId}");
            BakedWorldWriter.WriteMacro(layerDir, header, heights, biome, splat, features, props);

            Debug.Log($"[MacroBake] Complete -> {layerDir} ({w}x{h} cells, {props.Length} props)");
            EditorUtility.DisplayDialog("Macro Bake",
                $"Wrote {w}x{h} cells, {props.Length} props to\n{layerDir}", "OK");
        }
    }
}
