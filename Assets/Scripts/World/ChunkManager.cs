// Assets/Scripts/World/ChunkManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Streams chunks around the player. Loads within LoadRadius, generates ahead
    /// within GenerateRadius, unloads beyond UnloadRadius. Persists chunks to disk.
    /// </summary>
    public class ChunkManager : UnityEngine.MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("Streaming Radii (in chunks)")]
        public int LoadRadius = 5;
        public int GenerateAheadRadius = 6;
        public int UnloadRadius = 8;

        [Header("World")]
        public int WorldSeed = 42;

        public PlanetSkeleton Skeleton { get; private set; }

        private readonly Dictionary<ChunkCoord, LoadedChunk> _loaded = new();
        private readonly HashSet<ChunkCoord> _generating = new();
        private ChunkCoord _lastPlayerChunk;
        private Transform _playerTransform;

        private struct LoadedChunk
        {
            public ChunkData Data;
            public Terrain Terrain;
        }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Initialize the chunk system. Call once after setting WorldSeed.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            Skeleton = new PlanetSkeleton(WorldSeed);
            _lastPlayerChunk = new ChunkCoord(int.MinValue, 0); // Force initial load
            Debug.Log($"[ChunkManager] Initialized with seed {WorldSeed}, skeleton {Skeleton.Width}x{Skeleton.Height}");
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            ChunkCoord playerChunk = ChunkCoord.FromWorldPos(_playerTransform.position);

            if (playerChunk != _lastPlayerChunk)
            {
                _lastPlayerChunk = playerChunk;
                StartCoroutine(UpdateChunks(playerChunk));
            }
        }

        private IEnumerator UpdateChunks(ChunkCoord center)
        {
            // Phase 1: Generate + Load chunks within radius
            for (int dz = -GenerateAheadRadius; dz <= GenerateAheadRadius; dz++)
            {
                for (int dx = -GenerateAheadRadius; dx <= GenerateAheadRadius; dx++)
                {
                    var coord = new ChunkCoord(center.X + dx, center.Z + dz);
                    int dist = center.ChebyshevTo(coord);

                    if (dist <= GenerateAheadRadius && !_loaded.ContainsKey(coord) && !_generating.Contains(coord))
                    {
                        _generating.Add(coord);
                        yield return StartCoroutine(LoadOrGenerateChunk(coord, dist <= LoadRadius));
                        _generating.Remove(coord);
                    }
                }
            }

            // Phase 2: Unload distant chunks
            var toUnload = new List<ChunkCoord>();
            foreach (var kvp in _loaded)
            {
                if (center.ChebyshevTo(kvp.Key) > UnloadRadius)
                    toUnload.Add(kvp.Key);
            }

            foreach (var coord in toUnload)
                UnloadChunk(coord);
        }

        private IEnumerator LoadOrGenerateChunk(ChunkCoord coord, bool createTerrain)
        {
            ChunkData data;

            // Try loading from disk first
            if (ChunkPersistence.Exists(WorldSeed, coord))
            {
                data = ChunkPersistence.Load(WorldSeed, coord);
            }
            else
            {
                // Generate new chunk
                data = new ChunkData(coord.X, coord.Z);
                TerrainGenerator.GenerateHeightmap(data, Skeleton, WorldSeed);
                ChunkPersistence.Save(WorldSeed, coord, data);
            }

            if (createTerrain)
            {
                var terrain = TerrainGenerator.CreateTerrain(data);
                _loaded[coord] = new LoadedChunk { Data = data, Terrain = terrain };
            }
            else
            {
                // Generate-ahead: data on disk but no terrain mesh yet
                _loaded[coord] = new LoadedChunk { Data = data, Terrain = null };
            }

            yield return null; // Spread across frames
        }

        private void UnloadChunk(ChunkCoord coord)
        {
            if (!_loaded.TryGetValue(coord, out var chunk)) return;

            if (chunk.Terrain != null)
                TerrainGenerator.DestroyTerrain(chunk.Terrain);

            _loaded.Remove(coord);
        }

        /// <summary>Get the ChunkData at a coordinate (if loaded).</summary>
        public ChunkData GetChunkData(ChunkCoord coord) =>
            _loaded.TryGetValue(coord, out var chunk) ? chunk.Data : null;

        /// <summary>Get the biome at a world position.</summary>
        public BiomeType GetBiomeAt(Vector3 worldPos)
        {
            var coord = ChunkCoord.FromWorldPos(worldPos);
            var data = GetChunkData(coord);
            if (data != null) return data.Biome;
            return Skeleton.SampleAt(coord).Biome;
        }

        /// <summary>Number of currently loaded chunks.</summary>
        public int LoadedCount => _loaded.Count;
    }
}
