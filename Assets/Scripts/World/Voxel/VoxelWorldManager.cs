using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    public sealed class VoxelWorldManager : UnityEngine.MonoBehaviour
    {
        public Material voxelMaterial;

        /// <summary>
        /// Max chunks meshed into Unity Meshes per Update frame. Higher = faster
        /// initial catchup, lower = smoother frame pacing during chunk burst.
        /// </summary>
        [UnityEngine.Tooltip("Max chunks meshed per frame. Default 4 keeps ~20ms/frame budget.")]
        public int MaxMeshBuildsPerFrame = 4;

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
            _renderer = new VoxelChunkRenderer(transform, voxelMaterial);
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

        void Update() => DrainPending();

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
                // assertions are valid.
                if (_renderer != null)
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
