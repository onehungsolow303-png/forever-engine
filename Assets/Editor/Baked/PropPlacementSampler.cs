using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Deterministic prop placer for the macro bake with slope/height fitness filtering.
    ///
    /// For each cell a density-driven count of candidates is generated. Each candidate:
    ///   1. Random (offsetX, offsetZ) within the cell
    ///   2. Y sampled via bilinear interpolation of the tile heightmap at exact world (x,z)
    ///      — NOT the cell-center height, which misses fine-scale terrain variation
    ///   3. Slope computed via central-difference gradient on the heightmap
    ///   4. Prefab category chosen by slope band (trees on flat, rocks on moderate)
    ///      — a candidate on slope > 45° is dropped entirely, no prefab can stand vertically
    ///        there without pivot visibly clipping the surface
    ///   5. Per-biome category weights multiplex onto the slope weights
    ///
    /// BakedPropPlacement has only Yaw (no pitch/roll), so all props are placed vertical.
    /// Slope filtering keeps placements on terrain where vertical orientation is visually
    /// acceptable. If pitch/roll support is added to the wire protocol later, this can be
    /// extended with align-to-normal for rocks.
    /// </summary>
    public static class PropPlacementSampler
    {
        // Props per km². Bumped ~4× from prior revision (2026-04-23 playtest showed
        // 1 tile at old densities was visibly barren). Stay below 1000/km² to avoid
        // the prop-Instantiate-on-main-thread spikes documented in
        // feedback_prop_density_performance_budget.md (tanked playtest to 3 FPS twice).
        private static readonly Dictionary<BiomeType, int> PropsPerKm2 = new()
        {
            { BiomeType.Grassland, 120 },
            { BiomeType.TemperateForest, 800 },
            { BiomeType.BorealForest, 720 },
            { BiomeType.Taiga, 600 },
            { BiomeType.TropicalRainforest, 900 },
            { BiomeType.Savanna, 160 },
            { BiomeType.Desert, 60 },
            { BiomeType.AridSteppe, 100 },
            { BiomeType.Tundra, 40 },
            { BiomeType.Mountain, 240 },
            { BiomeType.Beach, 20 },
            { BiomeType.Ocean, 0 },
            { BiomeType.IceSheet, 0 },
        };

        private const float MaxSlopeDegrees = 45f;

        public static BakedPropPlacement[] Sample(
            float worldMinX, float worldMinZ,
            float cellSizeMeters, int widthCells, int heightCells,
            float[] heightmap, byte[] biome,
            AssetPackBiomeCatalog catalog,
            int seed, byte layerId)
        {
            var placements = new List<BakedPropPlacement>();
            float tileSizeX = widthCells * cellSizeMeters;
            float tileSizeZ = heightCells * cellSizeMeters;

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

                        float cellOffX = (float)rng.NextDouble() * cellSizeMeters;
                        float cellOffZ = (float)rng.NextDouble() * cellSizeMeters;
                        float localX = x * cellSizeMeters + cellOffX;
                        float localZ = z * cellSizeMeters + cellOffZ;
                        float wx = worldMinX + localX;
                        float wz = worldMinZ + localZ;

                        float wy = SampleBilinear(heightmap, widthCells, heightCells, cellSizeMeters, localX, localZ);
                        float slopeDeg = ComputeSlopeDegrees(heightmap, widthCells, heightCells, cellSizeMeters, localX, localZ);

                        if (slopeDeg > MaxSlopeDegrees) continue;

                        var prefab = PickPrefabForSlopeAndBiome(entry, b, slopeDeg, rng);
                        if (prefab == null) continue;

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

        /// <summary>
        /// Picks a prefab from the entry's pools, with slope acting as a shaping
        /// filter on top of per-biome category preferences. Steeper ground pushes
        /// the choice toward rocks; flat ground keeps the biome-default mix.
        /// </summary>
        private static GameObject PickPrefabForSlopeAndBiome(AssetPackBiomeEntry entry, BiomeType biome, float slopeDeg, System.Random rng)
        {
            // Base weights [tree, rock, bush] from biome.
            float[] baseW = biome switch
            {
                BiomeType.TemperateForest or BiomeType.BorealForest or BiomeType.Taiga or BiomeType.TropicalRainforest
                    => new[] { 0.80f, 0.10f, 0.10f },
                BiomeType.Desert or BiomeType.AridSteppe
                    => new[] { 0.20f, 0.60f, 0.20f },
                BiomeType.Mountain
                    => new[] { 0.10f, 0.80f, 0.10f },
                _ => new[] { 0.40f, 0.30f, 0.30f },
            };

            // Slope shaping: as slope grows, shift weight from trees/bushes to rocks.
            // 0° → no shift, 45° (clamp) → pure rocks.
            float t = Mathf.Clamp01(slopeDeg / MaxSlopeDegrees);
            float treeW = baseW[0] * (1f - t);
            float bushW = baseW[2] * (1f - t);
            float rockW = baseW[1] + (baseW[0] + baseW[2]) * t;
            float total = treeW + rockW + bushW;
            if (total <= 0f) return null;
            treeW /= total;
            rockW /= total;

            double r = rng.NextDouble();
            GameObject[] pool;
            if (r < treeW && entry.TreePrefabs != null && entry.TreePrefabs.Length > 0)
                pool = entry.TreePrefabs;
            else if (r < treeW + rockW && entry.RockPrefabs != null && entry.RockPrefabs.Length > 0)
                pool = entry.RockPrefabs;
            else if (entry.BushPrefabs != null && entry.BushPrefabs.Length > 0)
                pool = entry.BushPrefabs;
            else if (entry.RockPrefabs != null && entry.RockPrefabs.Length > 0)
                pool = entry.RockPrefabs;
            else if (entry.TreePrefabs != null && entry.TreePrefabs.Length > 0)
                pool = entry.TreePrefabs;
            else
                return null;
            return pool[rng.Next(pool.Length)];
        }

        /// <summary>
        /// Bilinear sample of the tile heightmap at (localX, localZ) in meters.
        /// Cell centers are at (x * cellSize + cellSize/2, z * cellSize + cellSize/2).
        /// </summary>
        private static float SampleBilinear(float[] heightmap, int widthCells, int heightCells, float cellSize, float localX, float localZ)
        {
            if (heightmap == null || heightmap.Length != widthCells * heightCells) return 0f;
            // Convert to cell-index space; cell i's value is stored at center of cell i.
            float u = localX / cellSize - 0.5f;
            float v = localZ / cellSize - 0.5f;
            int u0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, widthCells - 1);
            int v0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, heightCells - 1);
            int u1 = Mathf.Min(u0 + 1, widthCells - 1);
            int v1 = Mathf.Min(v0 + 1, heightCells - 1);
            float fu = Mathf.Clamp01(u - u0);
            float fv = Mathf.Clamp01(v - v0);
            float h00 = heightmap[v0 * widthCells + u0];
            float h10 = heightmap[v0 * widthCells + u1];
            float h01 = heightmap[v1 * widthCells + u0];
            float h11 = heightmap[v1 * widthCells + u1];
            float a = h00 + (h10 - h00) * fu;
            float b = h01 + (h11 - h01) * fu;
            return a + (b - a) * fv;
        }

        /// <summary>
        /// Slope in degrees via central-difference gradient on the heightmap at (localX, localZ).
        /// </summary>
        private static float ComputeSlopeDegrees(float[] heightmap, int widthCells, int heightCells, float cellSize, float localX, float localZ)
        {
            if (heightmap == null || heightmap.Length != widthCells * heightCells) return 0f;
            int xi = Mathf.Clamp(Mathf.FloorToInt(localX / cellSize), 0, widthCells - 1);
            int zi = Mathf.Clamp(Mathf.FloorToInt(localZ / cellSize), 0, heightCells - 1);
            int xm = Mathf.Max(0, xi - 1);
            int xp = Mathf.Min(widthCells - 1, xi + 1);
            int zm = Mathf.Max(0, zi - 1);
            int zp = Mathf.Min(heightCells - 1, zi + 1);
            float dhdx = (heightmap[zi * widthCells + xp] - heightmap[zi * widthCells + xm]) / ((xp - xm) * cellSize);
            float dhdz = (heightmap[zp * widthCells + xi] - heightmap[zm * widthCells + xi]) / ((zp - zm) * cellSize);
            float gradMag = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz);
            return Mathf.Atan(gradMag) * Mathf.Rad2Deg;
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
