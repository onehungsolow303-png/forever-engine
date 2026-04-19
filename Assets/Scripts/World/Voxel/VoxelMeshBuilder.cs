using UnityEngine;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    public static class VoxelMeshBuilder
    {
        /// <summary>
        /// Builds a Unity Mesh from a voxel chunk using Core's Surface Nets mesher.
        /// </summary>
        /// <remarks>
        /// Must be called from the Unity main thread. <see cref="Mesh"/> construction
        /// is not thread-safe. Background streaming callbacks must marshal to the main
        /// thread before calling this.
        /// </remarks>
        public static Mesh Build(VoxelChunk chunk)
        {
            var voxelMesh = SurfaceNets.Mesh(chunk);
            var mesh = new Mesh { name = $"VoxelChunk {chunk.Coord}" };
            if (voxelMesh.Vertices.Length == 0) return mesh;

            // 1m voxel pitch → vertex positions in meters, chunk-local.
            var verts = new Vector3[voxelMesh.Vertices.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = voxelMesh.Vertices[i];
                // v.Material intentionally dropped — Phase B will add submesh split or vertex colors.
                verts[i] = new Vector3(v.X, v.Y, v.Z);
            }
            mesh.indexFormat = verts.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(voxelMesh.Indices, submesh: 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
