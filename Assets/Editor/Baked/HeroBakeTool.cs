using System.IO;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    public static class HeroBakeTool
    {
        private const string OutputRoot = "C:/Dev/.shared/baked/planet";

        [MenuItem("Forever Engine/Bake/Hero Zone (Active Terrain + Selected Zone Asset)")]
        public static void BakeSelectedZone()
        {
            var zoneAsset = Selection.activeObject as BakedHeroZoneAsset;
            if (zoneAsset == null)
            {
                EditorUtility.DisplayDialog("Hero Bake",
                    "Select a BakedHeroZoneAsset in the Project window first.", "OK");
                return;
            }
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Hero Bake", "No active Terrain in scene.", "OK");
                return;
            }
            var catalog = AssetPackBiomeCatalog.Load();
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Hero Bake", "No AssetPackBiomeCatalog.", "OK");
                return;
            }

            float resolution = Mathf.Max(0.5f, zoneAsset.ResolutionMeters);
            int w = Mathf.Max(1, Mathf.RoundToInt((zoneAsset.WorldMaxX - zoneAsset.WorldMinX) / resolution));
            int h = Mathf.Max(1, Mathf.RoundToInt((zoneAsset.WorldMaxZ - zoneAsset.WorldMinZ) / resolution));

            Debug.Log($"[HeroBake] Sampling {w}x{h} cells @ {resolution}m for zone '{zoneAsset.ZoneId}'");

            var heights = UnityTerrainSampler.SampleHeightmap(terrain, w, h, maxHeightMeters: 1000f);
            var splatLayerToBiome = new[] {
                BiomeType.Grassland, BiomeType.TemperateForest, BiomeType.Mountain, BiomeType.Desert
            };
            var biome = UnityTerrainSampler.SampleBiome(terrain, w, h, splatLayerToBiome);
            var splat = UnityTerrainSampler.SampleSplat(terrain, w, h);
            var features = new byte[w * h];

            var props = PropPlacementSampler.Sample(
                worldMinX: zoneAsset.WorldMinX, worldMinZ: zoneAsset.WorldMinZ,
                cellSizeMeters: resolution,
                widthCells: w, heightCells: h,
                heightmap: heights, biome: biome,
                catalog: catalog,
                seed: zoneAsset.ZoneId.GetHashCode(),
                layerId: zoneAsset.LayerId);

            var zone = new BakedHeroZone(
                Magic: "FEW1", FormatVersion: 0x0002,
                ZoneId: zoneAsset.ZoneId,
                LayerId: zoneAsset.LayerId,
                WorldMinX: zoneAsset.WorldMinX, WorldMinZ: zoneAsset.WorldMinZ,
                WorldMaxX: zoneAsset.WorldMaxX, WorldMaxZ: zoneAsset.WorldMaxZ,
                ResolutionMeters: resolution);

            var layerDir = Path.Combine(OutputRoot, $"layer_{zoneAsset.LayerId}");
            BakedWorldWriter.WriteHeroZone(layerDir, zone, heights, biome, splat, features, props);

            // Regenerate manifest from whatever's present on disk
            var existingZones = new System.Collections.Generic.List<BakedHeroZone>();
            var heroRoot = Path.Combine(layerDir, "hero");
            if (Directory.Exists(heroRoot))
            {
                var heroDirs = Directory.GetDirectories(heroRoot);
                System.Array.Sort(heroDirs, System.StringComparer.Ordinal);
                foreach (var dir in heroDirs)
                {
                    var metaPath = Path.Combine(dir, "metadata.json");
                    if (File.Exists(metaPath))
                    {
                        var json = File.ReadAllText(metaPath);
                        var z = System.Text.Json.JsonSerializer.Deserialize<BakedHeroZone>(json);
                        existingZones.Add(z);
                    }
                }
            }
            BakedWorldWriter.WriteHeroManifest(layerDir, existingZones.ToArray());

            Debug.Log($"[HeroBake] Complete -> {layerDir}/hero/{zoneAsset.ZoneId} ({w}x{h} cells, {props.Length} props)");
            EditorUtility.DisplayDialog("Hero Bake",
                $"Wrote zone '{zoneAsset.ZoneId}' ({w}x{h} cells, {props.Length} props).", "OK");
        }
    }
}
