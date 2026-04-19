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
        private readonly MeshPool _meshPool = new MeshPool(maxSize: 64);

        public VoxelChunkRenderer(Transform root, Material material)
        {
            _root = root;
            _material = material;
        }

        public void OnArrived(
            ChunkCoord3D coord,
            VoxelChunk chunk,
            VoxelChunk neighborPosX = null,
            VoxelChunk neighborPosY = null,
            VoxelChunk neighborPosZ = null)
        {
            if (_children.TryGetValue(coord, out var existing))
            {
                // Return the mesh to the pool BEFORE destroying the GameObject.
                DestroyGameObject(existing);
                _children.Remove(coord);
            }

            var rented = _meshPool.Rent();
            var mesh = VoxelMeshBuilder.Build(chunk, neighborPosX, neighborPosY, neighborPosZ, meshToReuse: rented);
            if (mesh.vertexCount == 0) { _meshPool.Return(rented); return; }

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
                // Return the mesh to the pool BEFORE destroying the GameObject.
                DestroyGameObject(go);
                _children.Remove(coord);
            }
        }

        private void DestroyGameObject(GameObject go)
        {
            // Return the mesh to the pool before destroying the GO so the
            // native Mesh handle can be reused for the next arriving chunk.
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) _meshPool.Return(mf.sharedMesh);
            // Play Mode: deferred frame-end cleanup (what Unity's frame pipeline expects).
            // Edit Mode (tests, editor tooling): synchronous so assertions are reliable.
            if (Application.isPlaying) Object.Destroy(go);
            else                       Object.DestroyImmediate(go);
        }
    }
}
