using NUnit.Framework;
using ForeverEngine.Generation.Utility;
using ForeverEngine.Generation.Agents;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Tests
{
    public class TerrainGenerationTests
    {
        [Test] public void PerlinNoise_InRange() { PerlinNoise.Seed(42); for (int i = 0; i < 100; i++) { float v = PerlinNoise.Sample(i * 0.1f, i * 0.1f); Assert.GreaterOrEqual(v, 0f); Assert.LessOrEqual(v, 1f); } }
        [Test] public void PerlinNoise_Deterministic() { PerlinNoise.Seed(42); float a = PerlinNoise.Octave(5f, 5f, 4); PerlinNoise.Seed(42); float b = PerlinNoise.Octave(5f, 5f, 4); Assert.AreEqual(a, b, 0.0001f); }
        [Test] public void TerrainGenerator_ProducesWalkable()
        {
            var req = new MapGenerationRequest { Width = 64, Height = 64, Seed = 42 };
            var profile = MapProfile.Get("dungeon");
            var result = TerrainGenerator.Generate(req, profile);
            int walkable = 0; for (int i = 0; i < result.Walkability.Length; i++) if (result.Walkability[i]) walkable++;
            Assert.Greater(walkable, 0, "Should have some walkable tiles");
            Assert.Less(walkable, result.Walkability.Length, "Should have some walls");
        }
        [Test] public void CaveCarver_KeepsLargestRegion()
        {
            int w = 32, h = 32; var walk = new bool[w*h]; var rng = new System.Random(42);
            for (int i = 0; i < walk.Length; i++) walk[i] = rng.NextDouble() > 0.45;
            CaveCarver.Carve(walk, w, h, 42, 3);
            Assert.IsFalse(walk[0], "Border should be wall"); Assert.IsFalse(walk[w-1], "Border should be wall");
        }
    }
}
