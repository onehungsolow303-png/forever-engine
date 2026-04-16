using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Streams mesh-based terrain chunks around the player.
    /// Uses simple mesh planes (not Unity Terrain) for fast creation/destruction.
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

        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            if (Skeleton == null)
            {
                Skeleton = new PlanetSkeleton(WorldSeed);
                Debug.Log($"[ChunkManager] Skeleton generated: seed {WorldSeed}");
            }
            _lastPlayerChunk = new ChunkCoord(int.MinValue, 0);
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

        private IEnumerator UpdateChunks(ChunkCoord center)
        {
            _updating = true;

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

            // Unload distant chunks
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

            _updating = false;
        }

        public ChunkData GetChunkData(ChunkCoord coord) =>
            _loaded.TryGetValue(coord, out var chunk) ? chunk.Data : null;

        public BiomeType GetBiomeAt(Vector3 worldPos)
        {
            var coord = ChunkCoord.FromWorldPos(worldPos);
            var data = GetChunkData(coord);
            return data != null ? data.Biome : Skeleton.SampleAt(coord).Biome;
        }

        public int LoadedCount => _loaded.Count;
    }
}
