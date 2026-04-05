using NUnit.Framework;
using ForeverEngine.Generation;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Tests
{
    public class PipelineTests
    {
        [Test] public void Pipeline_DungeonSucceeds()
        {
            var req = new MapGenerationRequest { MapType = "dungeon", Width = 64, Height = 64, Seed = 42, PartyLevel = 3, PartySize = 4 };
            var result = PipelineCoordinator.Generate(req);
            Assert.IsTrue(result.Success, result.Error);
            Assert.Greater(result.Layout.Nodes.Count, 0);
            Assert.AreEqual("player", result.Population.PlayerSpawn.Variant);
        }

        [Test] public void Pipeline_InvalidRequest_Fails()
        {
            var req = new MapGenerationRequest { Width = 5 }; // Too small
            var result = PipelineCoordinator.Generate(req);
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
        }
    }
}
