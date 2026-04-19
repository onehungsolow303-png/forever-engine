using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.World;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Tests.EditMode.World
{
    public class VoxelWorldManagerQueueTests
    {
        [Test]
        public void PendingQueue_DrainsAtBoundedRate_PerUpdate()
        {
            var go = new GameObject("VoxelWorldMgr");
            try
            {
                var vwm = go.AddComponent<VoxelWorldManager>();
                // Override for deterministic assertions.
                vwm.MaxMeshBuildsPerFrame = 3;
                // Feed 10 homogenous-solid syncs (cheap to mesh; empty mesh -> no GO spawned,
                // but still counts toward the per-frame budget since it goes through the
                // renderer path).
                for (int x = 0; x < 10; x++)
                {
                    vwm.Streamer.OnSync(new VoxelChunkSync
                    {
                        Layer = 0, ChunkX = x, ChunkY = 1, ChunkZ = 0,
                        HomogenousMaterial = (byte)VoxelMaterial.Stone,
                    });
                }
                Assert.AreEqual(10, vwm.PendingBuildCount, "all 10 should be queued before Update");

                vwm.DrainPending();   // exposed for test; simulates one Update() tick
                Assert.AreEqual(7, vwm.PendingBuildCount, "3 processed, 7 remain after one tick");

                vwm.DrainPending();
                Assert.AreEqual(4, vwm.PendingBuildCount);

                vwm.DrainPending(); vwm.DrainPending();
                Assert.AreEqual(0, vwm.PendingBuildCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Depart_Cancels_PendingBuild()
        {
            var go = new GameObject("VoxelWorldMgr");
            try
            {
                var vwm = go.AddComponent<VoxelWorldManager>();
                vwm.MaxMeshBuildsPerFrame = 1;
                // Queue 3 syncs.
                for (int x = 0; x < 3; x++)
                {
                    vwm.Streamer.OnSync(new VoxelChunkSync
                    {
                        Layer = 0, ChunkX = x, ChunkY = 1, ChunkZ = 0,
                        HomogenousMaterial = (byte)VoxelMaterial.Stone,
                    });
                }
                // Depart the middle one BEFORE it's processed.
                vwm.Streamer.OnUnsubscribe(new VoxelChunkUnsubscribe
                {
                    Layer = 0, ChunkX = 1, ChunkY = 1, ChunkZ = 0,
                });
                Assert.AreEqual(2, vwm.PendingBuildCount, "departed chunk removed from queue");

                vwm.DrainPending();
                Assert.AreEqual(1, vwm.PendingBuildCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
