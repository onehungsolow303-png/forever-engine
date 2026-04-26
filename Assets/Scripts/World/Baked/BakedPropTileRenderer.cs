using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Per-tile instantiator for Gaia GameObject placements (props.bin). Tile-
    /// keyed and reference-counted to mirror BakedTerrainTileRenderer / Tree.
    /// On Retain we read props, resolve each PrefabGuid via BakedAssetRegistry
    /// (PropPrefabs map populated at bake time from the union of props.bin
    /// GUIDs), and instantiate at exact authored world transform. On Release
    /// we destroy every spawned GameObject.
    ///
    /// Why this exists: Gaia's Alpine Meadow tree spawner places trees as
    /// GameObject children of the Terrain (NOT terrainData.treeInstances), so
    /// BakedTreeInstanceRenderer's DrawMeshInstanced path captures nothing.
    /// Props.bin holds the GameObject placements via PropSourceSelector +
    /// GaiaPlacementExtractor — this renderer instantiates them.
    /// </summary>
    public sealed class BakedPropTileRenderer
    {
        private readonly Transform _root;
        private readonly string _layerDir;
        private readonly BakedAssetRegistry _registry;

        private readonly Dictionary<(int tx, int tz), TileEntry> _tiles = new();
        private readonly HashSet<string> _missingGuidsLogged = new();

        private struct TileEntry
        {
            public int RefCount;
            public List<GameObject> Spawned;
        }

        public BakedPropTileRenderer(Transform root, string layerDir, BakedAssetRegistry registry)
        {
            _root = root;
            _layerDir = layerDir;
            _registry = registry;
        }

        public int LoadedTileCount => _tiles.Count;

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
            if (!Directory.Exists(tileDir)) return;

            BakedMacroData macro;
            try { macro = BakedWorldReader.LoadMacro(tileDir); }
            catch (System.Exception e) { Debug.LogError($"[BakedPropRenderer] LoadMacro({tileDir}) failed: {e.Message}"); return; }

            var spawned = Instantiate(macro, tileX, tileZ);
            _tiles[key] = new TileEntry { RefCount = 1, Spawned = spawned };
        }

        public void ReleaseTile(int tileX, int tileZ)
        {
            var key = (tileX, tileZ);
            if (!_tiles.TryGetValue(key, out var entry)) return;
            entry.RefCount--;
            if (entry.RefCount > 0) { _tiles[key] = entry; return; }

            if (entry.Spawned != null)
            {
                bool isPlaying = Application.isPlaying;
                foreach (var go in entry.Spawned)
                {
                    if (go == null) continue;
                    if (isPlaying) Object.Destroy(go);
                    else           Object.DestroyImmediate(go);
                }
            }
            _tiles.Remove(key);
        }

        private List<GameObject> Instantiate(BakedMacroData macro, int tileX, int tileZ)
        {
            var spawned = new List<GameObject>();
            var props = macro.Props ?? System.Array.Empty<BakedPropPlacement>();
            if (props.Length == 0) return spawned;
            if (_registry == null)
            {
                Debug.LogWarning($"[BakedPropRenderer] no BakedAssetRegistry — tile ({tileX},{tileZ}) {props.Length} props skipped");
                return spawned;
            }

            int placed = 0, missing = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                var prefab = _registry.ResolvePropPrefab(p.PrefabGuid);
                if (prefab == null)
                {
                    missing++;
                    if (_missingGuidsLogged.Add(p.PrefabGuid))
                        Debug.LogWarning($"[BakedPropRenderer] prefab guid {p.PrefabGuid} ({p.PrefabResourcePath}) not in registry — repopulate after bake");
                    continue;
                }
                var go = Object.Instantiate(prefab, _root);
                go.transform.position = new Vector3(p.WorldX, p.WorldY, p.WorldZ);
                go.transform.rotation = Quaternion.Euler(0f, p.YawDegrees, 0f);
                go.transform.localScale = Vector3.one * p.UniformScale;
                // Strip colliders — props are pass-through (matches BakedPropRenderer convention)
                bool isPlaying = Application.isPlaying;
                foreach (var col in go.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    if (isPlaying) Object.Destroy(col);
                    else           Object.DestroyImmediate(col);
                }
                spawned.Add(go);
                placed++;
                if (p.WorldY < minY) minY = p.WorldY;
                if (p.WorldY > maxY) maxY = p.WorldY;
            }
            string yRange = placed > 0 ? $"Y[{minY:F1}..{maxY:F1}]" : "(no Y)";
            string sampleNames = "";
            int samples = Mathf.Min(3, spawned.Count);
            for (int s = 0; s < samples; s++)
            {
                var go = spawned[s];
                if (go == null) continue;
                var pos = go.transform.position;
                sampleNames += $" [{go.name} @ ({pos.x:F0},{pos.y:F0},{pos.z:F0})]";
            }
            Debug.Log($"[BakedPropRenderer] tile ({tileX},{tileZ}): placed {placed}/{props.Length} props {yRange} ({missing} unresolved) samples={sampleNames}");
            return spawned;
        }
    }
}
