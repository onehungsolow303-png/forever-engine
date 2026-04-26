using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

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
        private const int HeightmapResolution = 65;
        private const float TileHeightMeters = 1000f;

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
            data = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                alphamapResolution  = macro.HighResSplatGridSize > 0 ? macro.HighResSplatGridSize : 256,
                baseMapResolution   = 256,
            };
            data.size = new Vector3(_index.TileSize, TileHeightMeters, _index.TileSize);

            ApplyHeightmap(data, macro);

            int layerCount = macro.HighResSplatLayerCount;
            if (layerCount > 0 && macro.HighResSplat != null && macro.HighResSplat.Length > 0)
            {
                ApplyTerrainLayers(data, layerCount);
                ApplyAlphamap(data, macro, layerCount);
            }
            else
            {
                Debug.LogWarning($"[BakedTerrainTile] tile ({tileX},{tileZ}) has no high-res splat — terrain will be blank");
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = $"BakedTerrain_{tileX}_{tileZ}";
            go.transform.SetParent(_root, worldPositionStays: false);
            go.transform.position = new Vector3(macro.Header.WorldMinX, 0f, macro.Header.WorldMinZ);

            var terrain = go.GetComponent<Terrain>();
            if (terrain != null)
            {
                terrain.materialTemplate = GetOrCreateTerrainMaterial();
                terrain.drawTreesAndFoliage = false; // we render trees ourselves via DrawMeshInstanced
            }
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < macro.Heightmap.Length; i++)
            { if (macro.Heightmap[i] < minH) minH = macro.Heightmap[i]; if (macro.Heightmap[i] > maxH) maxH = macro.Heightmap[i]; }
            Debug.Log($"[BakedTerrainTile] tile ({tileX},{tileZ}) spawned at world ({go.transform.position.x},{go.transform.position.z}) " +
                      $"heights {minH:F1}..{maxH:F1}m, layers={layerCount}");
            return go;
        }

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
            int srcW = macro.Header.MacroWidthCells;
            int srcH = macro.Header.MacroHeightCells;
            var src = macro.Heightmap;

            int dstRes = HeightmapResolution;
            var dst = new float[dstRes, dstRes];
            float sy = data.size.y;
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

                    float h00 = src[z0 * srcW + x0];
                    float h10 = src[z0 * srcW + x1];
                    float h01 = src[z1 * srcW + x0];
                    float h11 = src[z1 * srcW + x1];
                    float h0 = Mathf.Lerp(h00, h10, tx);
                    float h1 = Mathf.Lerp(h01, h11, tx);
                    float h  = Mathf.Lerp(h0, h1, tz);
                    dst[z, x] = Mathf.Clamp01(h / sy);
                }
            }
            data.SetHeights(0, 0, dst);
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
