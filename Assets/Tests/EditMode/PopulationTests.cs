using NUnit.Framework;
using ForeverEngine.Generation.Data;
using ForeverEngine.Generation.Agents;

namespace ForeverEngine.Tests
{
    public class PopulationTests
    {
        [Test] public void Population_HasPlayerSpawn()
        {
            var graph = TopologyBuilder.Build(TopologyType.LinearWithBranches, 5, 42);
            RoomPlacer.Place(graph, 64, 64, 42);
            var req = new MapGenerationRequest { Width = 64, Height = 64, Seed = 42, PartyLevel = 3, PartySize = 4 };
            var result = PopulationGenerator.Populate(graph, req, MapProfile.Get("dungeon"), new bool[64*64], 64);
            Assert.AreEqual("player", result.PlayerSpawn.Variant);
        }

        [Test] public void Population_EncountersScaleWithLevel()
        {
            var graph = TopologyBuilder.Build(TopologyType.LinearWithBranches, 8, 42);
            RoomPlacer.Place(graph, 128, 128, 42);
            foreach (var n in graph.Nodes) n.Purpose = "guard_room";
            var low = PopulationGenerator.Populate(graph, new MapGenerationRequest { Width=128, Height=128, Seed=42, PartyLevel=1, PartySize=4 }, MapProfile.Get("dungeon"), new bool[128*128], 128);
            var high = PopulationGenerator.Populate(graph, new MapGenerationRequest { Width=128, Height=128, Seed=42, PartyLevel=10, PartySize=4 }, MapProfile.Get("dungeon"), new bool[128*128], 128);
            int lowXP = 0, highXP = 0;
            foreach (var e in low.Encounters) lowXP += e.Value;
            foreach (var e in high.Encounters) highXP += e.Value;
            Assert.Greater(highXP, lowXP, "Higher level should have more XP budget");
        }

        [Test] public void Population_DressingAlwaysPresent()
        {
            var graph = TopologyBuilder.Build(TopologyType.LinearWithBranches, 5, 42);
            RoomPlacer.Place(graph, 64, 64, 42);
            foreach (var n in graph.Nodes) n.Purpose = "guard_room";
            var result = PopulationGenerator.Populate(graph, new MapGenerationRequest { Width=64, Height=64, Seed=42 }, MapProfile.Get("dungeon"), new bool[64*64], 64);
            Assert.Greater(result.Dressing.Count, 0, "Should have dressing entities");
        }
    }
}
