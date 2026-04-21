using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Deterministic prop placer for the macro bake. Each cell may contribute
    /// zero or more placements, driven by the biome at that cell + the catalog's
    /// available packs for that biome + a seeded RNG keyed by (layerId, cellX, cellZ, seed).
    /// </summary>
    public static class PropPlacementSampler
    {
        private static readonly Dictionary<BiomeType, int> PropsPerKm2 = new()
        {
            { BiomeType.Grassland, 30 },
            { BiomeType.TemperateForest, 200 },
            { BiomeType.BorealForest, 180 },
            { BiomeType.Taiga, 150 },
            { BiomeType.TropicalRainforest, 280 },
            { BiomeType.Savanna, 40 },
            { BiomeType.Desert, 15 },
            { BiomeType.AridSteppe, 25 },
            { BiomeType.Tundra, 10 },
            { BiomeType.Mountain, 60 },
            { BiomeType.Beach, 5 },
            { BiomeType.Ocean, 0 },
            { BiomeType.IceSheet, 0 },
        };

        public static BakedPropPlacement[] Sample(
            float worldMinX, float worldMinZ,
            float cellSizeMeters, int widthCells, int heightCells,
            float[] heightmap, byte[] biome,
            AssetPackBiomeCatalog catalog,
            int seed, byte layerId)
        {
            var placements = new List<BakedPropPlacement>();

            for (int z = 0; z < heightCells; z++)
            {
                for (int x = 0; x < widthCells; x++)
                {
                    int idx = z * widthCells + x;
                    var b = (BiomeType)biome[idx];
                    if (!PropsPerKm2.TryGetValue(b, out var density) || density == 0) continue;

                    var entries = catalog.GetEntriesForBiome(b);
                    if (entries.Length == 0) continue;

                    float cellAreaKm2 = (cellSizeMeters * cellSizeMeters) / 1_000_000f;
                    float expected = density * cellAreaKm2;

                    int cellSeed = Hash3(layerId, x, z, seed);
                    var rng = new System.Random(cellSeed);

                    int count = (int)expected;
                    if (rng.NextDouble() < (expected - count)) count++;

                    for (int i = 0; i < count; i++)
                    {
                        var entry = entries[rng.Next(entries.Length)];
                        var prefab = PickPrefabForBiome(entry, b, rng);
                        if (prefab == null) continue;

                        float cellOffX = (float)rng.NextDouble() * cellSizeMeters;
                        float cellOffZ = (float)rng.NextDouble() * cellSizeMeters;
                        float wx = worldMinX + x * cellSizeMeters + cellOffX;
                        float wz = worldMinZ + z * cellSizeMeters + cellOffZ;
                        float wy = heightmap[idx];
                        float yaw = (float)rng.NextDouble() * 360f;
                        float scale = 0.9f + (float)rng.NextDouble() * 0.2f;

                        string assetPath = AssetDatabase.GetAssetPath(prefab);
                        string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                        placements.Add(new BakedPropPlacement(
                            PrefabGuid: assetGuid,
                            PrefabResourcePath: assetPath,
                            WorldX: wx, WorldY: wy, WorldZ: wz,
                            YawDegrees: yaw,
                            UniformScale: scale));
                    }
                }
            }
            return placements.ToArray();
        }

        private static GameObject PickPrefabForBiome(AssetPackBiomeEntry entry, BiomeType biome, System.Random rng)
        {
            float[] weights = biome switch
            {
                BiomeType.TemperateForest or BiomeType.BorealForest or BiomeType.Taiga or BiomeType.TropicalRainforest
                    => new[] { 0.80f, 0.10f, 0.10f },
                BiomeType.Desert or BiomeType.AridSteppe
                    => new[] { 0.20f, 0.60f, 0.20f },
                BiomeType.Mountain
                    => new[] { 0.10f, 0.80f, 0.10f },
                _ => new[] { 0.40f, 0.30f, 0.30f },
            };
            double r = rng.NextDouble();
            GameObject[] pool;
            if (r < weights[0] && entry.TreePrefabs != null && entry.TreePrefabs.Length > 0)
                pool = entry.TreePrefabs;
            else if (r < weights[0] + weights[1] && entry.RockPrefabs != null && entry.RockPrefabs.Length > 0)
                pool = entry.RockPrefabs;
            else if (entry.BushPrefabs != null && entry.BushPrefabs.Length > 0)
                pool = entry.BushPrefabs;
            else
                return null;
            return pool[rng.Next(pool.Length)];
        }

        private static int Hash3(byte layer, int x, int z, int seed)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + layer;
                h = h * 31 + x;
                h = h * 31 + z;
                h = h * 31 + seed;
                return h;
            }
        }
    }
}
