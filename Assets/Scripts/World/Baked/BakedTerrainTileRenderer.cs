using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

// NOTE: do NOT add `using ProceduralWorlds.GTS;` here. This file is inside
// `Assets/Scripts/ForeverEngine.asmdef`; GTS Core has no asmdef and lives
// in Assembly-CSharp, which asmdef code cannot reference (gaia skill
// Bug #17). The GTS shader globals are set by `GTSRuntimeAutoBoot.cs` at
// `Assets/GTSRuntimeAutoBoot.cs` (root, no asmdef → Assembly-CSharp). This
// file only loads the per-tile .mat, which is plain `UnityEngine.Material`
// and needs no GTS namespace.

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Spawns one Unity Terrain GameObject per baked tile, fed from the
    /// macro-bake on disk. Heights upsampled bilinearly from the coarse
    /// 16×16 macro grid into a Unity-pow2+1 heightmap so the player has
    /// something to walk on. Splats fed verbatim from splat_hires.bin
    /// (256² × N-layer union) into TerrainData.SetAlphamaps. TerrainLayers
    /// resolved through BakedAssetRegistry.
    ///
    /// Owned by VoxelWorldManager (or whichever streamer drives chunk
    /// arrivals). Tiles are reference-counted so that arrivals/departures
    /// of constituent chunks ensure/release the underlying Terrain.
    /// </summary>
    public sealed class BakedTerrainTileRenderer
    {
        private const int FallbackHeightmapResolution = 65;
        private const float TileHeightMeters = 1000f;

        // Hardcoded for now — Coniferous Forest Medium is the only baked biome.
        // Lift to per-biome registry (chunk metadata → profile name) when a 2nd
        // biome lands. Resources.Load paths derived from this key match what
        // GaiaHeadlessPipeline.ApplyGTSToBakedTerrains writes.
        private const string GTSBiomeKey = "Coniferous_Forest_Medium";

        private readonly Transform _root;
        private readonly string _layerDir;
        private readonly BakedLayerIndex _index;
        private readonly BakedAssetRegistry _registry;
        private readonly Dictionary<(int tx, int tz), TileEntry> _tiles = new();
        private Material _sharedTerrainMaterial;

        private struct TileEntry
        {
            public GameObject Go;
            public TerrainData Data;
            public int RefCount;
        }

        public BakedTerrainTileRenderer(Transform root, string layerDir, BakedLayerIndex index, BakedAssetRegistry registry)
        {
            _root = root;
            _layerDir = layerDir;
            _index = index;
            _registry = registry;
        }

        /// <summary>
        /// Increments the reference count for a tile, spawning the Terrain on
        /// first ref. Safe to call repeatedly — only loads from disk once.
        /// </summary>
        public void RetainTile(int tileX, int tileZ)
        {
            var key = (tileX, tileZ);
            if (_tiles.TryGetValue(key, out var entry))
            {
                entry.RefCount++;
                _tiles[key] = entry;
                return;
            }

            var tileDir = Path.Combine(_layerDir, $"tile_{tileX}_{tileZ}");
            if (!Directory.Exists(tileDir))
            {
                Debug.LogWarning($"[BakedTerrainTile] missing tile dir {tileDir}");
                return;
            }

            BakedMacroData macro;
            try { macro = BakedWorldReader.LoadMacro(tileDir); }
            catch (System.Exception e) { Debug.LogError($"[BakedTerrainTile] LoadMacro({tileDir}) failed: {e.Message}"); return; }

            var go = BuildTerrainGameObject(macro, tileX, tileZ, out var data);
            if (go == null) return;

            _tiles[key] = new TileEntry { Go = go, Data = data, RefCount = 1 };
        }

        /// <summary>
        /// Decrements the reference count for a tile, releasing the Terrain when
        /// no chunk in this tile is loaded.
        /// </summary>
        public void ReleaseTile(int tileX, int tileZ)
        {
            var key = (tileX, tileZ);
            if (!_tiles.TryGetValue(key, out var entry)) return;
            entry.RefCount--;
            if (entry.RefCount > 0) { _tiles[key] = entry; return; }

            if (entry.Go != null)
            {
                if (Application.isPlaying) Object.Destroy(entry.Go);
                else                       Object.DestroyImmediate(entry.Go);
            }
            if (entry.Data != null)
            {
                if (Application.isPlaying) Object.Destroy(entry.Data);
                else                       Object.DestroyImmediate(entry.Data);
            }
            _tiles.Remove(key);
        }

        public int LoadedTileCount => _tiles.Count;

        private GameObject BuildTerrainGameObject(BakedMacroData macro, int tileX, int tileZ, out TerrainData data)
        {
            // v0x0005 hi-res heightmap drives heightmapResolution directly when
            // present (513 verts default). Pre-v0x0005 bakes fall back to 65,
            // matching the legacy bilinear-from-macro path.
            int heightRes = macro.HighResHeightmapGridSize > 0
                ? macro.HighResHeightmapGridSize
                : FallbackHeightmapResolution;
            data = new TerrainData
            {
                heightmapResolution = heightRes,
                alphamapResolution  = macro.HighResSplatGridSize > 0 ? macro.HighResSplatGridSize : 256,
                baseMapResolution   = 256,
            };
            data.size = new Vector3(_index.TileSize, TileHeightMeters, _index.TileSize);

            ApplyHeightmap(data, macro);

            int layerCount = macro.HighResSplatLayerCount;
            bool hasSplats = layerCount > 0 && macro.HighResSplat != null && macro.HighResSplat.Length > 0;

            // GTS path: per-tile .mat baked by GaiaHeadlessPipeline lives at
            // Resources/GTSMaterials/<biomeKey>_tile_<tx>_<tz>.mat with shader,
            // texture arrays, and per-tile aux textures all bound. The GTS
            // shader handles its own splat sampling via the .mat's pre-baked
            // SplatmapIndex texture, so we skip Unity's TerrainLayers system
            // (mirrors GTSPreprocessBuild's behavior for editor-authored scenes).
            // We ALSO skip ApplyAlphamap for the GTS path — Unity's SetAlphamaps
            // rejects a 3D float array with non-zero layer dim when terrainLayers
            // is null, and the GTS shader doesn't read Unity's alphamap anyway.
            var gtsMat = LoadGTSMaterialForTile(tileX, tileZ);
            bool useGTS = gtsMat != null;

            if (!useGTS)
            {
                if (hasSplats)
                {
                    ApplyTerrainLayers(data, layerCount);
                    ApplyAlphamap(data, macro, layerCount);
                }
                else
                {
                    Debug.LogWarning($"[BakedTerrainTile] tile ({tileX},{tileZ}) has no high-res splat — terrain will be blank");
                }
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = $"BakedTerrain_{tileX}_{tileZ}";
            go.transform.SetParent(_root, worldPositionStays: false);
            go.transform.position = new Vector3(macro.Header.WorldMinX, 0f, macro.Header.WorldMinZ);

            var terrain = go.GetComponent<Terrain>();
            if (terrain != null)
            {
                // Bug #12/#26 prevention: Unity 6 defaults cull terrain at
                // ~1000-1500 units distance. Match the gaia skill audit floor.
                terrain.heightmapPixelError = 5f;
                terrain.basemapDistance = 2000f;

                // GTS shader globals are set by GTSRuntimeAutoBoot (Assembly-CSharp,
                // RuntimeInitializeOnLoadMethod) — no per-tile bootstrap call here.
                terrain.materialTemplate = useGTS ? gtsMat : GetOrCreateTerrainMaterial();

                // Let Unity Terrain draw whatever's stored in TerrainData
                // (treeInstances + detail prototypes). For Alpine Meadow this
                // primarily unlocks grass — 5.7M density per tile baked into
                // detail layers that were previously suppressed. GameObject
                // tree placements come through BakedPropTileRenderer instead.
                terrain.drawTreesAndFoliage = true;

                // Same class of bug as #26 (heightmapPixelError): runtime-
                // constructed Terrains inherit Unity 6 defaults for tree LOD,
                // which sets billboard distance to ~50m. That makes pines
                // flatten into 2D cards immediately around the player. Saved-
                // scene values (TreeDistance=1000, BillboardStart=90, CrossFade=50)
                // don't propagate to Terrain.CreateTerrainGameObject. Override:
                terrain.treeDistance              = 2000f;  // total tree render distance
                terrain.treeBillboardDistance     = 600f;   // 3D mesh -> billboard switch (was ~50)
                terrain.treeCrossFadeLength       = 80f;    // smooth crossfade band
                terrain.treeMaximumFullLODCount   = 400;    // max simultaneously full-3D trees
            }
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < macro.Heightmap.Length; i++)
            { if (macro.Heightmap[i] < minH) minH = macro.Heightmap[i]; if (macro.Heightmap[i] > maxH) maxH = macro.Heightmap[i]; }
            Debug.Log($"[BakedTerrainTile] tile ({tileX},{tileZ}) spawned at world ({go.transform.position.x},{go.transform.position.z}) " +
                      $"heights {minH:F1}..{maxH:F1}m, layers={layerCount}, mat={(useGTS ? "GTS" : "URP/Terrain/Lit")}");
            return go;
        }

        /// <summary>
        /// Per-tile GTS material baked by GaiaHeadlessPipeline.ApplyGTSToBakedTerrains.
        /// Returns null if the tile wasn't baked with GTS or if Resources/GTSMaterials/
        /// is empty (e.g. legacy bake without GTS post-processing) — caller then falls
        /// back to URP/Terrain/Lit.
        /// </summary>
        private static Material LoadGTSMaterialForTile(int tileX, int tileZ) =>
            Resources.Load<Material>($"GTSMaterials/{GTSBiomeKey}_tile_{tileX}_{tileZ}");

        /// <summary>
        /// Default Unity Terrain shader is built-in-pipeline, which renders as
        /// magenta under URP. Resolve the URP terrain shader at runtime so the
        /// build doesn't need a serialized material reference.
        /// </summary>
        private Material GetOrCreateTerrainMaterial()
        {
            if (_sharedTerrainMaterial != null) return _sharedTerrainMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                      ?? Shader.Find("Hidden/TerrainEngine/Splatmap/Universal_Standard")
                      ?? Shader.Find("Nature/Terrain/Standard");
            if (shader == null)
            {
                Debug.LogError("[BakedTerrainTile] no terrain shader available — terrain will render magenta");
                return null;
            }
            _sharedTerrainMaterial = new Material(shader) { name = "BakedTerrainURP" };
            return _sharedTerrainMaterial;
        }

        private static void ApplyHeightmap(TerrainData data, BakedMacroData macro)
        {
            float sy = data.size.y;

            // v0x0005 fast path: hi-res grid maps 1:1 to TerrainData. Source
            // is float meters; normalize per-cell to [0,1] for SetHeights.
            if (macro.HighResHeightmapGridSize > 0 && macro.HighResHeightmap.Length > 0)
            {
                int g = macro.HighResHeightmapGridSize;
                var src = macro.HighResHeightmap;
                var dst = new float[g, g];
                for (int z = 0; z < g; z++)
                for (int x = 0; x < g; x++)
                    dst[z, x] = Mathf.Clamp01(src[z * g + x] / sy);
                data.SetHeights(0, 0, dst);
                return;
            }

            // Fallback: bilinear upsample of the coarse macro heightmap into
            // the legacy 65x65 TerrainData grid. Used for pre-v0x0005 bakes.
            int srcW = macro.Header.MacroWidthCells;
            int srcH = macro.Header.MacroHeightCells;
            var srcMacro = macro.Heightmap;

            int dstRes = FallbackHeightmapResolution;
            var dstMacro = new float[dstRes, dstRes];
            for (int z = 0; z < dstRes; z++)
            {
                float fz = (float)z / (dstRes - 1) * (srcH - 1);
                int z0 = Mathf.FloorToInt(fz);
                int z1 = Mathf.Min(z0 + 1, srcH - 1);
                float tz = fz - z0;
                for (int x = 0; x < dstRes; x++)
                {
                    float fx = (float)x / (dstRes - 1) * (srcW - 1);
                    int x0 = Mathf.FloorToInt(fx);
                    int x1 = Mathf.Min(x0 + 1, srcW - 1);
                    float tx = fx - x0;

                    float h00 = srcMacro[z0 * srcW + x0];
                    float h10 = srcMacro[z0 * srcW + x1];
                    float h01 = srcMacro[z1 * srcW + x0];
                    float h11 = srcMacro[z1 * srcW + x1];
                    float h0 = Mathf.Lerp(h00, h10, tx);
                    float h1 = Mathf.Lerp(h01, h11, tx);
                    float h  = Mathf.Lerp(h0, h1, tz);
                    dstMacro[z, x] = Mathf.Clamp01(h / sy);
                }
            }
            data.SetHeights(0, 0, dstMacro);
        }

        private void ApplyTerrainLayers(TerrainData data, int layerCount)
        {
            var guids = _index.SplatLayerGuids ?? System.Array.Empty<string>();
            var layers = new TerrainLayer[layerCount];
            int resolved = 0;
            for (int i = 0; i < layerCount; i++)
            {
                var guid = i < guids.Length ? guids[i] : null;
                var layer = _registry != null ? _registry.ResolveTerrainLayer(guid) : null;
                if (layer != null) resolved++;
                layers[i] = layer;
            }
            data.terrainLayers = layers;
            if (resolved < layerCount)
                Debug.LogWarning($"[BakedTerrainTile] resolved {resolved}/{layerCount} terrain layers from registry");
        }

        private static void ApplyAlphamap(TerrainData data, BakedMacroData macro, int layerCount)
        {
            int grid = macro.HighResSplatGridSize;
            var src = macro.HighResSplat;
            int cellsPerLayer = grid * grid;

            var dst = new float[grid, grid, layerCount];
            for (int l = 0; l < layerCount; l++)
            {
                int baseIdx = l * cellsPerLayer;
                for (int z = 0; z < grid; z++)
                for (int x = 0; x < grid; x++)
                    dst[z, x, l] = src[baseIdx + z * grid + x] / 255f;
            }
            data.SetAlphamaps(0, 0, dst);
        }
    }
}
