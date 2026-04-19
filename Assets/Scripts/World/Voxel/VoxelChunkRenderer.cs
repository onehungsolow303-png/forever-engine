using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    /// <summary>
    /// Owns a GameObject-per-chunk under <paramref name="root"/>, each with
    /// MeshFilter + MeshRenderer. Not a MonoBehaviour — owned by VoxelWorldManager.
    /// </summary>
    public sealed class VoxelChunkRenderer
    {
        private readonly Transform _root;
        private readonly Material _material;
        private readonly Dictionary<ChunkCoord3D, GameObject> _children = new();

        public VoxelChunkRenderer(Transform root, Material material)
        {
            _root = root;
            _material = material;
        }

        public void OnArrived(ChunkCoord3D coord, VoxelChunk chunk)
        {
            if (_children.TryGetValue(coord, out var existing))
            {
                // Replacement path — destroy the prior GameObject synchronously
                // so the new one takes its place in the same frame.
                Object.DestroyImmediate(existing);
                _children.Remove(coord);
            }

            var mesh = VoxelMeshBuilder.Build(chunk);
            if (mesh.vertexCount == 0) return;   // all air / all solid interior

            var go = new GameObject($"VC {coord}");
            go.transform.SetParent(_root, worldPositionStays: false);
            go.transform.position = new Vector3(
                coord.X * ChunkCoord3D.SizeMeters,
                coord.Y * ChunkCoord3D.SizeMeters,
                coord.Z * ChunkCoord3D.SizeMeters);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (_material != null) mr.sharedMaterial = _material;
            _children[coord] = go;
        }

        public void OnDeparted(ChunkCoord3D coord)
        {
            if (_children.TryGetValue(coord, out var go))
            {
                // DestroyImmediate required for EditMode tests; play-mode Unity
                // accepts it too. Standard Destroy() is deferred and would fail
                // the EditMode synchronous-child-count assertion.
                Object.DestroyImmediate(go);
                _children.Remove(coord);
            }
        }
    }
}
