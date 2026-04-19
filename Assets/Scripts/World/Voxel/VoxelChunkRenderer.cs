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

        public void OnArrived(
            ChunkCoord3D coord,
            VoxelChunk chunk,
            VoxelChunk neighborPosX = null,
            VoxelChunk neighborPosY = null,
            VoxelChunk neighborPosZ = null)
        {
            if (_children.TryGetValue(coord, out var existing))
            {
                // Drop the prior GameObject; conditional destroy keeps Play Mode quiet and
                // EditMode tests synchronous.
                DestroyGameObject(existing);
                _children.Remove(coord);
            }

            var mesh = VoxelMeshBuilder.Build(chunk, neighborPosX, neighborPosY, neighborPosZ);
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
                // Drop the prior GameObject; conditional destroy keeps Play Mode quiet and
                // EditMode tests synchronous.
                DestroyGameObject(go);
                _children.Remove(coord);
            }
        }

        private static void DestroyGameObject(GameObject go)
        {
            // Play Mode: deferred frame-end cleanup (what Unity's frame pipeline expects).
            // Edit Mode (tests, editor tooling): synchronous so assertions are reliable.
            if (Application.isPlaying) Object.Destroy(go);
            else                       Object.DestroyImmediate(go);
        }
    }
}
