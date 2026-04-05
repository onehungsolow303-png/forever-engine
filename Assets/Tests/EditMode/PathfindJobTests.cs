using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.Tests
{
    public class PathfindJobTests
    {
        [Test]
        public void StraightLine_FindsPath()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(1, 4);
            targets[0] = new int2(6, 4);
            maxSteps[0] = 20;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsTrue(found[0]);
            Assert.AreEqual(2, nextSteps[0].x);
            Assert.AreEqual(4, nextSteps[0].y);

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }

        [Test]
        public void NavigatesAroundWall()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;
            for (int y = 2; y <= 5; y++) walk[y * w + 3] = false;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(1, 4);
            targets[0] = new int2(6, 4);
            maxSteps[0] = 30;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsTrue(found[0]);
            var step = nextSteps[0];
            Assert.IsTrue(walk[step.y * w + step.x], "First step must be walkable");

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }

        [Test]
        public void Adjacent_StaysPut()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(4, 4);
            targets[0] = new int2(4, 5);
            maxSteps[0] = 10;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsTrue(found[0]);
            Assert.AreEqual(starts[0], nextSteps[0]);

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }

        [Test]
        public void NoPath_Blocked()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;
            walk[3 * w + 4] = false; walk[5 * w + 4] = false;
            walk[4 * w + 3] = false; walk[4 * w + 5] = false;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(4, 4);
            targets[0] = new int2(0, 0);
            maxSteps[0] = 20;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsFalse(found[0]);

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }
    }
}
