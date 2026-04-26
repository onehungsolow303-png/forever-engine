using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ForeverEngine.Core.World;
using ForeverEngine.Core.World.Baked;
using ForeverEngine.Procedural;

namespace ForeverEngine.World.Voxel
{
    public sealed class VoxelWorldManager : UnityEngine.MonoBehaviour
    {
        public Material voxelMaterial;

        [UnityEngine.Tooltip("Render baked Unity Terrain + tree instances for tiles overlapping streamed chunks. Sub C runtime-bake-extension.")]
        public bool RenderBakedTiles = true;

        [UnityEngine.Tooltip("Where the baked planet lives. Falls back to StreamingAssets if missing.")]
        public string BakedLayerDir = "C:/Dev/.shared/baked/planet/layer_0";

        [UnityEngine.Tooltip("Sub C visual-test mode: retain every known baked tile on Start so the world is visible without a running server. Default OFF — production drives tiles via the BakedRegionTracker (distance-based) instead.")]
        public bool LoadAllBakedTilesOnStart = false;

        [UnityEngine.Tooltip("Tile retain radius (meters) for the auto-attached BakedRegionTracker. Ignored when LoadAllBakedTilesOnStart is true.")]
        public float BakedTileRetainRadius = 1500f;

        /// <summary>
        /// Max chunks meshed into Unity Meshes per Update frame. Higher = faster
        /// initial catchup, lower = smoother frame pacing during chunk burst.
        /// </summary>
        [UnityEngine.Tooltip("Max chunks meshed per frame. Default 2 keeps the upload sync below ~10ms/frame at 60 FPS.")]
        public int MaxMeshBuildsPerFrame = 2;

        [UnityEngine.Tooltip("Draw the voxel mesh AND build it. Default OFF — voxel data still streams for future carving + collision, but meshes are skipped entirely because Surface-Nets + 3× neighbor re-mesh per chunk costs ~15ms each, which at 2205-chunk worst-case subscription tanks the main thread to 3-5 FPS even though nothing would be visible.")]
        public bool RenderMeshes = false;

        private readonly List<ChunkCoord3D> _pendingArrived = new List<ChunkCoord3D>();
        private readonly HashSet<ChunkCoord3D> _pendingSet = new HashSet<ChunkCoord3D>();

        public int PendingBuildCount => _pendingArrived.Count;

        // Streamer is initialized at declaration and events are wired immediately so the
        // queue/drain path works as soon as AddComponent<VoxelWorldManager>() returns,
        // even in EditMode [Test] methods where Awake may not have fired yet.
        private VoxelChunkStreamer _streamer;
        public VoxelChunkStreamer Streamer
        {
            get
            {
                if (_streamer == null) EnsureStreamer();
                return _streamer;
            }
        }
        private VoxelChunkRenderer _renderer;
        private BakedTerrainTileRenderer _bakedTileRenderer;
        private BakedTreeInstanceRenderer _bakedTreeRenderer;
        private BakedPropTileRenderer _bakedPropRenderer;
        private BakedRegionTracker _bakedRegionTracker;
        private BakedLayerIndex _bakedIndex;
        private HashSet<(int, int)> _bakedTileSet;

        private void EnsureStreamer()
        {
            if (_streamer != null) return;
            _streamer = new VoxelChunkStreamer();
            _streamer.ChunkArrived += OnArrived;
            _streamer.ChunkDeparted += OnDeparted;
        }

        void Awake()
        {
            EnsureStreamer();
            // Fallback material if none assigned in the Inspector / via scene placement.
            // Phase A placeholder — Phase B will wire a triplanar material from the
            // skeleton's material palette.
            if (voxelMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    voxelMaterial = new Material(shader) { name = "VoxelDefault" };
                    // Earthy brown-grey so it reads as dirt/stone against the sky.
                    voxelMaterial.color = new Color(0.55f, 0.45f, 0.35f);
                }
                else
                {
                    Debug.LogWarning("[VoxelWorldManager] No URP/Lit or Standard shader found; voxel meshes will render with Unity's missing-material pink.");
                }
            }
            _renderer = new VoxelChunkRenderer(transform, voxelMaterial, renderMeshes: RenderMeshes);

