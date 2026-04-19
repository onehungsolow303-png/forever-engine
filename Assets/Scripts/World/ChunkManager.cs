using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Streams mesh-based terrain chunks around the player.
    /// In multiplayer mode, receives pre-generated ChunkData from the server via
    /// <see cref="ReceiveServerChunk"/>. In offline/fallback mode, generates
    /// chunks locally using PlanetSkeleton + TerrainGenerator.
    /// </summary>
    public class ChunkManager : UnityEngine.MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("Streaming Radii (in chunks)")]
        public int LoadRadius = 2;
        public int GenerateAheadRadius = 3;
        // Must be >= server's GameLoop.MoveChunkRadius (currently 5) plus enough
        // hysteresis to survive a round-trip walk. At radius 12 the client keeps
        // 25*25 = 625 chunks resident (~8km across), which is plenty of slack
        // against the server's 11*11 = 121 streamed window without the bandwidth
        // storm of re-streaming already-delivered chunks on every chunk-cross.
        public int UnloadRadius = 12;

        [Header("World")]
        public int WorldSeed = 42;

        public PlanetSkeleton Skeleton { get; private set; }

        /// <summary>Number of currently loaded chunks (with or without mesh).</summary>
        public int LoadedChunkCount => _loaded.Count;

        /// <summary>True when receiving chunks from the server instead of generating locally.</summary>
        public bool ServerMode { get; private set; }

        private readonly Dictionary<ChunkCoord, LoadedChunk> _loaded = new();
        private readonly HashSet<ChunkCoord> _generating = new();
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

        private void Awake() => Instance = this;

        /// <summary>
        /// Initialize the chunk manager. In server mode, skeleton is not created
        /// (the server owns world generation). In offline mode, a local
        /// PlanetSkeleton is built from the seed for client-side generation.
        /// </summary>
        public void Initialize(Transform playerTransform, bool serverMode = false)
        {
            _playerTransform = playerTransform;
            ServerMode = serverMode;

            if (!ServerMode && Skeleton == null)
            {
                Skeleton = new PlanetSkeleton(WorldSeed);
                Debug.Log($"[ChunkManager] Skeleton generated: seed {WorldSeed}");
            }
            _lastPlayerChunk = new ChunkCoord(int.MinValue, 0);
        }

        /// <summary>
        /// Legacy overload — defaults to offline (local generation) mode.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            Initialize(playerTransform, serverMode: false);
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            ChunkCoord playerChunk = ChunkCoord.FromWorldPos(_playerTransform.position);
            if (playerChunk != _lastPlayerChunk && !_updating)
            {
                _lastPlayerChunk = playerChunk;
                StartCoroutine(UpdateChunks(playerChunk));
            }
        }

        /// <summary>
        /// Receive a chunk from the server. Queues the chunk for async build —
        /// the actual mesh + collider cook + prop decoration is spread across
        /// subsequent frames to avoid main-thread hitches when multiple chunks
        /// arrive in the same tick. Mirrors the frame-breaking pattern used
        /// by LocalGenerateChunks for the offline path.
        /// </summary>
        public void ReceiveServerChunk(ChunkData data)
        {
            // Auto-enable server mode on first received chunk
            if (!ServerMode)
            {
                ServerMode = true;
                Debug.Log("[ChunkManager] Server mode enabled — receiving chunks from server.");
            }

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
                var props = SurfaceDecorator.DecorateMesh(data, terrainGO);
                _loaded[coord] = new LoadedChunk
                {
                    Data = data,
                    TerrainGO = terrainGO,
                    Props = props,
                };

                Debug.Log($"[ChunkManager] Server chunk loaded: {coord} biome={data.Biome}");
                yield return null; // frame break between chunks in the queue
            }
            _processingServerQueue = false;
        }

        private IEnumerator UpdateChunks(ChunkCoord center)
        {
            _updating = true;

            // In server mode, the server pushes chunks — client only handles unloading
            if (!ServerMode)
            {
                yield return LocalGenerateChunks(center);
            }

            // Unload distant chunks (both modes)
            UnloadDistantChunks(center);

            _updating = false;
        }

        /// <summary>
        /// Local (offline/fallback) chunk generation — used when no server is connected.
        /// </summary>
        private IEnumerator LocalGenerateChunks(ChunkCoord center)
        {
            // Collect needed chunks, sorted closest first
            var needed = new List<(ChunkCoord coord, int dist)>();
            for (int dz = -GenerateAheadRadius; dz <= GenerateAheadRadius; dz++)
                for (int dx = -GenerateAheadRadius; dx <= GenerateAheadRadius; dx++)
                {
                    var coord = new ChunkCoord(center.X + dx, center.Z + dz);
                    int dist = center.ChebyshevTo(coord);
                    if (dist <= GenerateAheadRadius && !_loaded.ContainsKey(coord) && !_generating.Contains(coord))
                        needed.Add((coord, dist));
                }

            needed.Sort((a, b) => a.dist.CompareTo(b.dist));

            foreach (var (coord, dist) in needed)
            {
                _generating.Add(coord);

                // Generate or load data
                ChunkData data;
                if (ChunkPersistence.Exists(WorldSeed, coord))
                    data = ChunkPersistence.Load(WorldSeed, coord);
                else
                {
                    data = new ChunkData(coord.X, coord.Z);
                    TerrainGenerator.GenerateHeightmap(data, Skeleton, WorldSeed);
                    ChunkPersistence.Save(WorldSeed, coord, data);
                }

                yield return null; // Frame break after data generation

                if (dist <= LoadRadius)
                {
                    // Create mesh terrain
                    var terrainGO = TerrainGenerator.CreateTerrain(data, needsCollider: ChunkNeedsCollider(coord));
                    _loaded[coord] = new LoadedChunk { Data = data, TerrainGO = terrainGO, Props = null };

                    yield return null; // Frame break after mesh creation

                    // Decorate
                    var props = SurfaceDecorator.DecorateMesh(data, terrainGO);
                    _loaded[coord] = new LoadedChunk { Data = data, TerrainGO = terrainGO, Props = props };
                }
                else
                {
                    _loaded[coord] = new LoadedChunk { Data = data, TerrainGO = null, Props = null };
                }

                _generating.Remove(coord);
                yield return null; // Frame break between chunks
            }
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
            if (data != null) return data.Biome;

            // Fallback: sample from skeleton if available (offline mode only)
            if (Skeleton != null)
                return Skeleton.SampleAt(coord).Biome;

            return BiomeType.Grassland;
        }

        public int LoadedCount => _loaded.Count;
    }
}
