using System.Collections.Generic;

namespace ForeverEngine.ECS.Data
{
    public enum QuestStatus { NotStarted, Active, Completed, Failed }

    public struct QuestObjective { public string Id; public string Description; public int RequiredCount; }

    public class QuestDefinition { public string Id; public string Title; public string Description; public QuestObjective[] Objectives; public int GoldReward; public int XPReward; }

    public class QuestInstance
    {
        public string QuestId { get; }
        public QuestDefinition Definition { get; }
        public QuestStatus Status { get; private set; }
        public bool IsComplete => Status == QuestStatus.Completed;
        private Dictionary<string, int> _progress = new();

        public QuestInstance(string questId, QuestDefinition definition) { QuestId = questId; Definition = definition; Status = QuestStatus.NotStarted; }

        public void Start() { if (Status == QuestStatus.NotStarted) Status = QuestStatus.Active; }

        public void Progress(string objectiveId, int amount)
        {
            if (Status != QuestStatus.Active) return;
            _progress.TryGetValue(objectiveId, out int current);
            _progress[objectiveId] = current + amount;
            CheckCompletion();
        }

        public int GetObjectiveProgress(string objectiveId) => _progress.TryGetValue(objectiveId, out int val) ? val : 0;

        private void CheckCompletion()
        {
            if (Definition.Objectives == null) return;
            foreach (var obj in Definition.Objectives)
                if (GetObjectiveProgress(obj.Id) < obj.RequiredCount) return;
            Status = QuestStatus.Completed;
        }

        public void Fail() => Status = QuestStatus.Failed;
    }
}
