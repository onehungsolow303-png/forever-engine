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

        // Regression A: URP-conversion can leave every MeshRenderer on a prefab
        // disabled. Fallback scan must kick in so the prop doesn't float at pivot.
        [Test]
        public void ComputeBaseOffset_AllRenderersDisabled_FallsBackToAllFilters()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = Vector3.zero;

                var mr = go.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr);
                mr.enabled = false;

                // Cube: bounds.center=(0,0,0), extents=(0.5,0.5,0.5). min.y = -0.5.
                float offset = SurfaceDecorator.ComputeBaseOffset_ForTest(go);

                Assert.That(offset, Is.EqualTo(-0.5f).Within(1e-3f),
                    "All-disabled prefabs should fall back to scanning any filter.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // Regression B: LODGroup + hidden-decoration prefabs have SOME enabled
        // renderers and SOME disabled ones. Offset must come from the ENABLED
        // geometry only — otherwise a disabled decoration mesh parked far below
        // pivot would yank the prop upward into the air.
        [Test]
        public void ComputeBaseOffset_MixedEnabledDisabled_UsesOnlyEnabled()
        {
            var root = new GameObject("MixedLODRoot");
            try
            {
                // Enabled child: cube from y=-0.5 to y=+0.5 (the "LOD0" visible mesh).
                var enabledChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                enabledChild.transform.SetParent(root.transform, worldPositionStays: false);
                enabledChild.transform.localPosition = Vector3.zero;
                Assert.IsTrue(enabledChild.GetComponent<MeshRenderer>().enabled);

                // Disabled child: cube parked 10m BELOW pivot (a "reference" mesh that
                // would wrongly pull baseOffset down to -10.5 if counted).
                var disabledChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                disabledChild.transform.SetParent(root.transform, worldPositionStays: false);
                disabledChild.transform.localPosition = new Vector3(0f, -10f, 0f);
                disabledChild.GetComponent<MeshRenderer>().enabled = false;

                // Offset must match the enabled cube's min (-0.5), NOT the disabled cube's min (-10.5).
                float offset = SurfaceDecorator.ComputeBaseOffset_ForTest(root);

                Assert.That(offset, Is.EqualTo(-0.5f).Within(1e-3f),
                    "Disabled LOD/decoration children should be ignored when at least one renderer is enabled.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
