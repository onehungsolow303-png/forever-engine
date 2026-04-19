using NUnit.Framework;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.World;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Tests.EditMode.World
{
    public class VoxelChunkStreamerTests
    {
        [Test]
        public void OnVoxelChunkSync_Homogenous_AddsChunkToStore()
        {
            var streamer = new VoxelChunkStreamer();
            streamer.OnSync(new VoxelChunkSync
            {
                Layer = 0, ChunkX = 0, ChunkY = 0, ChunkZ = 0,
                HomogenousMaterial = (byte)VoxelMaterial.Stone,
            });
            Assert.IsNotNull(streamer.Get(new ChunkCoord3D(0, 0, 0)));
            Assert.IsTrue(streamer.Get(new ChunkCoord3D(0, 0, 0)).IsAllSolid);
        }

        [Test]
        public void OnVoxelChunkUnsubscribe_RemovesChunk()
        {
            var streamer = new VoxelChunkStreamer();
            streamer.OnSync(new VoxelChunkSync
            {
                Layer = 0, ChunkX = 1, ChunkY = 0, ChunkZ = 0,
                HomogenousMaterial = (byte)VoxelMaterial.Air,
            });
            streamer.OnUnsubscribe(new VoxelChunkUnsubscribe
            {
                Layer = 0, ChunkX = 1, ChunkY = 0, ChunkZ = 0,
            });
            Assert.IsNull(streamer.Get(new ChunkCoord3D(1, 0, 0)));
        }
    }
}
