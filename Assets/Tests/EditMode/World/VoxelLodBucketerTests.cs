using NUnit.Framework;
using ForeverEngine.Core.World;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Tests.EditMode.World
{
    public class VoxelLodBucketerTests
    {
        [Test]
        public void CenterChunk_IsL0()
        {
            var b = VoxelLodBucketer.Bucket(
                center: new ChunkCoord3D(5, 0, 5),
                chunk:  new ChunkCoord3D(5, 0, 5),
                speed: 0f);
            Assert.AreEqual(LodBand.L0, b);
        }

        [Test]
        public void MidRange_IsL1()
        {
            var b = VoxelLodBucketer.Bucket(
                center: new ChunkCoord3D(0, 0, 0),
                chunk:  new ChunkCoord3D(4, 0, 0),
                speed: 0f);
            Assert.AreEqual(LodBand.L1, b);
        }

        [Test]
        public void FarRange_IsL2()
        {
            var b = VoxelLodBucketer.Bucket(
                center: new ChunkCoord3D(0, 0, 0),
                chunk:  new ChunkCoord3D(8, 0, 0),
                speed: 0f);
            Assert.AreEqual(LodBand.L2, b);
        }

        [Test]
        public void FastMovement_DropsAllToL2()
        {
            var b = VoxelLodBucketer.Bucket(
                center: new ChunkCoord3D(0, 0, 0),
                chunk:  new ChunkCoord3D(1, 0, 1),
                speed: 25f);
            Assert.AreEqual(LodBand.L2, b);
        }

        [Test]
        public void FarOutOfRange_IsHidden_EvenAtHighSpeed()
        {
            // Chebyshev distance 11 > L2Radius (10) → Hidden, and the speed
            // throttle must not rescue it (Hidden branch fires before speed check).
            var b1 = VoxelLodBucketer.Bucket(
                center: new ChunkCoord3D(0, 0, 0),
                chunk:  new ChunkCoord3D(11, 0, 0),
                speed: 0f);
            Assert.AreEqual(LodBand.Hidden, b1);

            var b2 = VoxelLodBucketer.Bucket(
                center: new ChunkCoord3D(0, 0, 0),
                chunk:  new ChunkCoord3D(11, 0, 0),
                speed: 50f);
            Assert.AreEqual(LodBand.Hidden, b2);
        }
    }
}
