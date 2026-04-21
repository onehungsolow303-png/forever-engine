using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural.Editor;

namespace ForeverEngine.Tests.World.Baked
{
    public class UnityTerrainSamplerTests
    {
        [Test]
        public void SampleHeightmap_ReturnsArrayOfExpectedSize()
        {
            var terrainData = new TerrainData { heightmapResolution = 65 };
            terrainData.size = new Vector3(256f, 200f, 256f);
            // Known ramp: height[z,x] = x * 0.01
            var raw = new float[65, 65];
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                    raw[z, x] = x * 0.01f;
            terrainData.SetHeights(0, 0, raw);

            var go = new GameObject("TestTerrain");
            var terrain = go.AddComponent<Terrain>();
            terrain.terrainData = terrainData;
            try
            {
                var heights = UnityTerrainSampler.SampleHeightmap(terrain, 4, 4, maxHeightMeters: 200f);
                Assert.AreEqual(16, heights.Length);
                Assert.Less(heights[0], 1f);
                Assert.Greater(heights[3], 1f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
