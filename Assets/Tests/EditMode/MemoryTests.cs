using NUnit.Framework;
using UnityEngine;
using System.Linq;
using ForeverEngine.AI.Memory;

namespace ForeverEngine.Tests
{
    public class MemoryTests
    {
        [Test] public void ShortTerm_RecordAndRetrieve() { var stm = new ShortTermMemory(10f); stm.RecordEvent("spotted", Vector3.zero, "npc1"); Assert.AreEqual(1, stm.Count); Assert.AreEqual(1, stm.GetEventsByType("spotted").Count); }
        [Test] public void Episodic_RecordAndQuery() { var em = new EpisodicMemory(); em.Record(new Episode { Actor = "guard", Action = "patrol", Target = "hallway", Importance = 0.5f }); Assert.AreEqual(1, em.Count); Assert.AreEqual(1, em.Query(actor: "guard").Count()); }
        [Test] public void Episodic_MostImportant() { var em = new EpisodicMemory(); em.Record(new Episode { Action = "low", Importance = 0.1f }); em.Record(new Episode { Action = "high", Importance = 0.9f }); Assert.AreEqual("high", em.GetMostImportant(1)[0].Action); }
        [Test] public void LongTerm_SetAndGet() { var ltm = new LongTermMemory(); ltm.Set("kills", 42); Assert.AreEqual(42, ltm.GetInt("kills")); }
        [Test] public void LongTerm_Relationships() { var ltm = new LongTermMemory(); ltm.AdjustRelationship("npc1", -30); Assert.AreEqual(-30, ltm.GetRelationship("npc1")); ltm.AdjustRelationship("npc1", 10); Assert.AreEqual(-20, ltm.GetRelationship("npc1")); }
        [Test] public void LongTerm_Clamps() { var ltm = new LongTermMemory(); ltm.AdjustRelationship("x", -200); Assert.AreEqual(-100, ltm.GetRelationship("x")); }
    }
}
