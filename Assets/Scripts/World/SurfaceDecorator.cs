using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Places vegetation, rocks, and props on terrain chunks based on biome rules.
    /// Uses seeded random placement for determinism (same chunk always gets same props).
    /// Props are simple geometric shapes for now — replace with real models later.
    /// </summary>
    public static class SurfaceDecorator
    {
        /// <summary>
        /// Decorate a terrain chunk with biome-appropriate props.
        /// Returns the parent GameObject containing all placed props.
        /// </summary>
        public static GameObject Decorate(ChunkData chunkData, Terrain terrain)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            var parent = new GameObject($"Props_{coord.X}_{coord.Z}");
            parent.transform.position = coord.WorldOrigin;

            // Seeded RNG for deterministic placement
            int seed = chunkData.ChunkX * 73856093 ^ chunkData.ChunkZ * 19349663;
            var rng = new System.Random(seed);

            var rules = GetBiomeRules(chunkData.Biome);

            // Place each prop type
            foreach (var rule in rules)
            {
                var positions = PoissonDisk(ChunkCoord.ChunkSize, rule.MinSpacing, rule.Count, rng);
                foreach (var pos in positions)
                {
                    // Get terrain height at this position
                    float worldX = coord.WorldOrigin.x + pos.x;
                    float worldZ = coord.WorldOrigin.z + pos.y;
                    float height = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
                    float terrainY = terrain.transform.position.y + height;

                    // Skip if underwater or too steep
                    if (height < 0.1f) continue;

                    var prop = CreateProp(rule.PropType, rng);
                    if (prop == null) continue;

                    float scale = rule.BaseScale * (0.8f + (float)rng.NextDouble() * 0.4f);
                    float rotation = (float)rng.NextDouble() * 360f;

                    prop.transform.localScale = Vector3.one * scale;
                    prop.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
                    // Place pivot at terrain Y. Well-authored prefabs anchor pivot at the
                    // desired ground-contact point; we no longer try to compute a per-prefab
                    // offset from mesh bounds (those pivot conventions differ by asset pack,
                    // and bounds-min often points at a sub-pivot mesh feature that's not the
                    // visual base). Mesh-triangle sampler gives the exact mesh surface Y,
                    // so pivot = terrainY anchors the visible prop on the rendered terrain.
                    prop.transform.position = new Vector3(worldX, terrainY, worldZ);
                    prop.transform.SetParent(parent.transform, worldPositionStays: true);
                }
            }

            // Grass painting disabled for performance — re-enable when GPU instancing is set up
            // if (rules.Exists(r => r.PropType == PropType.Grass))
            //     PaintGrass(chunkData, terrain, rng);

            return parent;
        }

        /// <summary>
        /// Decorate a mesh-based terrain chunk (no Unity Terrain required).
        /// Samples height from the stored heightmap directly.
        /// Catalog path: if an AssetPackBiomeCatalog is loaded and has entries for
        /// this biome, instantiates real pack prefabs. Falls back to procedural
        /// primitives otherwise. The AssetPackBiomeCatalog is the single source of
        /// truth — the baked pipeline consumes the same catalog at bake time.
        /// </summary>
        public static GameObject DecorateMesh(ChunkData chunkData, GameObject terrainGO)
        {
            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            var parent = new GameObject($"Props_{coord.X}_{coord.Z}");
            parent.transform.position = coord.WorldOrigin;

            int seed = chunkData.ChunkX * 73856093 ^ chunkData.ChunkZ * 19349663;
            var rng = new System.Random(seed);

            // 1) Catalog path: if AssetPackBiomeCatalog has entries for this biome,
            // instantiate real pack prefabs. Single source of truth — same catalog
            // the baked pipeline reads at bake time (see PropPlacementSampler).
            var catalog = AssetPackBiomeCatalog.Load();
            var entries = catalog != null ? catalog.GetEntriesForBiome(chunkData.Biome) : System.Array.Empty<AssetPackBiomeEntry>();
            if (entries.Length > 0)
            {
                DecorateFromCatalog(chunkData, parent.transform, rng, chunkData.Biome, entries);
                ScatterGrass(chunkData, parent, rng);
                return parent;
            }

            // 2) Fallback: procedural primitive props (original path).
            var rules = GetBiomeRules(chunkData.Biome);
            int hmRes = ChunkData.HeightmapRes;

            foreach (var rule in rules)
            {
                if (rule.PropType == PropType.Grass) continue; // Skip grass for mesh terrain

                var positions = PoissonDisk(ChunkCoord.ChunkSize, rule.MinSpacing, rule.Count, rng);
                foreach (var pos in positions)
                {
                    float worldX = coord.WorldOrigin.x + pos.x;
                    float worldZ = coord.WorldOrigin.z + pos.y;

                    float height = ForeverEngine.Core.World.TerrainTriangleSampler.SampleMeshTriangleY(
                        chunkData.Heightmap,
                        heightmapRes: hmRes,
                        chunkSizeMeters: ChunkCoord.ChunkSize,
                        meshResolution: ForeverEngine.Core.World.TerrainTriangleSampler.DefaultMeshResolution,
                        localX: pos.x,
                        localZ: pos.y);

                    var prop = CreateProp(rule.PropType, rng);
                    if (prop == null) continue;

                    float scale = rule.BaseScale * (0.8f + (float)rng.NextDouble() * 0.4f);
                    float rotation = (float)rng.NextDouble() * 360f;

                    prop.transform.localScale = Vector3.one * scale;
                    prop.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
                    // Pivot-at-ground placement (see Decorate() above for rationale).
                    prop.transform.position = new Vector3(worldX, height, worldZ);
                    prop.transform.SetParent(parent.transform, worldPositionStays: true);
                }
            }

            return parent;
        }

        /// <summary>
        /// Attach a GrassInstancer to the chunk props parent for biomes that
        /// should have ground foliage. Positions are Poisson-disk scattered with
        /// the same seeded RNG as props, so the result is deterministic per chunk.
        /// </summary>
        private static void ScatterGrass(ChunkData chunkData, GameObject parent, System.Random rng)
        {
            if (!GrassInstancer.Enabled) return;
            if (!BiomeHasGrass(chunkData.Biome)) return;

            var cfg = GrassConfig.Load();
            if (cfg == null || cfg.GrassMesh == null || cfg.GrassMaterial == null) return;

            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            int hmRes = ChunkData.HeightmapRes;
            var positions = PoissonDisk(ChunkCoord.ChunkSize, cfg.MinSpacing, cfg.CountPerChunk, rng);

            var matrices = new Matrix4x4[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                var pos = positions[i];
                float worldX = coord.WorldOrigin.x + pos.x;
                float worldZ = coord.WorldOrigin.z + pos.y;
                float hmX = pos.x / ChunkCoord.ChunkSize * (hmRes - 1);
                float hmZ = pos.y / ChunkCoord.ChunkSize * (hmRes - 1);
                int ix = Mathf.Clamp(Mathf.RoundToInt(hmX), 0, hmRes - 1);
                int iz = Mathf.Clamp(Mathf.RoundToInt(hmZ), 0, hmRes - 1);
                // Grass uses nearest-neighbor heightmap lookup (cheaper, visually indistinguishable at blade density). Props use mesh-triangle.
                float height = chunkData.Heightmap[iz * hmRes + ix];

                float scale = cfg.BaseScale * (0.8f + (float)rng.NextDouble() * 0.4f);
                float yaw = (float)rng.NextDouble() * 360f;
                matrices[i] = Matrix4x4.TRS(
                    new Vector3(worldX, height, worldZ),
                    Quaternion.Euler(0f, yaw, 0f),
                    Vector3.one * scale);
            }

            var instancer = parent.AddComponent<GrassInstancer>();
            var center = coord.WorldOrigin + new Vector3(ChunkCoord.ChunkSize * 0.5f, 0f, ChunkCoord.ChunkSize * 0.5f);
            instancer.Setup(cfg.GrassMesh, cfg.GrassMaterial, matrices, center, cfg.MaxDrawDistance);
        }

        private static bool BiomeHasGrass(BiomeType biome) => biome switch
        {
            BiomeType.Grassland or
            BiomeType.TemperateForest or
            BiomeType.BorealForest or
            BiomeType.Savanna or
            BiomeType.TropicalRainforest or
            BiomeType.Taiga => true,
            _ => false,
        };

        // Diagnostic kill-switch for SurfaceDecorator. Tested 2026-04-19 with
        // PropsEnabled=false → FPS remained ~4, so prop instantiation was NOT
        // the dominant bottleneck. Leave this enabled for normal play; flip if
        // the prop-cost hypothesis ever needs revisiting.
        public static bool PropsEnabled = true;

        // Per-biome fallback density. Baked tiles are authoritative — this path only
        // runs when a chunk has no baked props. Values keep the fallback visually
        // consistent with the primitive path in GetBiomeRules (same total count ±).
        private struct FallbackDensity { public int Count; public float MinSpacing; public float BaseScale; }

        private static FallbackDensity GetFallbackDensity(BiomeType biome) => biome switch
        {
            BiomeType.Grassland           => new FallbackDensity { Count = 6,  MinSpacing = 40f, BaseScale = 1.0f },
            BiomeType.TemperateForest     => new FallbackDensity { Count = 35, MinSpacing = 16f, BaseScale = 1.0f },
            BiomeType.BorealForest        => new FallbackDensity { Count = 35, MinSpacing = 16f, BaseScale = 1.0f },
            BiomeType.Taiga               => new FallbackDensity { Count = 25, MinSpacing = 20f, BaseScale = 1.0f },
            BiomeType.Mountain            => new FallbackDensity { Count = 25, MinSpacing = 20f, BaseScale = 1.0f },
            BiomeType.Desert              => new FallbackDensity { Count = 12, MinSpacing = 35f, BaseScale = 1.0f },
            BiomeType.AridSteppe          => new FallbackDensity { Count = 15, MinSpacing = 28f, BaseScale = 1.0f },
            BiomeType.Tundra              => new FallbackDensity { Count = 12, MinSpacing = 22f, BaseScale = 1.0f },
            BiomeType.Savanna             => new FallbackDensity { Count = 15, MinSpacing = 30f, BaseScale = 1.0f },
            BiomeType.TropicalRainforest  => new FallbackDensity { Count = 45, MinSpacing = 12f, BaseScale = 1.0f },
            _ => new FallbackDensity { Count = 0, MinSpacing = 30f, BaseScale = 1.0f },
        };

        private static void DecorateFromCatalog(
            ChunkData chunkData,
            Transform parent,
            System.Random rng,
            BiomeType biome,
            AssetPackBiomeEntry[] entries)
        {
            if (!PropsEnabled) return;
            if (entries == null || entries.Length == 0) return;

            var density = GetFallbackDensity(biome);
            if (density.Count == 0) return;

            var coord = new ChunkCoord(chunkData.ChunkX, chunkData.ChunkZ);
            int hmRes = ChunkData.HeightmapRes;

            var positions = PoissonDisk(ChunkCoord.ChunkSize, density.MinSpacing, density.Count, rng);
            foreach (var pos in positions)
            {
                float worldX = coord.WorldOrigin.x + pos.x;
                float worldZ = coord.WorldOrigin.z + pos.y;

                float height = ForeverEngine.Core.World.TerrainTriangleSampler.SampleMeshTriangleY(
                    chunkData.Heightmap,
                    heightmapRes: hmRes,
                    chunkSizeMeters: ChunkCoord.ChunkSize,
                    meshResolution: ForeverEngine.Core.World.TerrainTriangleSampler.DefaultMeshResolution,
                    localX: pos.x,
                    localZ: pos.y);

                var entry = entries[rng.Next(entries.Length)];
                var prefab = PickPrefabFromEntry(entry, biome, rng);
                if (prefab == null) continue;

                var go = Object.Instantiate(prefab);
                float scale = density.BaseScale * (0.8f + (float)rng.NextDouble() * 0.4f);
                float rotation = (float)rng.NextDouble() * 360f;
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
                // Pivot-at-ground placement (see Decorate() above for rationale).
                go.transform.position = new Vector3(worldX, height, worldZ);
                go.transform.SetParent(parent, worldPositionStays: true);

                // Props are pass-through — strip EVERY collider (root + children).
                // Imported mesh prefabs often have MeshColliders on child trunk/
                // branch/leaf objects; leaving them in place (a) blocks player
                // movement and (b) misleads spawn raycast into hitting props
                // instead of terrain, causing fall-through-ground bugs.
                foreach (var col in go.GetComponentsInChildren<Collider>(includeInactive: true))
                    Object.Destroy(col);
            }
        }

        // Category selection mirrors PropPlacementSampler's biome weights minus slope
        // shaping (fallback path has no per-cell slope). Coarse-on-purpose — the baked
        // pipeline handles the authoritative placement.
        internal static GameObject PickPrefabFromEntry(AssetPackBiomeEntry entry, BiomeType biome, System.Random rng)
        {
            if (entry == null) return null;

            float[] w = biome switch
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
            if (r < w[0] && HasAny(entry.TreePrefabs)) pool = entry.TreePrefabs;
            else if (r < w[0] + w[1] && HasAny(entry.RockPrefabs)) pool = entry.RockPrefabs;
            else if (HasAny(entry.BushPrefabs)) pool = entry.BushPrefabs;
            else if (HasAny(entry.RockPrefabs)) pool = entry.RockPrefabs;
            else if (HasAny(entry.TreePrefabs)) pool = entry.TreePrefabs;
            else return null;
            return pool[rng.Next(pool.Length)];
        }

        private static bool HasAny(GameObject[] arr) => arr != null && arr.Length > 0;

        /// <summary>Remove all decoration from a chunk.</summary>
        public static void RemoveDecoration(GameObject propsParent)
        {
            if (propsParent != null)
            {
                int childCount = propsParent.transform.childCount;
                Debug.Log($"[DECOR-DIAG] remove parent={propsParent.name} children={childCount} t={Time.realtimeSinceStartup:F2}");
                Object.Destroy(propsParent);
            }
        }

        // ── Prop Types ────────────────────────────────────────────────────

        private enum PropType { Tree, ConiferTree, Rock, Bush, DeadTree, Cactus, Grass }

        private struct PlacementRule
        {
            public PropType PropType;
            public int Count;
            public float MinSpacing;
            public float BaseScale;
        }

        // ── Biome Rules ───────────────────────────────────────────────────

        private static List<PlacementRule> GetBiomeRules(BiomeType biome) => biome switch
        {
            BiomeType.Grassland => new List<PlacementRule>
            {
                new() { PropType = PropType.Tree, Count = 3, MinSpacing = 50f, BaseScale = 3f },
                new() { PropType = PropType.Rock, Count = 3, MinSpacing = 40f, BaseScale = 1.2f },
            },
            BiomeType.TemperateForest => new List<PlacementRule>
            {
                new() { PropType = PropType.Tree, Count = 20, MinSpacing = 18f, BaseScale = 4f },
                new() { PropType = PropType.Rock, Count = 5, MinSpacing = 35f, BaseScale = 1.5f },
                new() { PropType = PropType.Bush, Count = 10, MinSpacing = 20f, BaseScale = 0.7f },
                new() { PropType = PropType.Grass, Count = 0, MinSpacing = 0f, BaseScale = 0f },
            },
            BiomeType.BorealForest => new List<PlacementRule>
            {
                new() { PropType = PropType.ConiferTree, Count = 25, MinSpacing = 16f, BaseScale = 3.5f },
                new() { PropType = PropType.Rock, Count = 8, MinSpacing = 25f, BaseScale = 1.8f },
                new() { PropType = PropType.Bush, Count = 5, MinSpacing = 25f, BaseScale = 0.6f },
                new() { PropType = PropType.Grass, Count = 0, MinSpacing = 0f, BaseScale = 0f },
            },
            BiomeType.Taiga => new List<PlacementRule>
            {
                new() { PropType = PropType.ConiferTree, Count = 15, MinSpacing = 20f, BaseScale = 3f },
                new() { PropType = PropType.Rock, Count = 10, MinSpacing = 25f, BaseScale = 2f },
            },
            BiomeType.Mountain => new List<PlacementRule>
            {
                new() { PropType = PropType.Rock, Count = 20, MinSpacing = 18f, BaseScale = 2.5f },
                new() { PropType = PropType.ConiferTree, Count = 4, MinSpacing = 40f, BaseScale = 2.5f },
            },
            BiomeType.Desert => new List<PlacementRule>
            {
                new() { PropType = PropType.Cactus, Count = 4, MinSpacing = 45f, BaseScale = 1.5f },
                new() { PropType = PropType.Rock, Count = 5, MinSpacing = 40f, BaseScale = 2f },
                new() { PropType = PropType.DeadTree, Count = 2, MinSpacing = 50f, BaseScale = 2f },
            },
            BiomeType.AridSteppe => new List<PlacementRule>
            {
                new() { PropType = PropType.Rock, Count = 8, MinSpacing = 30f, BaseScale = 1.5f },
                new() { PropType = PropType.Bush, Count = 5, MinSpacing = 30f, BaseScale = 0.5f },
                new() { PropType = PropType.DeadTree, Count = 2, MinSpacing = 50f, BaseScale = 2f },
            },
            BiomeType.Tundra => new List<PlacementRule>
            {
                new() { PropType = PropType.Rock, Count = 12, MinSpacing = 22f, BaseScale = 1.5f },
            },
            BiomeType.Savanna => new List<PlacementRule>
            {
                new() { PropType = PropType.Tree, Count = 4, MinSpacing = 50f, BaseScale = 4.5f },
                new() { PropType = PropType.Bush, Count = 8, MinSpacing = 25f, BaseScale = 0.6f },
                new() { PropType = PropType.Rock, Count = 4, MinSpacing = 40f, BaseScale = 1f },
                new() { PropType = PropType.Grass, Count = 0, MinSpacing = 0f, BaseScale = 0f },
            },
            BiomeType.TropicalRainforest => new List<PlacementRule>
            {
                new() { PropType = PropType.Tree, Count = 30, MinSpacing = 12f, BaseScale = 5f },
                new() { PropType = PropType.Bush, Count = 15, MinSpacing = 12f, BaseScale = 0.8f },
                new() { PropType = PropType.Grass, Count = 0, MinSpacing = 0f, BaseScale = 0f },
            },
            _ => new List<PlacementRule>(),
        };

        // ── Prop Creation (simple geometric shapes) ───────────────────────

        private static GameObject CreateProp(PropType type, System.Random rng)
        {
            switch (type)
            {
                case PropType.Tree:
                    return CreateDeciduous(rng);
                case PropType.ConiferTree:
                    return CreateConifer(rng);
                case PropType.Rock:
                    return CreateRock(rng);
                case PropType.Bush:
                    return CreateBush(rng);
                case PropType.DeadTree:
                    return CreateDeadTree(rng);
                case PropType.Cactus:
                    return CreateCactus(rng);
                default:
                    return null;
            }
        }

        private static GameObject CreateDeciduous(System.Random rng)
        {
            var tree = new GameObject("Tree");

            // Trunk (cylinder)
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
            trunk.transform.localPosition = new Vector3(0f, 1f, 0f);
            SetColor(trunk, new Color(0.4f, 0.25f, 0.1f));
            RemoveCollider(trunk);

            // Canopy (sphere)
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(tree.transform, false);
            float canopySize = 0.7f + (float)rng.NextDouble() * 0.4f;
            canopy.transform.localScale = new Vector3(canopySize, canopySize * 0.8f, canopySize);
            canopy.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            float g = 0.25f + (float)rng.NextDouble() * 0.2f;
            SetColor(canopy, new Color(0.15f, g, 0.1f));
            RemoveCollider(canopy);

            return tree;
        }

        private static GameObject CreateConifer(System.Random rng)
        {
            var tree = new GameObject("Conifer");

            // Trunk
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localScale = new Vector3(0.1f, 1.2f, 0.1f);
            trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            SetColor(trunk, new Color(0.35f, 0.2f, 0.1f));
            RemoveCollider(trunk);

            // Cone layers (3 stacked spheres, getting smaller)
            for (int i = 0; i < 3; i++)
            {
                var layer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                layer.transform.SetParent(tree.transform, false);
                float s = 0.5f - i * 0.12f;
                layer.transform.localScale = new Vector3(s, s * 1.3f, s);
                layer.transform.localPosition = new Vector3(0f, 2.0f + i * 0.6f, 0f);
                float g = 0.2f + (float)rng.NextDouble() * 0.15f;
                SetColor(layer, new Color(0.1f, g, 0.08f));
                RemoveCollider(layer);
            }

            return tree;
        }

        private static GameObject CreateRock(System.Random rng)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            float sx = 0.6f + (float)rng.NextDouble() * 0.8f;
            float sy = 0.4f + (float)rng.NextDouble() * 0.4f;
            float sz = 0.6f + (float)rng.NextDouble() * 0.8f;
            rock.transform.localScale = new Vector3(sx, sy, sz);
            float grey = 0.35f + (float)rng.NextDouble() * 0.2f;
            SetColor(rock, new Color(grey, grey * 0.95f, grey * 0.9f));
            RemoveCollider(rock);
            return rock;
        }

        private static GameObject CreateBush(System.Random rng)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush";
            float s = 0.3f + (float)rng.NextDouble() * 0.3f;
            bush.transform.localScale = new Vector3(s, s * 0.7f, s);
            float g = 0.3f + (float)rng.NextDouble() * 0.2f;
            SetColor(bush, new Color(0.15f, g, 0.1f));
            RemoveCollider(bush);
            return bush;
        }

        private static GameObject CreateDeadTree(System.Random rng)
        {
            var tree = new GameObject("DeadTree");
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localScale = new Vector3(0.12f, 1.5f, 0.12f);
            trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            // Slight lean
            tree.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 15f - 7.5f);
            SetColor(trunk, new Color(0.3f, 0.25f, 0.2f));
            RemoveCollider(trunk);
            return tree;
        }

        private static GameObject CreateCactus(System.Random rng)
        {
            var cactus = new GameObject("Cactus");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(cactus.transform, false);
            body.transform.localScale = new Vector3(0.15f, 0.8f, 0.15f);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            SetColor(body, new Color(0.2f, 0.45f, 0.15f));
            RemoveCollider(body);

            // Arm
            if (rng.NextDouble() > 0.4f)
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arm.transform.SetParent(cactus.transform, false);
                arm.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
                arm.transform.localPosition = new Vector3(0.2f, 1.2f, 0f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                SetColor(arm, new Color(0.2f, 0.45f, 0.15f));
                RemoveCollider(arm);
            }
            return cactus;
        }

        // ── Grass Painting ────────────────────────────────────────────────

        private static void PaintGrass(ChunkData chunkData, Terrain terrain, System.Random rng)
        {
            var terrainData = terrain.terrainData;

            // Create a grass detail prototype
            var detail = new DetailPrototype();
            detail.prototypeTexture = null; // Will use default grass rendering
            detail.renderMode = DetailRenderMode.GrassBillboard;
            detail.healthyColor = new Color(0.3f, 0.6f, 0.2f);
            detail.dryColor = new Color(0.5f, 0.5f, 0.2f);
            detail.minHeight = 0.3f;
            detail.maxHeight = 0.8f;
            detail.minWidth = 0.3f;
            detail.maxWidth = 0.6f;
            terrainData.detailPrototypes = new[] { detail };

            // Paint grass density
            int resolution = terrainData.detailResolution > 0 ? terrainData.detailResolution : 128;
            terrainData.SetDetailResolution(resolution, 16);
            int[,] grassMap = new int[resolution, resolution];

            float density = chunkData.Biome switch
            {
                BiomeType.Grassland => 0.7f,
                BiomeType.Savanna => 0.4f,
                BiomeType.TemperateForest => 0.3f,
                BiomeType.TropicalRainforest => 0.5f,
                BiomeType.BorealForest => 0.15f,
                _ => 0f,
            };

            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                    grassMap[y, x] = rng.NextDouble() < density ? rng.Next(1, 4) : 0;

            terrainData.SetDetailLayer(0, 0, 0, grassMap);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
            renderer.material = mat;
        }

        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        /// <summary>
        /// Poisson disk sampling — places points with minimum spacing for natural distribution.
        /// Returns positions in local chunk space (0 to chunkSize).
        /// </summary>
        private static List<Vector2> PoissonDisk(int size, float minSpacing, int maxPoints, System.Random rng)
        {
            var points = new List<Vector2>();
            int attempts = maxPoints * 15; // Try 15x to find valid positions

            for (int i = 0; i < attempts && points.Count < maxPoints; i++)
            {
                float x = (float)rng.NextDouble() * size;
                float y = (float)rng.NextDouble() * size;
                var candidate = new Vector2(x, y);

                bool valid = true;
                foreach (var existing in points)
                {
                    if (Vector2.Distance(candidate, existing) < minSpacing)
                    { valid = false; break; }
                }

                if (valid)
                    points.Add(candidate);
            }

            return points;
        }

        // Internal for tests. Samples the rendered mesh surface at (localX, localZ) using the
        // shared TerrainTriangleSampler, matching the 33-cell mesh topology built by
        // TerrainGenerator.BuildLodMesh. Client heightmap is 64×64 post-upsample; the sampler
        // works identically at either 16 or 64 res since 64×64 IS bilinear-of-16×16.
        public static float SampleGroundY_ForTest(
            float[] heightmap, int hmRes, float chunkSizeM, float localX, float localZ)
        {
            return ForeverEngine.Core.World.TerrainTriangleSampler.SampleMeshTriangleY(
                heightmap,
                heightmapRes: hmRes,
                chunkSizeMeters: chunkSizeM,
                meshResolution: ForeverEngine.Core.World.TerrainTriangleSampler.DefaultMeshResolution,
                localX: localX,
                localZ: localZ);
        }

        /// <summary>
        /// Bilinear-sample the chunk heightmap at fractional cell coord (hmX, hmZ).
        /// Matches the interpolation used by TerrainGenerator.BuildLodMesh, so props
        /// placed at arbitrary sub-cell positions land on the actual mesh surface
        /// instead of floating/sinking by up to one heightmap cell's delta.
        /// </summary>
        private static float SampleHeightmapBilinear(float[] heightmap, int hmRes, float hmX, float hmZ)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(hmX), 0, hmRes - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(hmZ), 0, hmRes - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, hmRes - 1);
            int z1 = Mathf.Clamp(z0 + 1, 0, hmRes - 1);
            float fx = Mathf.Clamp01(hmX - x0);
            float fz = Mathf.Clamp01(hmZ - z0);

            float h00 = heightmap[z0 * hmRes + x0];
            float h10 = heightmap[z0 * hmRes + x1];
            float h01 = heightmap[z1 * hmRes + x0];
            float h11 = heightmap[z1 * hmRes + x1];

            float a = h00 + (h10 - h00) * fx;
            float b = h01 + (h11 - h01) * fx;
            return a + (b - a) * fz;
        }
    }
}
