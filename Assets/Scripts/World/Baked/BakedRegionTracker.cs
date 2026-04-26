using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Drives baked Unity Terrain + tree tile retain/release based on a
    /// position transform's distance to each tile, independent of voxel
    /// chunk arrival events.
    ///
    /// Why decoupled from VoxelChunkStreamer: voxel chunks won't arrive
    /// without a server connection, but baked terrain still needs to render
    /// in single-player and visual-test scenarios. The voxel layer is the
    /// future substrate for diggability — it's read-only delivery, NOT a
    /// render driver. See gap 4 in
    /// project_next_session_sub_c_quality_gaps.md.
    ///
    /// Usage: VoxelWorldManager (or any peer that constructs the renderers)
    /// calls Init(...) once renderers + index + the set of tiles that
    /// actually exist on disk are known.
    /// </summary>
    public sealed class BakedRegionTracker : UnityEngine.MonoBehaviour
    {
        [Tooltip("Transform whose XZ position drives tile retain. Auto-binds to Camera.main on first Update if null.")]
        public Transform Target;

        [Tooltip("Tiles whose centers are within this radius (meters) of the target's XZ are retained.")]
        public float RetainRadiusMeters = 1500f;

        [Tooltip("Re-evaluate the retain set this often (seconds). 0 = every frame.")]
        public float ReevalIntervalSeconds = 0.25f;

        private BakedTerrainTileRenderer _terrain;
        private BakedTreeInstanceRenderer _trees;
        private BakedPropTileRenderer _props;
        private BakedLayerIndex _index;
        private HashSet<(int, int)> _knownTiles;
        private readonly HashSet<(int, int)> _retained = new HashSet<(int, int)>();
        private readonly List<(int, int)> _scratchRelease = new List<(int, int)>();
        private float _nextEvalAt;
        private bool _initialized;

        public int RetainedCount => _retained.Count;

        public void Init(BakedTerrainTileRenderer terrain,
                         BakedTreeInstanceRenderer trees,
                         BakedPropTileRenderer props,
                         BakedLayerIndex index,
                         HashSet<(int, int)> knownTiles)
        {
            _terrain = terrain;
            _trees = trees;
            _props = props;
            _index = index;
            _knownTiles = knownTiles;
            _initialized = true;
            _nextEvalAt = 0f;
        }

        public void ReleaseAll()
        {
            if (!_initialized) return;
            foreach (var key in _retained)
            {
                _terrain?.ReleaseTile(key.Item1, key.Item2);
                _trees?.ReleaseTile(key.Item1, key.Item2);
                _props?.ReleaseTile(key.Item1, key.Item2);
            }
            _retained.Clear();
        }

        void Update()
        {
            if (!_initialized) return;
            if (Target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                Target = cam.transform;
            }
            if (Time.time < _nextEvalAt) return;
            _nextEvalAt = Time.time + Mathf.Max(0f, ReevalIntervalSeconds);
            Reevaluate(Target.position);
        }

        void OnDisable() => ReleaseAll();

        private void Reevaluate(Vector3 worldPos)
        {
            float tileSize = _index.TileSize;
            if (tileSize <= 0f) return;

            float originX = _index.Origin.X;
            float originZ = _index.Origin.Z;
            float r = RetainRadiusMeters;
            float r2 = r * r;

            int txMin = Mathf.FloorToInt((worldPos.x - r - originX) / tileSize);
            int txMax = Mathf.FloorToInt((worldPos.x + r - originX) / tileSize);
            int tzMin = Mathf.FloorToInt((worldPos.z - r - originZ) / tileSize);
            int tzMax = Mathf.FloorToInt((worldPos.z + r - originZ) / tileSize);

            _scratchRelease.Clear();
            foreach (var key in _retained)
            {
                bool inBox = key.Item1 >= txMin && key.Item1 <= txMax
                          && key.Item2 >= tzMin && key.Item2 <= tzMax;
                if (!inBox) { _scratchRelease.Add(key); continue; }
                float cx = originX + (key.Item1 + 0.5f) * tileSize;
                float cz = originZ + (key.Item2 + 0.5f) * tileSize;
                float dx = cx - worldPos.x;
                float dz = cz - worldPos.z;
                if (dx * dx + dz * dz > r2) _scratchRelease.Add(key);
            }
            for (int i = 0; i < _scratchRelease.Count; i++)
            {
                var key = _scratchRelease[i];
                _terrain?.ReleaseTile(key.Item1, key.Item2);
                _trees?.ReleaseTile(key.Item1, key.Item2);
                _props?.ReleaseTile(key.Item1, key.Item2);
                _retained.Remove(key);
            }

            for (int tx = txMin; tx <= txMax; tx++)
            for (int tz = tzMin; tz <= tzMax; tz++)
            {
                var key = (tx, tz);
                if (_retained.Contains(key)) continue;
                if (_knownTiles != null && !_knownTiles.Contains(key)) continue;
                float cx = originX + (tx + 0.5f) * tileSize;
                float cz = originZ + (tz + 0.5f) * tileSize;
                float dx = cx - worldPos.x;
                float dz = cz - worldPos.z;
                if (dx * dx + dz * dz > r2) continue;
                _terrain?.RetainTile(tx, tz);
                _trees?.RetainTile(tx, tz);
                _props?.RetainTile(tx, tz);
                _retained.Add(key);
            }
        }
    }
}
