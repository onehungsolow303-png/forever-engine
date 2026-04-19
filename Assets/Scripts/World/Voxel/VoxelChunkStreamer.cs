using System.Collections.Generic;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    /// <summary>
    /// Pure C# (not a MonoBehaviour) so it's unit-testable. Lives as a field
    /// on a MonoBehaviour wrapper (VoxelWorldManager in Task 25). Receives
    /// server sync messages, maintains the local chunk cache, emits events
    /// when chunks arrive/leave for the renderer to pick up.
    /// </summary>
    public sealed class VoxelChunkStreamer
    {
        private readonly Dictionary<ChunkCoord3D, VoxelChunk> _chunks =
            new Dictionary<ChunkCoord3D, VoxelChunk>();

        public event System.Action<ChunkCoord3D> ChunkArrived;
        public event System.Action<ChunkCoord3D> ChunkDeparted;

        public VoxelChunk Get(ChunkCoord3D coord) =>
            _chunks.TryGetValue(coord, out var c) ? c : null;

        public IReadOnlyList<(ChunkCoord3D coord, VoxelChunk chunk)> All()
        {
            // Snapshot to avoid "Collection modified" if a message arrives mid-iteration.
            var list = new List<(ChunkCoord3D, VoxelChunk)>(_chunks.Count);
            foreach (var kv in _chunks)
                list.Add((kv.Key, kv.Value));
            return list;
        }

        public void OnSync(VoxelChunkSync msg)
        {
            var coord = new ChunkCoord3D(msg.ChunkX, msg.ChunkY, msg.ChunkZ);
            VoxelChunk chunk;
            if (msg.HomogenousMaterial.HasValue)
            {
                chunk = new VoxelChunk(coord);
                if (msg.HomogenousMaterial.Value != (byte)VoxelMaterial.Air)
                    chunk.FillSolid((VoxelMaterial)msg.HomogenousMaterial.Value);
            }
            else if (!string.IsNullOrEmpty(msg.VoxelBlobBase64))
            {
                var bytes = System.Convert.FromBase64String(msg.VoxelBlobBase64);
                chunk = VoxelChunk.FromGzipBytes(coord, bytes);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"[VoxelChunkStreamer] Malformed VoxelChunkSync for chunk ({msg.ChunkX},{msg.ChunkY},{msg.ChunkZ}): " +
                    $"neither HomogenousMaterial nor VoxelBlobBase64 set; dropping.");
                return;
            }
            _chunks[coord] = chunk;
            ChunkArrived?.Invoke(coord);
        }

        public void OnUnsubscribe(VoxelChunkUnsubscribe msg)
        {
            var coord = new ChunkCoord3D(msg.ChunkX, msg.ChunkY, msg.ChunkZ);
            if (_chunks.Remove(coord)) ChunkDeparted?.Invoke(coord);
        }
    }
}
