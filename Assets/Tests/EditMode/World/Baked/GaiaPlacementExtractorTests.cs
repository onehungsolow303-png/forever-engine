using System.Linq;
using ForeverEngine.Core.World.Baked;
using ForeverEngine.Procedural.Editor;
using NUnit.Framework;
using UnityEngine;

namespace ForeverEngine.Tests.World.Baked
{
    // Verifies GaiaPlacementExtractor walks a Terrain GameObject's descendant prefab
    // instances and emits one BakedPropPlacement per instance with correct world
    // transforms. The extractor is the bake-time replacement for PropPlacementSampler:
    // because Gaia has already physics-settled props onto the terrain, the world Y
    // is taken directly from the GO transform — no elevation re-sample, no slope
    // filter. If this ever regresses, the next Gaia bake will silently produce
    // empty or mis-positioned props.bin.
    [TestFixture]
    public class GaiaPlacementExtractorTests
    {
        [Test]
        public void Extract_GathersChildrenBelowTerrain_AsBakedPropPlacements()
        {
            var root = new GameObject("TestRoot");
            try
            {
                var terrain = Terrain.CreateTerrainGameObject(new TerrainData());
                terrain.transform.SetParent(root.transform);

                var stand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                stand.name = "FakeTreePrefab";

                for (int i = 0; i < 3; i++)
                {
                    var go = Object.Instantiate(stand, terrain.transform);
                    go.transform.position = new Vector3(10f * i, 5f + i, 20f * i);
                    go.transform.rotation = Quaternion.Euler(0f, 45f * i, 0f);
                    go.transform.localScale = Vector3.one * (1f + 0.1f * i);
                }

                BakedPropPlacement[] placements = GaiaPlacementExtractor.Extract(
                    terrain, resolvePrefabGuid: _ => "fake-guid");

                Assert.That(placements.Length, Is.EqualTo(3));
                Assert.That(placements.All(p => p.PrefabGuid == "fake-guid"));
                Assert.That(placements[1].WorldX, Is.EqualTo(10f).Within(1e-4f));
                Assert.That(placements[1].WorldY, Is.EqualTo(6f).Within(1e-4f));
                Assert.That(placements[1].WorldZ, Is.EqualTo(20f).Within(1e-4f));
                Assert.That(placements[1].YawDegrees, Is.EqualTo(45f).Within(1e-3f));
                Assert.That(placements[1].UniformScale, Is.EqualTo(1.1f).Within(1e-4f));

                Object.DestroyImmediate(stand);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Extract_SkipsChildrenWithoutPrefabSource()
        {
            var root = new GameObject("Root");
            try
            {
                var terrain = Terrain.CreateTerrainGameObject(new TerrainData());
                terrain.transform.SetParent(root.transform);

                var orphan = new GameObject("NotAPrefabInstance");
                orphan.transform.SetParent(terrain.transform);

                var placements = GaiaPlacementExtractor.Extract(
                    terrain, resolvePrefabGuid: _ => null);

                Assert.That(placements.Length, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Extract_ReadsTerrainDataTreeInstances()
        {
            // Gaia's primary spawn path is TerrainData.treeInstances (high-density
            // tree-system, not GameObject children). The extractor must walk
            // these too — without this the bake is empty for any standard Gaia
            // biome.
            var root = new GameObject("Root");
            try
            {
                var data = new TerrainData();
                data.size = new Vector3(1024f, 100f, 1024f);

                var protoPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                protoPrefab.name = "TreeProto";
                data.treePrototypes = new[]
                {
                    new TreePrototype { prefab = protoPrefab },
                };

                data.treeInstances = new[]
                {
                    new TreeInstance
                    {
                        prototypeIndex = 0,
                        position = new Vector3(0.5f, 0f, 0.25f),  // normalized
                        rotation = 0f,
                        widthScale = 1f, heightScale = 1f, color = Color.white, lightmapColor = Color.white,
                    },
                    new TreeInstance
                    {
                        prototypeIndex = 0,
                        position = new Vector3(0.1f, 0f, 0.9f),
                        rotation = Mathf.PI / 2f,                  // 90°
                        widthScale = 0.8f, heightScale = 1.2f, color = Color.white, lightmapColor = Color.white,
                    },
                };

                var terrainGO = Terrain.CreateTerrainGameObject(data);
                terrainGO.transform.position = new Vector3(100f, 0f, 200f);
                terrainGO.transform.SetParent(root.transform);

                var placements = GaiaPlacementExtractor.Extract(
                    terrainGO, resolvePrefabGuid: _ => "tree-guid");

                Assert.That(placements.Length, Is.EqualTo(2));
                // First instance: pos.xz=(0.5, 0.25) on 1024×1024 starting at (100, 200)
                // → world (100 + 512, ?, 200 + 256) = (612, ?, 456)
                Assert.That(placements[0].WorldX, Is.EqualTo(612f).Within(1e-3f));
                Assert.That(placements[0].WorldZ, Is.EqualTo(456f).Within(1e-3f));
                // Second instance: rot 90° in radians → 90° in degrees
                Assert.That(placements[1].YawDegrees, Is.EqualTo(90f).Within(1e-2f));
                Assert.That(placements[1].UniformScale, Is.EqualTo(1.0f).Within(1e-3f));  // (0.8+1.2)/2

                Object.DestroyImmediate(protoPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Extract_RecursesIntoOrganizationalGroups()
        {
            // Gaia spawners wrap their output in a group GameObject (one per spawner).
            // The extractor must descend through groups and still find the leaf prefab
            // instances. Simulate this by parenting a "prefab" two levels below the
            // terrain — once for the spawner group, once for a sub-pool group.
            var root = new GameObject("Root");
            try
            {
                var terrain = Terrain.CreateTerrainGameObject(new TerrainData());
                terrain.transform.SetParent(root.transform);

                var spawnerGroup = new GameObject("SpawnerGroup");
                spawnerGroup.transform.SetParent(terrain.transform);

                var poolGroup = new GameObject("TreePool");
                poolGroup.transform.SetParent(spawnerGroup.transform);

                var leafA = new GameObject("Leaf_A");
                leafA.transform.SetParent(poolGroup.transform);
                leafA.transform.position = new Vector3(7f, 3f, 11f);

                var leafB = new GameObject("Leaf_B");
                leafB.transform.SetParent(poolGroup.transform);
                leafB.transform.position = new Vector3(8f, 4f, 12f);

                // Resolve only the leaves — groups must be skipped, not counted.
                System.Func<GameObject, string> resolver = go =>
                    go.name.StartsWith("Leaf_") ? "leaf-" + go.name : null;

                var placements = GaiaPlacementExtractor.Extract(terrain, resolver);

                Assert.That(placements.Length, Is.EqualTo(2));
                Assert.That(placements.Any(p => p.PrefabGuid == "leaf-Leaf_A"));
                Assert.That(placements.Any(p => p.PrefabGuid == "leaf-Leaf_B"));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
