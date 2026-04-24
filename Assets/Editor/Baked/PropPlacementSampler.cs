using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Deterministic prop placer for the macro bake with slope/height fitness filtering.
    ///
    /// Y and slope both come from <see cref="BakedElevationSynth.Sample"/> — the SAME
    /// function the runtime uses to compute terrain height at a world position. This is
    /// the non-negotiable invariant: bake-time and runtime must agree on ground Y or
    /// props float / sink. Don't replace with a local sampler that "looks equivalent";
    /// every past "floating props" bug in this codebase traces back to two samplers
    /// disagreeing by half a cell + missing detail noise.
    ///
    /// Per candidate:
    ///   1. Random (offsetX, offsetZ) within the cell
    ///   2. World Y  = BakedElevationSynth.Sample(wx, wz, macro, layerId)
    ///   3. Slope    = central-difference of BakedElevationSynth at (wx, wz) +/- dx
    ///   4. Candidates on slope > 45° are dropped (no pitch/roll in wire protocol,
    ///      so props can't lie flat on steep terrain)
    ///   5. Prefab category chosen by biome + slope band; mixed Tree/Rock/Bush pools.
    /// </summary>
    public static class PropPlacementSampler
    {
        /// <summary>Half-cell step used to compute slope via central-difference on the
        /// elevation-synth surface. Must be much smaller than a macro cell (64m) to
        /// keep the slope estimate local.</summary>
        private const float SlopeSampleDeltaMeters = 1f;
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
            // Build a transient BakedMacroData exactly as it will live on disk after
            // this bake, so SampleGroundY / ComputeSlopeDegrees use the same function
            // runtime uses. The Props/Splat/Features fields are unused by
            // BakedElevationSynth — pass empty arrays.
            var header = new BakedLayerHeader(
                Magic: "FEW1",
                FormatVersion: BakedFormatConstants.FormatVersion,
                LayerId: layerId,
                WorldMinX: worldMinX, WorldMinZ: worldMinZ,
                WorldMaxX: worldMinX + widthCells * cellSizeMeters,
                WorldMaxZ: worldMinZ + heightCells * cellSizeMeters,
                MacroCellSizeMeters: cellSizeMeters,
                MacroWidthCells: widthCells, MacroHeightCells: heightCells,
                BiomeTableChecksum: 0,
                BakedAtUnixSeconds: 0,
                TileX: 0, TileZ: 0);
            var macro = new BakedMacroData(
                Header: header,
                Heightmap: heightmap,
                Biome: biome,
                Splat: Array.Empty<byte>(),
                Features: Array.Empty<byte>(),
                Props: Array.Empty<BakedPropPlacement>());

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

                        float cellOffX = (float)rng.NextDouble() * cellSizeMeters;
                        float cellOffZ = (float)rng.NextDouble() * cellSizeMeters;
                        float wx = worldMinX + x * cellSizeMeters + cellOffX;
                        float wz = worldMinZ + z * cellSizeMeters + cellOffZ;

                        float wy = BakedElevationSynth.Sample(wx, wz, macro, layerId);
                        float slopeDeg = ComputeSlopeDegrees(wx, wz, macro, layerId);

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
        /// Slope in degrees at (worldX, worldZ) computed as the central difference of
        /// <see cref="BakedElevationSynth.Sample"/> over <see cref="SlopeSampleDeltaMeters"/>.
        /// Uses the same elevation function as the runtime so rejected-as-too-steep
        /// candidates are rejected based on the actual rendered terrain, not an
        /// approximation that happens to be cheaper to compute.
        /// </summary>
        public static float ComputeSlopeDegrees(float worldX, float worldZ, BakedMacroData macro, int layerId)
        {
            float d = SlopeSampleDeltaMeters;
            float hxp = BakedElevationSynth.Sample(worldX + d, worldZ, macro, layerId);
            float hxm = BakedElevationSynth.Sample(worldX - d, worldZ, macro, layerId);
            float hzp = BakedElevationSynth.Sample(worldX, worldZ + d, macro, layerId);
            float hzm = BakedElevationSynth.Sample(worldX, worldZ - d, macro, layerId);
            float dhdx = (hxp - hxm) / (2f * d);
            float dhdz = (hzp - hzm) / (2f * d);
            float gradMag = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz);
            return Mathf.Atan(gradMag) * Mathf.Rad2Deg;
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
