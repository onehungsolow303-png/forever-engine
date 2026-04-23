using ForeverEngine.Core.World.Baked;
using ForeverEngine.Network;
using NUnit.Framework;
using CoreChunk = ForeverEngine.Core.World.ChunkData;

namespace ForeverEngine.Tests.Network
{
    [TestFixture]
    public class ChunkDataMapperPropsTests
    {
        [Test]
        public void MapServerToLocal_CopiesProps()
        {
            var src = new CoreChunk
            {
                ChunkX = 3,
                ChunkZ = -2,
                Biome = "alpine_meadow",
                Heightmap = new float[16 * 16],
                Props = new[]
                {
                    new BakedPropPlacement("guid-X", "Assets/X.prefab", 10f, 5f, 20f, 90f, 1.2f),
                    new BakedPropPlacement("guid-Y", "Assets/Y.prefab", 30f, 6f, 40f,  0f, 1.0f),
                },
            };

            var dst = ChunkDataMapper.MapServerToLocal(src);

            Assert.That(dst.Props, Is.Not.Null);
            Assert.That(dst.Props.Count, Is.EqualTo(2));
            Assert.That(dst.Props[0].PrefabGuid, Is.EqualTo("guid-X"));
            Assert.That(dst.Props[0].WorldY, Is.EqualTo(5f));
            Assert.That(dst.Props[1].PrefabGuid, Is.EqualTo("guid-Y"));
        }

        [Test]
        public void MapServerToLocal_EmptyProps_ProducesEmptyList()
        {
            var src = new CoreChunk { ChunkX = 0, ChunkZ = 0, Heightmap = new float[16 * 16] };
            var dst = ChunkDataMapper.MapServerToLocal(src);
            Assert.That(dst.Props, Is.Not.Null);
            Assert.That(dst.Props.Count, Is.EqualTo(0));
        }
    }
}
