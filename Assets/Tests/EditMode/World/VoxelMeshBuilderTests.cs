using NUnit.Framework;
using ForeverEngine.Core.World;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Tests.EditMode.World
{
    public class VoxelMeshBuilderTests
    {
        [Test]
        public void Build_AllAirChunk_ReturnsEmptyMesh()
        {
            var c = new VoxelChunk(new ChunkCoord3D(0, 0, 0));
            var mesh = VoxelMeshBuilder.Build(c);
            Assert.AreEqual(0, mesh.vertexCount);
        }

        [Test]
        public void Build_HalfFilledChunk_ReturnsMeshWithTriangles()
        {
            var c = new VoxelChunk(new ChunkCoord3D(0, 0, 0));
            for (int y = 0; y < 32; y++)
            for (int z = 0; z < 64; z++)
            for (int x = 0; x < 64; x++)
                c.SetVoxel(x, y, z, sbyte.MinValue, VoxelMaterial.Stone);

            var mesh = VoxelMeshBuilder.Build(c);
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(mesh.triangles.Length, 0);
        }
    }
}
