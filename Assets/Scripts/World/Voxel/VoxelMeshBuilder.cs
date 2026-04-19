using UnityEngine;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    public static class VoxelMeshBuilder
    {
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
