using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.EditMode.Procedural
{
    public class ChunkManagerVoxelSuppressionTests
    {
        [Test]
        public void VoxelTerrainActive_True_DisablesAllLoadedTerrainRenderers()
        {
            // Mirror production LOD hierarchy: TerrainGO is a LODGroup root with MeshRenderer children.
            var go = new GameObject("CM");
            var cm = go.AddComponent<ChunkManager>();

            // Root holds a LODGroup (no MeshRenderer on root, matching TerrainGenerator output).
            var fakeTerrain = new GameObject("FakeChunkRoot");
            fakeTerrain.AddComponent<UnityEngine.LODGroup>();
            var lodChild = new GameObject("LOD0");
            lodChild.transform.SetParent(fakeTerrain.transform);
            var childMR = lodChild.AddComponent<MeshRenderer>();

            cm.RegisterTerrainForTest(new ChunkCoord(0, 0), fakeTerrain);

            cm.VoxelTerrainActive = true;
            Assert.IsFalse(childMR.enabled);

            cm.VoxelTerrainActive = false;
            Assert.IsTrue(childMR.enabled);

            // Destroying the parent also destroys its children.
            Object.DestroyImmediate(fakeTerrain);
            Object.DestroyImmediate(go);
        }
    }
}
