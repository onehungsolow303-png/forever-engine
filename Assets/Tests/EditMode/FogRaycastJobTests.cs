using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.Tests
{
    public class FogRaycastJobTests
    {
        [Test]
        public void OpenRoom_AllVisible()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            var fog = new NativeArray<byte>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var job = new FogRaycastJob
            {
                PlayerX = 4, PlayerY = 4, SightRadius = 8,
                MapWidth = w, MapHeight = h,
                Walkability = walk, FogGrid = fog
            };
            job.Schedule(360, 36).Complete();

            Assert.AreEqual(2, fog[4 * w + 4]);
            Assert.AreEqual(2, fog[4 * w + 5]);

            walk.Dispose(); fog.Dispose();
        }

        [Test]
        public void WallBlocks_BehindWall_NotVisible()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            var fog = new NativeArray<byte>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;
            for (int y = 0; y < h; y++) walk[y * w + 5] = false;

            var job = new FogRaycastJob
            {
                PlayerX = 2, PlayerY = 4, SightRadius = 8,
                MapWidth = w, MapHeight = h,
                Walkability = walk, FogGrid = fog
            };
            job.Schedule(360, 36).Complete();

            Assert.AreEqual(2, fog[4 * w + 5]);
            Assert.AreEqual(0, fog[4 * w + 7]);

            walk.Dispose(); fog.Dispose();
        }

        [Test]
        public void DimJob_VisibleBecomesExplored()
        {
            int size = 16;
            var fog = new NativeArray<byte>(size, Allocator.TempJob);
            fog[0] = 2; fog[1] = 1; fog[2] = 0;

            new FogDimJob { FogGrid = fog }.Schedule(size, 8).Complete();

            Assert.AreEqual(1, fog[0]);
            Assert.AreEqual(1, fog[1]);
            Assert.AreEqual(0, fog[2]);

            fog.Dispose();
        }

        [Test]
        public void OutOfBounds_DoesNotCrash()
        {
            int w = 4, h = 4;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            var fog = new NativeArray<byte>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var job = new FogRaycastJob
            {
                PlayerX = 0, PlayerY = 0, SightRadius = 16,
                MapWidth = w, MapHeight = h,
                Walkability = walk, FogGrid = fog
            };

            Assert.DoesNotThrow(() => job.Schedule(360, 36).Complete());

            walk.Dispose(); fog.Dispose();
        }
    }
}
