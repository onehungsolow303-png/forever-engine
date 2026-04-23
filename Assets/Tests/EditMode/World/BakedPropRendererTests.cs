using ForeverEngine.Core.World.Baked;
using ForeverEngine.Procedural;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForeverEngine.Tests.World
{
    [TestFixture]
    public class BakedPropRendererTests
    {
        private PrefabRegistry _registry;
        private GameObject _samplePrefab;

        [SetUp]
        public void SetUp()
        {
            _samplePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _samplePrefab.name = "TestCubePrefab";

            _registry = ScriptableObject.CreateInstance<PrefabRegistry>();
            _registry.SetEntries_Editor(new[]
            {
                new PrefabRegistry.Entry { Guid = "guid-A", Prefab = _samplePrefab },
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_samplePrefab != null) Object.DestroyImmediate(_samplePrefab);
            if (_registry != null) Object.DestroyImmediate(_registry);
        }

        [Test]
        public void Render_InstantiatesPrefabAtBakedPosition()
        {
            var parent = new GameObject("Parent");
            try
            {
                var placements = new[]
                {
                    new BakedPropPlacement("guid-A", "path", 100.5f, 42.25f, -3.125f, 45f, 1.5f),
                };

                BakedPropRenderer.Render(placements, parent.transform, _registry);

                Assert.That(parent.transform.childCount, Is.EqualTo(1));
                var child = parent.transform.GetChild(0);
                Assert.That(child.position.x, Is.EqualTo(100.5f).Within(1e-4f));
                Assert.That(child.position.y, Is.EqualTo(42.25f).Within(1e-4f));
                Assert.That(child.position.z, Is.EqualTo(-3.125f).Within(1e-4f));
                Assert.That(child.rotation.eulerAngles.y, Is.EqualTo(45f).Within(1e-3f));
                Assert.That(child.localScale.x, Is.EqualTo(1.5f).Within(1e-4f));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Render_UnknownGuid_Skips()
        {
            var parent = new GameObject("Parent");
            try
            {
                // Declare the expected warning so LogAssert does not fail the test.
                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex("not in PrefabRegistry"));

                var placements = new[]
                {
                    new BakedPropPlacement("unknown-guid", "path", 0f, 0f, 0f, 0f, 1f),
                };

                BakedPropRenderer.Render(placements, parent.transform, _registry);

                Assert.That(parent.transform.childCount, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Render_EmptyArray_DoesNothing()
        {
            var parent = new GameObject("Parent");
            try
            {
                BakedPropRenderer.Render(System.Array.Empty<BakedPropPlacement>(), parent.transform, _registry);
                Assert.That(parent.transform.childCount, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
