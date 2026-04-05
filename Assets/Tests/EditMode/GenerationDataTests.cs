using NUnit.Framework;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Tests
{
    public class GenerationDataTests
    {
        [Test] public void MapGenerationRequest_Valid() { var req = new MapGenerationRequest(); Assert.IsTrue(req.Validate(out _)); }
        [Test] public void MapGenerationRequest_InvalidWidth() { var req = new MapGenerationRequest { Width = 10 }; Assert.IsFalse(req.Validate(out string err)); Assert.IsTrue(err.Contains("Width")); }
        [Test] public void MapProfile_DungeonExists() { var p = MapProfile.Get("dungeon"); Assert.IsNotNull(p); Assert.AreEqual("dungeon", p.Id); Assert.AreEqual(MapFamily.Dungeon, p.Family); }
        [Test] public void MapProfile_UnknownReturnsDungeon() { var p = MapProfile.Get("nonexistent"); Assert.AreEqual("dungeon", p.Id); }
        [Test] public void GameTables_XPBudget_Scales() { Assert.AreEqual(300, GameTables.GetXPBudget(1, 4)); Assert.Greater(GameTables.GetXPBudget(10, 4), GameTables.GetXPBudget(1, 4)); }
        [Test] public void RoomGraph_IsConnected() { var g = new RoomGraph(); g.AddNode(new RoomNode { Id = 0 }); g.AddNode(new RoomNode { Id = 1 }); g.Connect(0, 1); Assert.IsTrue(g.IsConnected()); }
    }
}
