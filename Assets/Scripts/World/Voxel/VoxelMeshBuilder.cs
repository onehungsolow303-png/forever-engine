using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    public static class VoxelMeshBuilder
    {
        // Reused across calls to eliminate GC pressure on the chunk-stream
        // hot path. NOT thread-safe; main-thread-only (as documented).
        [System.ThreadStatic] private static List<MeshVertex> _tlsMeshVerts;
        [System.ThreadStatic] private static List<int> _tlsIndices;
        [System.ThreadStatic] private static List<Vector3> _tlsPositions;

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
            var meshVerts = _tlsMeshVerts ??= new List<MeshVertex>(8192);
            var indices   = _tlsIndices   ??= new List<int>(24576);
            var positions = _tlsPositions ??= new List<Vector3>(8192);

            SurfaceNets.Mesh(chunk, meshVerts, indices);

            var mesh = new Mesh { name = $"VoxelChunk {chunk.Coord}" };
            if (meshVerts.Count == 0) return mesh;

            positions.Clear();
            for (int i = 0; i < meshVerts.Count; i++)
            {
                // v.Material intentionally dropped — Phase B will add submesh split or vertex colors.
                var v = meshVerts[i];
                positions.Add(new Vector3(v.X, v.Y, v.Z));
            }
            mesh.indexFormat = positions.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetTriangles(indices, submesh: 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
