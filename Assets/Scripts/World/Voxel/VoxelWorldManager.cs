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