            if (RenderBakedTiles) TryInitBakedTileRenderer();
        }

        void Start()
        {
            if (_bakedTileSet == null) return;

            if (LoadAllBakedTilesOnStart)
            {
                foreach (var (tx, tz) in _bakedTileSet)
                {
                    _bakedTileRenderer?.RetainTile(tx, tz);
                    _bakedTreeRenderer?.RetainTile(tx, tz);
                    _bakedPropRenderer?.RetainTile(tx, tz);
                }
                Debug.Log($"[VoxelWorldManager] retained all {_bakedTileSet.Count} baked tile(s) for visual test mode");
                PositionMainCameraOverWorldCenter();
                return;
            }

            // Production path: distance-based tile retention via the tracker.
            // Decoupled from VoxelChunkStreamer so baked terrain renders even
            // without a server connection. Auto-attached so existing scenes
            // (which only have VoxelWorldManager) work without an editor pass.
            _bakedRegionTracker = gameObject.GetComponent<BakedRegionTracker>()
                                  ?? gameObject.AddComponent<BakedRegionTracker>();
            _bakedRegionTracker.RetainRadiusMeters = BakedTileRetainRadius;
            _bakedRegionTracker.Init(_bakedTileRenderer, _bakedTreeRenderer, _bakedPropRenderer, _bakedIndex, _bakedTileSet);
        }

