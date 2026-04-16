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
        public int LoadRadius = 3;
        public int GenerateAheadRadius = 4;
        public int UnloadRadius = 6;

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
            public GameObject Props;
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
            if (Skeleton == null)
            {
                Skeleton = new PlanetSkeleton(WorldSeed);
                Debug.Log($"[ChunkManager] Skeleton generated: seed {WorldSeed}, {Skeleton.Width}x{Skeleton.Height}");
            }
            _lastPlayerChunk = new ChunkCoord(int.MinValue, 0); // Force initial load
            Debug.Log($"[ChunkManager] Initialized with player={playerTransform?.name ?? "null"}");
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
            // Phase 1: Generate + Load chunks — closest first, max 1 terrain per frame
            var needed = new List<(ChunkCoord coord, int dist)>();
            for (int dz = -GenerateAheadRadius; dz <= GenerateAheadRadius; dz++)
            {
                for (int dx = -GenerateAheadRadius; dx <= GenerateAheadRadius; dx++)
                {
                    var coord = new ChunkCoord(center.X + dx, center.Z + dz);
                    int dist = center.ChebyshevTo(coord);
                    if (dist <= GenerateAheadRadius && !_loaded.ContainsKey(coord) && !_generating.Contains(coord))
                        needed.Add((coord, dist));
                }
            }

            // Sort by distance — load closest chunks first
            needed.Sort((a, b) => a.dist.CompareTo(b.dist));

            foreach (var (coord, dist) in needed)
            {
                _generating.Add(coord);
                yield return StartCoroutine(LoadOrGenerateChunk(coord, dist <= LoadRadius));
                _generating.Remove(coord);
                // One chunk per frame when creating terrain to prevent stutter
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

            // Phase 3: Stitch terrain neighbors to eliminate seams
            StitchTerrainNeighbors();
        }

        /// <summary>Stitch a single terrain to its loaded neighbors.</summary>
        private void StitchSingleTerrain(ChunkCoord coord, Terrain terrain)
        {
            if (terrain == null) return;
            Terrain left = null, top = null, right = null, bottom = null;
            if (_loaded.TryGetValue(new ChunkCoord(coord.X - 1, coord.Z), out var lc)) left = lc.Terrain;
            if (_loaded.TryGetValue(new ChunkCoord(coord.X + 1, coord.Z), out var rc)) right = rc.Terrain;
            if (_loaded.TryGetValue(new ChunkCoord(coord.X, coord.Z + 1), out var tc)) top = tc.Terrain;
            if (_loaded.TryGetValue(new ChunkCoord(coord.X, coord.Z - 1), out var bc)) bottom = bc.Terrain;
            terrain.SetNeighbors(left, top, right, bottom);
            terrain.Flush();
        }

        /// <summary>
        /// Connect adjacent terrain chunks via Terrain.SetNeighbors so Unity
        /// stitches their edges together seamlessly (no visible seam lines).
        /// </summary>
        private void StitchTerrainNeighbors()
        {
            foreach (var kvp in _loaded)
            {
                if (kvp.Value.Terrain == null) continue;
                var coord = kvp.Key;

                Terrain left = null, top = null, right = null, bottom = null;
                var leftCoord = new ChunkCoord(coord.X - 1, coord.Z);
                var rightCoord = new ChunkCoord(coord.X + 1, coord.Z);
                var topCoord = new ChunkCoord(coord.X, coord.Z + 1);
                var bottomCoord = new ChunkCoord(coord.X, coord.Z - 1);

                if (_loaded.TryGetValue(leftCoord, out var lc)) left = lc.Terrain;
                if (_loaded.TryGetValue(rightCoord, out var rc)) right = rc.Terrain;
                if (_loaded.TryGetValue(topCoord, out var tc)) top = tc.Terrain;
                if (_loaded.TryGetValue(bottomCoord, out var bc)) bottom = bc.Terrain;

                kvp.Value.Terrain.SetNeighbors(left, top, right, bottom);
            }

            // Flush all terrains after stitching
            foreach (var kvp in _loaded)
                kvp.Value.Terrain?.Flush();
        }

        private IEnumerator LoadOrGenerateChunk(ChunkCoord coord, bool createTerrain)
        {
            ChunkData data = null;
            bool needsGeneration = !ChunkPersistence.Exists(WorldSeed, coord);

            if (!needsGeneration)
            {
                data = ChunkPersistence.Load(WorldSeed, coord);
            }
            else
            {
                // Heavy noise computation on background thread
                data = new ChunkData(coord.X, coord.Z);
                var skeleton = Skeleton;
                var seed = WorldSeed;
                var chunkData = data;
                bool done = false;

                System.Threading.Tasks.Task.Run(() =>
                {
                    TerrainGenerator.GenerateHeightmap(chunkData, skeleton, seed);
                    done = true;
                });

                // Wait for background thread to finish
                while (!done) yield return null;

                ChunkPersistence.Save(WorldSeed, coord, data);
            }

            if (createTerrain)
            {
                // Main thread: create terrain mesh (requires Unity API)
                var terrain = TerrainGenerator.CreateTerrain(data);
                _loaded[coord] = new LoadedChunk { Data = data, Terrain = terrain, Props = null };
                yield return null;

                // Stitch to neighbors
                StitchSingleTerrain(coord, terrain);
                yield return null;

                // Decoration
                var props = SurfaceDecorator.Decorate(data, terrain);
                _loaded[coord] = new LoadedChunk { Data = data, Terrain = terrain, Props = props };
                yield return null;
            }
            else
            {
                _loaded[coord] = new LoadedChunk { Data = data, Terrain = null, Props = null };
                yield return null;
            }
        }

        private void UnloadChunk(ChunkCoord coord)
        {
            if (!_loaded.TryGetValue(coord, out var chunk)) return;

            if (chunk.Props != null)
                SurfaceDecorator.RemoveDecoration(chunk.Props);
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
