using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Per-frame Graphics.DrawMeshInstanced driver for tile tree instances.
    /// Tile-keyed and reference-counted to mirror BakedTerrainTileRenderer.
    /// On Retain we read the tile's tree_instances.bin, group by prototype,
    /// pre-extract the prefab's Mesh + sharedMaterials per submesh, and
    /// pre-bake matrix batches of ≤1023 (the DrawMeshInstanced cap). On
    /// Render() we issue one DrawMeshInstanced call per submesh per batch.
    /// On Release we drop the tile's batches.
    /// </summary>
    public sealed class BakedTreeInstanceRenderer
    {
        private const int InstancingBatchCap = 1023;

        private readonly string _layerDir;
        private readonly BakedAssetRegistry _registry;
        private readonly BakedLayerIndex _index;

        private readonly Dictionary<(int tx, int tz), TileEntry> _tiles = new();

        private struct TileEntry { public int RefCount; public List<PrototypeBatches> Prototypes; }

        private struct PrototypeBatches
        {
            public Mesh Mesh;
            public Material[] Materials; // one per submesh
            public Matrix4x4[][] Batches;
            public int[] BatchCounts;
        }

        public BakedTreeInstanceRenderer(string layerDir, BakedLayerIndex index, BakedAssetRegistry registry)
        {
            _layerDir = layerDir;
            _index = index;
            _registry = registry;
        }

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
            catch (System.Exception e) { Debug.LogError($"[BakedTreeRenderer] LoadMacro({tileDir}) failed: {e.Message}"); return; }

            var prototypes = BuildPrototypeBatches(macro);
            _tiles[key] = new TileEntry { RefCount = 1, Prototypes = prototypes };
        }

        public void ReleaseTile(int tileX, int tileZ)
        {
            var key = (tileX, tileZ);
            if (!_tiles.TryGetValue(key, out var entry)) return;
            entry.RefCount--;
            if (entry.RefCount > 0) { _tiles[key] = entry; return; }
            _tiles.Remove(key);
        }

        public void Render()
        {
            foreach (var kv in _tiles)
            {
                var protos = kv.Value.Prototypes;
                if (protos == null) continue;
                for (int p = 0; p < protos.Count; p++)
                {
                    var pb = protos[p];
                    if (pb.Mesh == null || pb.Materials == null || pb.Batches == null) continue;
                    int submeshes = Mathf.Min(pb.Mesh.subMeshCount, pb.Materials.Length);
                    for (int b = 0; b < pb.Batches.Length; b++)
                    {
                        var matrices = pb.Batches[b];
                        int count = pb.BatchCounts[b];
                        if (count <= 0) continue;
                        for (int sm = 0; sm < submeshes; sm++)
                            Graphics.DrawMeshInstanced(pb.Mesh, sm, pb.Materials[sm], matrices, count);
                    }
                }
            }
        }

        public int LoadedTileCount => _tiles.Count;

        private List<PrototypeBatches> BuildPrototypeBatches(BakedMacroData macro)
        {
            var result = new List<PrototypeBatches>();
            var instances = macro.TreeInstances ?? System.Array.Empty<BakedTreeInstance>();
            if (instances.Length == 0) return result;

            // Group instances by prototype index.
            var byProto = new Dictionary<ushort, List<Matrix4x4>>();
            for (int i = 0; i < instances.Length; i++)
            {
                var ti = instances[i];
                var pos = new Vector3(ti.WorldX, ti.WorldY, ti.WorldZ);
                var rot = Quaternion.Euler(0f, ti.YawDegrees, 0f);
                var scl = new Vector3(ti.WidthScale, ti.HeightScale, ti.WidthScale);
                var m = Matrix4x4.TRS(pos, rot, scl);
                if (!byProto.TryGetValue(ti.PrototypeIndex, out var list))
                {
                    list = new List<Matrix4x4>();
                    byProto[ti.PrototypeIndex] = list;
                }
                list.Add(m);
            }

            var guids = _index.TreePrototypeGuids ?? System.Array.Empty<string>();
            foreach (var kv in byProto)
            {
                ushort idx = kv.Key;
                if (idx >= guids.Length) continue;
                var prefab = _registry != null ? _registry.ResolveTreePrefab(guids[idx]) : null;
                if (prefab == null) { Debug.LogWarning($"[BakedTreeRenderer] no prefab for guid {guids[idx]}"); continue; }

                var mf = prefab.GetComponentInChildren<MeshFilter>();
                var mr = prefab.GetComponentInChildren<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null) continue;

                // DrawMeshInstanced requires enableInstancing on every material
                // AND a non-null material per submesh. Gaia tree FBXs sometimes
                // import with null materials (broken HDRP→URP migration on the
                // pack); skip the whole prototype rather than crash per-frame.
                var srcMats = mr.sharedMaterials;
                bool anyNull = false;
                for (int m = 0; m < srcMats.Length; m++) if (srcMats[m] == null) { anyNull = true; break; }
                if (anyNull)
                {
                    Debug.LogWarning($"[BakedTreeRenderer] {prefab.name} has null material(s) — skipping prototype (likely broken FBX-mat import)");
                    continue;
                }
                var instancedMats = new Material[srcMats.Length];
                for (int m = 0; m < srcMats.Length; m++)
                {
                    if (srcMats[m].enableInstancing) { instancedMats[m] = srcMats[m]; continue; }
                    instancedMats[m] = new Material(srcMats[m]) { enableInstancing = true };
                }

                var matrices = kv.Value;
                int batchCount = (matrices.Count + InstancingBatchCap - 1) / InstancingBatchCap;
                var batches = new Matrix4x4[batchCount][];
                var counts  = new int[batchCount];
                for (int b = 0; b < batchCount; b++)
                {
                    int start = b * InstancingBatchCap;
                    int len = Mathf.Min(InstancingBatchCap, matrices.Count - start);
                    batches[b] = new Matrix4x4[InstancingBatchCap];
                    matrices.CopyTo(start, batches[b], 0, len);
                    counts[b] = len;
                }

                result.Add(new PrototypeBatches
                {
                    Mesh      = mf.sharedMesh,
                    Materials = instancedMats,
                    Batches   = batches,
                    BatchCounts = counts,
                });
            }
            return result;
        }
    }
}