        private void PositionMainCameraOverWorldCenter()
        {
            if (_bakedIndex.Tiles == null) return;
            float worldMinX = _bakedIndex.Origin.X + _bakedIndex.Grid.MinTileX * _bakedIndex.TileSize;
            float worldMinZ = _bakedIndex.Origin.Z + _bakedIndex.Grid.MinTileZ * _bakedIndex.TileSize;
            float worldMaxX = _bakedIndex.Origin.X + (_bakedIndex.Grid.MaxTileX + 1) * _bakedIndex.TileSize;
            float worldMaxZ = _bakedIndex.Origin.Z + (_bakedIndex.Grid.MaxTileZ + 1) * _bakedIndex.TileSize;
            float centerX = (worldMinX + worldMaxX) * 0.5f;
            float centerZ = (worldMinZ + worldMaxZ) * 0.5f;
            float worldSpanX = worldMaxX - worldMinX;
            float worldSpanZ = worldMaxZ - worldMinZ;
            float span = Mathf.Max(worldSpanX, worldSpanZ);
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(centerX, span * 0.6f, centerZ - span * 0.6f);
            cam.transform.LookAt(new Vector3(centerX, 0f, centerZ));
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, span * 4f);
            Debug.Log($"[VoxelWorldManager] camera positioned at {cam.transform.position} looking at center ({centerX},{centerZ}); world bounds X[{worldMinX}..{worldMaxX}] Z[{worldMinZ}..{worldMaxZ}]");
        }

        private void TryInitBakedTileRenderer()
        {
            string layerDir = BakedLayerDir;
            if (!File.Exists(Path.Combine(layerDir, "index.json")))
                layerDir = Path.Combine(Application.streamingAssetsPath, "baked", "planet", "layer_0");
            if (!File.Exists(Path.Combine(layerDir, "index.json")))
            {
                Debug.LogWarning("[VoxelWorldManager] no baked layer index found; baked tile/tree rendering disabled");
                return;
            }
            BakedLayerIndex index;
            try { index = BakedWorldReader.LoadLayerIndex(layerDir); }
            catch (System.Exception e) { Debug.LogError($"[VoxelWorldManager] LoadLayerIndex failed: {e.Message}"); return; }

            var registry = BakedAssetRegistry.Load();
            _bakedIndex = index;
            _bakedTileSet = new HashSet<(int, int)>();
            foreach (var t in index.Tiles ?? System.Array.Empty<BakedLayerTileEntry>())
                _bakedTileSet.Add((t.TileX, t.TileZ));

            _bakedTileRenderer = new BakedTerrainTileRenderer(transform, layerDir, index, registry);
            _bakedTreeRenderer = new BakedTreeInstanceRenderer(layerDir, index, registry);
            _bakedPropRenderer = new BakedPropTileRenderer(transform, layerDir, registry);
            Debug.Log($"[VoxelWorldManager] baked tile renderer ready: {_bakedTileSet.Count} tile(s), registry={(registry != null ? "yes" : "MISSING")}");
        }

        void OnDestroy()
        {
            if (_streamer != null)
            {
                _streamer.ChunkArrived -= OnArrived;
                _streamer.ChunkDeparted -= OnDeparted;
            }
        }

        private void OnArrived(ChunkCoord3D coord)
        {
            // Enqueue for bounded-rate processing in Update(); avoids freezing on
            // the initial ~441-chunk subscription burst.
            if (_pendingSet.Add(coord))
                _pendingArrived.Add(coord);
        }

        private void OnDeparted(ChunkCoord3D coord)
        {
            // If the chunk was queued but not yet meshed, cancel — no GameObject
            // exists to destroy.
            if (_pendingSet.Remove(coord))
                _pendingArrived.Remove(coord);
            else
                _renderer?.OnDeparted(coord);
        }

        void Update()
        {
            DrainPending();
            _bakedTreeRenderer?.Render();
        }

        /// <summary>
        /// Drains up to <see cref="MaxMeshBuildsPerFrame"/> queued chunks from the
        /// pending set. Public for test access; Update() calls this once per frame.
        /// </summary>
        public void DrainPending()
        {
            int budget = MaxMeshBuildsPerFrame;
            int processed = 0;
            while (processed < budget && _pendingArrived.Count > 0)
            {
                var coord = _pendingArrived[0];
                _pendingArrived.RemoveAt(0);
                _pendingSet.Remove(coord);
                // _renderer is null in EditMode tests where Awake hasn't fired; skip
                // the mesh-build step but still consume the budget slot so queue-drain
                // assertions are valid. Also skip when RenderMeshes=false — building
                // meshes we won't render wastes 15ms/chunk × 2205 chunks worst-case.
                // Voxel data remains cached in Streamer for future carving/collision.
                if (_renderer != null && RenderMeshes)
                {
                    var chunk = Streamer.Get(coord);
                    if (chunk != null)
                    {
                        var nx = Streamer.Get(new ChunkCoord3D(coord.X + 1, coord.Y, coord.Z));
                        var ny = Streamer.Get(new ChunkCoord3D(coord.X, coord.Y + 1, coord.Z));
                        var nz = Streamer.Get(new ChunkCoord3D(coord.X, coord.Y, coord.Z + 1));
                        _renderer.OnArrived(coord, chunk, nx, ny, nz);

                        // Re-mesh -X / -Y / -Z neighbors so THEIR boundary closes against us.
                        ReMeshIfLoaded(new ChunkCoord3D(coord.X - 1, coord.Y, coord.Z));
                        ReMeshIfLoaded(new ChunkCoord3D(coord.X, coord.Y - 1, coord.Z));
                        ReMeshIfLoaded(new ChunkCoord3D(coord.X, coord.Y, coord.Z - 1));
                    }
                }
                processed++;
            }
        }

        private void ReMeshIfLoaded(ChunkCoord3D coord)
        {
            var c = Streamer.Get(coord);
            if (c == null) return;
            var nx = Streamer.Get(new ChunkCoord3D(coord.X + 1, coord.Y, coord.Z));
            var ny = Streamer.Get(new ChunkCoord3D(coord.X, coord.Y + 1, coord.Z));
            var nz = Streamer.Get(new ChunkCoord3D(coord.X, coord.Y, coord.Z + 1));
            _renderer.OnArrived(coord, c, nx, ny, nz);
        }
    }
}
