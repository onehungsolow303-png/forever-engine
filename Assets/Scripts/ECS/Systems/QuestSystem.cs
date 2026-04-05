using Unity.Entities;
using System.Collections.Generic;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.ECS.Systems
{
    public partial class QuestSystem : SystemBase
    {
        private Dictionary<string, QuestDefinition> _definitions = new();
        private Dictionary<string, QuestInstance> _activeQuests = new();
        private List<QuestInstance> _completedQuests = new();
        public static QuestSystem Instance { get; private set; }

        protected override void OnCreate() => Instance = this;

        public void RegisterQuest(QuestDefinition def) => _definitions[def.Id] = def;

        public QuestInstance StartQuest(string questId)
        {
            if (!_definitions.TryGetValue(questId, out var def)) return null;
            var instance = new QuestInstance(questId, def);
            instance.Start();
            _activeQuests[questId] = instance;
            return instance;
        }

        public void ProgressQuest(string questId, string objectiveId, int amount = 1)
        {
            if (!_activeQuests.TryGetValue(questId, out var quest)) return;
            quest.Progress(objectiveId, amount);
            if (quest.IsComplete) { _activeQuests.Remove(questId); _completedQuests.Add(quest); }
        }

        public QuestInstance GetQuest(string questId) => _activeQuests.TryGetValue(questId, out var q) ? q : null;
        public List<QuestInstance> GetActiveQuests() => new(_activeQuests.Values);
        public List<QuestInstance> GetCompletedQuests() => new(_completedQuests);

        protected override void OnUpdate() { }
        protected override void OnDestroy() { _activeQuests.Clear(); _completedQuests.Clear(); if (Instance == this) Instance = null; }
    }
}
