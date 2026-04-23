using ForeverEngine.Core.World;
using ForeverEngine.Procedural;
using NUnit.Framework;
using UnityEngine;

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

        // Regression: URP-conversion or LODGroup can leave mr.enabled=false at
        // Instantiate time. ComputeBaseOffset must still read the MeshFilter's
        // geometry so props don't float at pivot height.
        [Test]
        public void ComputeBaseOffset_DisabledRenderer_StillReadsMeshBounds()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                // Simulate URP-broken material: renderer present but disabled.
                var mr = go.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, "Cube primitive must have a MeshRenderer");
                mr.enabled = false;

                // Unity cube primitive: bounds.center = (0,0,0), extents = (0.5, 0.5, 0.5).
                // With pivot at origin, expected offset = min.y - pivot.y = -0.5.
                float offset = SurfaceDecorator.ComputeBaseOffset_ForTest(go);

                Assert.That(offset, Is.EqualTo(-0.5f).Within(1e-3f),
                    "Disabled MeshRenderer should not prevent bounds-scan — prop would float otherwise.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
