using System.Collections;
using System.Collections.Generic;
using ForeverEngine.Core.World.Baked;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Streams mesh-based terrain chunks around the player. Receives
    /// pre-generated ChunkData from the server via <see cref="ReceiveServerChunk"/>.
    /// </summary>
    public class ChunkManager : UnityEngine.MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("Streaming Radii (in chunks)")]
        public int LoadRadius = 2;
        // Must be >= server's GameLoop.MoveChunkRadius (currently 5) plus enough
        // hysteresis to survive a round-trip walk. At radius 12 the client keeps
        // 25*25 = 625 chunks resident (~8km across), which is plenty of slack
        // against the server's 11*11 = 121 streamed window without the bandwidth
        // storm of re-streaming already-delivered chunks on every chunk-cross.
        public int UnloadRadius = 12;

        /// <summary>Number of currently loaded chunks (with or without mesh).</summary>
        public int LoadedChunkCount => _loaded.Count;

        private readonly Dictionary<ChunkCoord, LoadedChunk> _loaded = new();
        private readonly Queue<ChunkData> _serverChunkQueue = new();
        private bool _processingServerQueue;
        private ChunkCoord _lastPlayerChunk;
        private Transform _playerTransform;
        private bool _updating;

        private const int ColliderRadius = 2;

        /// <summary>
        /// Chunks within Chebyshev distance ≤ ColliderRadius of the player's chunk
        /// get a MeshCollider for local physics. Distant chunks are render-only —
        /// the server owns authoritative collision via GetTerrainHeightAt.
        /// </summary>
        private bool ChunkNeedsCollider(ChunkCoord chunk)
        {
            if (_playerTransform == null) return true;  // Safe default when no player tracked.
            var pPos = _playerTransform.position;
            int pcx = Mathf.FloorToInt(pPos.x / ChunkCoord.ChunkSize);
            int pcz = Mathf.FloorToInt(pPos.z / ChunkCoord.ChunkSize);
            int dx = Mathf.Abs(chunk.X - pcx);
            int dz = Mathf.Abs(chunk.Z - pcz);
            return Mathf.Max(dx, dz) <= ColliderRadius;
        }

        private struct LoadedChunk
        {
            public ChunkData Data;
            public GameObject TerrainGO;
            public GameObject Props;
        }

        // Default true: the production world uses Gaia-baked planet (BakedTerrainTile-
        // Renderer) or voxel mesh as the visible surface; chunk-mesh terrain is the
        // legacy/fallback path that shouldn't render alongside. Without this default
        // the heightmap-mesh terrain renders ON TOP OF the baked tiles, producing
        // the visible "two worlds stacked" / doubled-render artifact (Bug #31, 2026-04-26).
        // Flip back to false in scene setup if you genuinely want raw chunk-mesh terrain
        // visible (no callers do as of this write).
        private bool _voxelTerrainActive = true;

        /// <summary>
        /// When true, the heightmap-based terrain MeshRenderers are kept disabled —
        /// the voxel world rendering OR baked-planet rendering takes over the visual
        /// surface. Props remain enabled so trees/rocks still show. Default is true
        /// because the production world is bake-once via Gaia; chunk-mesh terrain
        /// is legacy and would visually conflict with the bake.
        /// </summary>
        public bool VoxelTerrainActive
        {
            get => _voxelTerrainActive;
            set
            {
                if (_voxelTerrainActive == value) return;
                _voxelTerrainActive = value;
                ApplyTerrainVisibility();
            }
        }

        private void ApplyTerrainVisibility()
        {
            foreach (var kv in _loaded)
            {
                var terrain = kv.Value.TerrainGO;
                if (terrain != null)
                    foreach (var mr in terrain.GetComponentsInChildren<MeshRenderer>(true))
                        mr.enabled = !_voxelTerrainActive;
                // Procedural-fallback props (SurfaceDecorator output for chunks
                // without authored bake data) get hidden too — when bake or voxel
                // surface is the authoritative renderer, the bake's
                // BakedPropTileRenderer is the canonical prop source.
                var props = kv.Value.Props;
                if (props != null)
                    foreach (var mr in props.GetComponentsInChildren<MeshRenderer>(true))
                        mr.enabled = !_voxelTerrainActive;
            }
        }

        /// <summary>
        /// Test-only helper. Keeps the test alongside the production code without
        /// introducing a new internalsVisibleTo for the EditMode test asmdef.
        /// </summary>
        public void RegisterTerrainForTest(ChunkCoord coord, GameObject terrainGO)
        {
            _loaded[coord] = new LoadedChunk { Data = null, TerrainGO = terrainGO, Props = null };
            if (_voxelTerrainActive)
            {
                foreach (var mr in terrainGO.GetComponentsInChildren<MeshRenderer>(true))
                    mr.enabled = false;
            }
        }

        private void Awake() => Instance = this;

        /// <summary>
        /// Initialize the chunk manager. Server owns world generation; the client
        /// only streams and renders.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            _lastPlayerChunk = new ChunkCoord(int.MinValue, 0);
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            ChunkCoord playerChunk = ChunkCoord.FromWorldPos(_playerTransform.position);
            if (playerChunk != _lastPlayerChunk && !_updating)
            {
                Debug.Log($"[CHUNKMGR-DIAG] Boundary cross: {_lastPlayerChunk} -> {playerChunk}, loaded={_loaded.Count}");
                _lastPlayerChunk = playerChunk;
                StartCoroutine(UpdateChunks(playerChunk));
            }
            else if (playerChunk != _lastPlayerChunk && _updating)
            {
                Debug.Log($"[CHUNKMGR-DIAG] Boundary cross SUPPRESSED (_updating=true): at {playerChunk}");
            }
        }

        /// <summary>
        /// Receive a chunk from the server. Queues the chunk for async build —
        /// the actual mesh + collider cook + prop decoration is spread across
        /// subsequent frames to avoid main-thread hitches when multiple chunks
        /// arrive in the same tick.
        /// </summary>
        public void ReceiveServerChunk(ChunkData data)
        {
            var coord = new ChunkCoord(data.ChunkX, data.ChunkZ);

            if (_loaded.ContainsKey(coord))
                return; // already loaded

            _serverChunkQueue.Enqueue(data);
            if (!_processingServerQueue)
                StartCoroutine(ProcessServerChunkQueue());
        }

        private IEnumerator ProcessServerChunkQueue()
        {
            _processingServerQueue = true;
            while (_serverChunkQueue.Count > 0)
            {
                var data = _serverChunkQueue.Dequeue();
                var coord = new ChunkCoord(data.ChunkX, data.ChunkZ);

                if (_loaded.ContainsKey(coord))
                    continue;

                // Build the render + collider mesh (heaviest step — MeshCollider
                // cook at LOD 0 is a few ms of main-thread work). TerrainGenerator
                // now always adds a LOD0 collider regardless of the needsCollider
                // flag, so this call is already correct.
                var terrainGO = TerrainGenerator.CreateTerrain(data, needsCollider: ChunkNeedsCollider(coord));
                yield return null; // frame break before prop instantiation

                // Instantiate biome props (trees, rocks, foliage). Spreads the
                // hit for prop-heavy chunks across two frames total.
                // Baked path: use exact authored placements when the server
                // sent Props[]; fall back to procedural decoration otherwise.
                GameObject props;
                if (data.Props != null && data.Props.Count > 0)
                {
                    // Baked path: instantiate Gaia/authored placements exactly.
                    var propsParent = new GameObject($"Props_{data.ChunkX}_{data.ChunkZ}");
                    propsParent.transform.position = new ChunkCoord(data.ChunkX, data.ChunkZ).WorldOrigin;
                    var placements = new BakedPropPlacement[data.Props.Count];
                    for (int i = 0; i < data.Props.Count; i++) placements[i] = data.Props[i];
                    BakedPropRenderer.Render(placements, propsParent.transform);
                    props = propsParent;
                }
                else
                {
                    // Fallback: procedural biome decoration for chunks without a bake.
                    props = SurfaceDecorator.DecorateMesh(data, terrainGO);
                }
                _loaded[coord] = new LoadedChunk
                {
                    Data = data,
                    TerrainGO = terrainGO,
                    Props = props,
                };

                if (_voxelTerrainActive)
                {
                    if (terrainGO != null)
                        foreach (var mr in terrainGO.GetComponentsInChildren<MeshRenderer>(true))
                            mr.enabled = false;
                    // Hide procedural fallback props too — bake's
                    // BakedPropTileRenderer is authoritative when bake takes over.
                    if (props != null)
                        foreach (var mr in props.GetComponentsInChildren<MeshRenderer>(true))
                            mr.enabled = false;
                }

                Debug.Log($"[ChunkManager] Server chunk loaded: {coord} biome={data.Biome}");
                yield return null; // frame break between chunks in the queue
            }
            _processingServerQueue = false;
        }

        private IEnumerator UpdateChunks(ChunkCoord center)
        {
            _updating = true;
            UnloadDistantChunks(center);
            _updating = false;
            yield break;
        }

        /// <summary>
        /// Unload chunks beyond the unload radius from the player's current chunk.
        /// </summary>
        private void UnloadDistantChunks(ChunkCoord center)
        {
            var toUnload = new List<ChunkCoord>();
            foreach (var kvp in _loaded)
                if (center.ChebyshevTo(kvp.Key) > UnloadRadius)
                    toUnload.Add(kvp.Key);

            foreach (var coord in toUnload)
            {
                if (_loaded.TryGetValue(coord, out var chunk))
                {
                    if (chunk.Props != null) Object.Destroy(chunk.Props);
                    if (chunk.TerrainGO != null) Object.Destroy(chunk.TerrainGO);
                    _loaded.Remove(coord);
                }
            }
        }

        public ChunkData GetChunkData(ChunkCoord coord) =>
            _loaded.TryGetValue(coord, out var chunk) ? chunk.Data : null;

        public BiomeType GetBiomeAt(Vector3 worldPos)
        {
            var coord = ChunkCoord.FromWorldPos(worldPos);
            var data = GetChunkData(coord);
            return data != null ? data.Biome : BiomeType.Grassland;
        }

        public int LoadedCount => _loaded.Count;
    }
}
