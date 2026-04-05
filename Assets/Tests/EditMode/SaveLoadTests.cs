using NUnit.Framework;
using ForeverEngine.MonoBehaviour.SaveLoad;

namespace ForeverEngine.Tests
{
    public class SaveLoadTests
    {
        [Test] public void SaveData_SerializesToJson() { var data = new SaveData { MapPath = "test.json", PlayerX = 5, PlayerY = 3, PlayerHP = 15, PlayerMaxHP = 20, Gold = 100 }; string json = SaveData.ToJson(data); Assert.IsNotNull(json); Assert.IsTrue(json.Contains("\"PlayerX\": 5")); }
        [Test] public void SaveData_DeserializesFromJson() { var orig = new SaveData { MapPath = "test.json", PlayerX = 7, PlayerY = 2, PlayerHP = 10, PlayerMaxHP = 20, Gold = 50 }; var loaded = SaveData.FromJson(SaveData.ToJson(orig)); Assert.AreEqual(7, loaded.PlayerX); Assert.AreEqual(10, loaded.PlayerHP); Assert.AreEqual("test.json", loaded.MapPath); }
        [Test] public void SaveData_RoundTrip_Preserves() { var data = new SaveData { MapPath = "dungeon/map.json", PlayerX = 12, PlayerY = 8, PlayerZ = -1, PlayerHP = 5, PlayerMaxHP = 30, Gold = 999, ActiveQuests = new[] { "kill_goblins", "find_artifact" }, CompletedQuests = new[] { "tutorial" } }; var restored = SaveData.FromJson(SaveData.ToJson(data)); Assert.AreEqual(data.MapPath, restored.MapPath); Assert.AreEqual(data.PlayerZ, restored.PlayerZ); Assert.AreEqual(2, restored.ActiveQuests.Length); }
    }
}
