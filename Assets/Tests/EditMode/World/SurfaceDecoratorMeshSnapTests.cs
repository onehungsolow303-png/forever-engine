using ForeverEngine.Core.World;
using ForeverEngine.Procedural;
using NUnit.Framework;

namespace ForeverEngine.Tests.World
{
    // Verifies SurfaceDecorator routes prop-Y through TerrainTriangleSampler,
    // not the local bilinear helper. The sampler itself is unit-tested in Core;
    // this test guards the delegation.
    [TestFixture]
    public class SurfaceDecoratorMeshSnapTests
    {
        [Test]
        public void PropPlacementY_MatchesTerrainTriangleSampler()
        {
            // 64×64 heightmap with quadratic gradient so bilinear ≠ triangle.
            var hm = new float[64 * 64];
            for (int z = 0; z < 64; z++)
                for (int x = 0; x < 64; x++)
                    hm[z * 64 + x] = (x * x) + (z * z);

            float localX = 137.42f, localZ = 94.11f;

            float expected = TerrainTriangleSampler.SampleMeshTriangleY(
                hm, heightmapRes: 64, chunkSizeMeters: 256f,
                meshResolution: TerrainTriangleSampler.DefaultMeshResolution,
                localX: localX, localZ: localZ);

            float got = SurfaceDecorator.SampleGroundY_ForTest(hm, 64, 256f, localX, localZ);

            Assert.That(got, Is.EqualTo(expected).Within(1e-3f));
        }
    }
}
