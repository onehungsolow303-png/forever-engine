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
        public int UnloadRadius = 5;

        [Header("World")]
        public int WorldSeed = 42;

        public PlanetSkeleton Skeleton { get; private set; }

        /// <summary>Number of currently loaded chunks (with or without mesh).</summary>
        public int LoadedChunkCount => _loaded.Count;

        /// <summary>True when receiving chunks from the server instead of generating locally.</summary>
        public bool ServerMode { get; private set; }

        private readonly Dictionary<ChunkCoord, LoadedChunk> _loaded = new();
        private readonly HashSet<ChunkCoord> _generating = new();
        private ChunkCoord _lastPlayerChunk;
        private Transform _playerTransform;
        private bool _updating;

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
        /// Receive a chunk from the server. Builds terrain mesh and decorations,
        /// then tracks the chunk as loaded. Skips chunks already present.
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

            // Build terrain mesh
            var terrainGO = TerrainGenerator.CreateTerrain(data);

            // Decorate with biome-appropriate props
            var props = SurfaceDecorator.DecorateMesh(data, terrainGO);

            // Track as loaded
            _loaded[coord] = new LoadedChunk
            {
                Data = data,
                TerrainGO = terrainGO,
                Props = props,
            };

            Debug.Log($"[ChunkManager] Server chunk loaded: {coord} biome={data.Biome}");
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
                    var terrainGO = TerrainGenerator.CreateTerrain(data);
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
