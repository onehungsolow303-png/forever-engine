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
            var go = new GameObject("CM");
            var cm = go.AddComponent<ChunkManager>();
            // Inject a fake LoadedChunk by hand — exposed via internal test helper.
            var fakeTerrain = new GameObject("FakeChunk");
            fakeTerrain.AddComponent<MeshRenderer>();
            cm.RegisterTerrainForTest(new ChunkCoord(0, 0), fakeTerrain);

            cm.VoxelTerrainActive = true;
            Assert.IsFalse(fakeTerrain.GetComponent<MeshRenderer>().enabled);

            cm.VoxelTerrainActive = false;
            Assert.IsTrue(fakeTerrain.GetComponent<MeshRenderer>().enabled);

            Object.DestroyImmediate(fakeTerrain);
            Object.DestroyImmediate(go);
        }
    }
}
