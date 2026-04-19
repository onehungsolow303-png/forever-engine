using UnityEngine;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    public sealed class VoxelWorldManager : UnityEngine.MonoBehaviour
    {
        public Material voxelMaterial;

        public VoxelChunkStreamer Streamer { get; private set; }
        private VoxelChunkRenderer _renderer;

        void Awake()
        {
            Streamer = new VoxelChunkStreamer();
            _renderer = new VoxelChunkRenderer(transform, voxelMaterial);
            Streamer.ChunkArrived += OnArrived;
            Streamer.ChunkDeparted += OnDeparted;
        }

        void OnDestroy()
        {
            if (Streamer != null)
            {
                Streamer.ChunkArrived -= OnArrived;
                Streamer.ChunkDeparted -= OnDeparted;
            }
        }

        private void OnArrived(ChunkCoord3D coord)
        {
            var chunk = Streamer.Get(coord);
            if (chunk != null) _renderer.OnArrived(coord, chunk);
        }

        private void OnDeparted(ChunkCoord3D coord) => _renderer.OnDeparted(coord);
    }
}
