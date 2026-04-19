using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Core.World;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Tests.EditMode.World
{
    public class VoxelChunkRendererTests
    {
        [Test]
        public void Render_AllSolidChunk_BuildsGameObject_WithMesh()
        {
            var parent = new GameObject("VoxelRoot");
            try
            {
                var renderer = new VoxelChunkRenderer(parent.transform, null);
                var c = new VoxelChunk(new ChunkCoord3D(0, 0, 0));
                // Sphere carves out a surface inside the chunk.
                for (int z = 0; z < 64; z++)
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 32, dy = y - 32, dz = z - 32;
                    if (dx*dx + dy*dy + dz*dz < 400) c.SetVoxel(x, y, z, sbyte.MinValue, VoxelMaterial.Stone);
                }
                renderer.OnArrived(new ChunkCoord3D(0, 0, 0), c);
                Assert.AreEqual(1, parent.transform.childCount);
                var mf = parent.transform.GetChild(0).GetComponent<MeshFilter>();
                Assert.IsNotNull(mf);
                Assert.Greater(mf.sharedMesh.vertexCount, 0);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Render_ArriveThenDepart_RemovesGameObject()
        {
            var parent = new GameObject("VoxelRoot");
            try
            {
                var renderer = new VoxelChunkRenderer(parent.transform, null);
                var c = new VoxelChunk(new ChunkCoord3D(1, 0, 0));
                // Half-solid so we generate a real mesh (all-solid interior would be empty).
                for (int y = 0; y < 32; y++)
                for (int z = 0; z < 64; z++)
                for (int x = 0; x < 64; x++)
                    c.SetVoxel(x, y, z, sbyte.MinValue, VoxelMaterial.Stone);
                renderer.OnArrived(new ChunkCoord3D(1, 0, 0), c);
                Assert.AreEqual(1, parent.transform.childCount);
                renderer.OnDeparted(new ChunkCoord3D(1, 0, 0));
                // Destroy is deferred; DestroyImmediate for EditMode tests.
                Assert.AreEqual(0, parent.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Render_ArriveTwiceForSameCoord_ReplacesExistingGameObject()
        {
            var parent = new GameObject("VoxelRoot");
            try
            {
                var renderer = new VoxelChunkRenderer(parent.transform, null);
                var coord = new ChunkCoord3D(2, 0, 0);

                // First arrive — half-solid chunk.
                var chunkA = new VoxelChunk(coord);
                for (int y = 0; y < 32; y++)
                for (int z = 0; z < 64; z++)
                for (int x = 0; x < 64; x++)
                    chunkA.SetVoxel(x, y, z, sbyte.MinValue, VoxelMaterial.Stone);
                renderer.OnArrived(coord, chunkA);
                Assert.AreEqual(1, parent.transform.childCount);
                var firstGo = parent.transform.GetChild(0).gameObject;

                // Second arrive for the SAME coord — different slice (quarter-solid).
                var chunkB = new VoxelChunk(coord);
                for (int y = 0; y < 16; y++)
                for (int z = 0; z < 64; z++)
                for (int x = 0; x < 64; x++)
                    chunkB.SetVoxel(x, y, z, sbyte.MinValue, VoxelMaterial.Stone);
                renderer.OnArrived(coord, chunkB);

                // childCount stays at 1; old GameObject was destroyed and replaced.
                Assert.AreEqual(1, parent.transform.childCount);
                var secondGo = parent.transform.GetChild(0).gameObject;
                Assert.AreNotEqual(firstGo.GetInstanceID(), secondGo.GetInstanceID(),
                    "replacement should be a new GameObject, not the same one reused");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
