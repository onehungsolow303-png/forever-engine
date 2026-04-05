using NUnit.Framework;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Tests
{
    public class QuestTests
    {
        [Test] public void NewQuest_IsNotStarted() { var quest = CreateTestQuest(); Assert.AreEqual(QuestStatus.NotStarted, quest.Status); }
        [Test] public void StartQuest_ChangesStatus() { var quest = CreateTestQuest(); quest.Start(); Assert.AreEqual(QuestStatus.Active, quest.Status); }
        [Test] public void ProgressObjective_TracksCount() { var quest = CreateTestQuest(); quest.Start(); quest.Progress("kill", 3); Assert.AreEqual(3, quest.GetObjectiveProgress("kill")); Assert.IsFalse(quest.IsComplete); }
        [Test] public void CompleteAllObjectives_CompletesQuest() { var quest = CreateTestQuest(); quest.Start(); quest.Progress("kill", 5); Assert.IsTrue(quest.IsComplete); Assert.AreEqual(QuestStatus.Completed, quest.Status); }
        [Test] public void MultipleObjectives_AllMustComplete()
        {
            var quest = new QuestInstance("dungeon", new QuestDefinition { Id = "dungeon", Title = "Clear", Objectives = new[] { new QuestObjective { Id = "kill", Description = "Kill boss", RequiredCount = 1 }, new QuestObjective { Id = "loot", Description = "Find treasure", RequiredCount = 1 } } });
            quest.Start(); quest.Progress("kill", 1); Assert.IsFalse(quest.IsComplete); quest.Progress("loot", 1); Assert.IsTrue(quest.IsComplete);
        }
        [Test] public void ProgressBeforeStart_DoesNothing() { var quest = CreateTestQuest(); quest.Progress("kill", 5); Assert.AreEqual(QuestStatus.NotStarted, quest.Status); Assert.AreEqual(0, quest.GetObjectiveProgress("kill")); }

        private QuestInstance CreateTestQuest() => new QuestInstance("test", new QuestDefinition { Id = "test", Title = "Test", Objectives = new[] { new QuestObjective { Id = "kill", Description = "Kill 5", RequiredCount = 5 } } });
    }
}
