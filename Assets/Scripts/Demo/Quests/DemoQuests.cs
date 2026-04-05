using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.Demo.Quests
{
    public static class DemoQuests
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var qs = QuestSystem.Instance;
            if (qs == null) return;

            qs.RegisterQuest(new QuestDefinition
            {
                Id = "signal_fire", Title = "Signal Fire",
                Description = "Reach the ruined town of Ashwick to make contact with survivors.",
                Objectives = new[] { new QuestObjective { Id = "reach_town", Description = "Reach Ashwick Ruins", RequiredCount = 1 } },
                GoldReward = 20, XPReward = 50
            });

            qs.RegisterQuest(new QuestDefinition
            {
                Id = "merchants_plea", Title = "The Merchant's Plea",
                Description = "Clear the dungeon known as The Hollow of its undead infestation.",
                Objectives = new[] { new QuestObjective { Id = "clear_dungeon", Description = "Defeat enemies in The Hollow", RequiredCount = 5 } },
                GoldReward = 50, XPReward = 200
            });

            qs.RegisterQuest(new QuestDefinition
            {
                Id = "dwarven_alliance", Title = "Dwarven Alliance",
                Description = "Deliver the merchant's letter to the dwarven chief at Ironhold.",
                Objectives = new[] { new QuestObjective { Id = "deliver_letter", Description = "Deliver letter to Chief Borin", RequiredCount = 1 } },
                GoldReward = 0, XPReward = 150
            });

            qs.RegisterQuest(new QuestDefinition
            {
                Id = "cursed_throne", Title = "The Cursed Throne",
                Description = "Confront and defeat the Rot King in the Throne of Rot castle.",
                Objectives = new[] { new QuestObjective { Id = "defeat_boss", Description = "Defeat the Rot King", RequiredCount = 1 } },
                GoldReward = 250, XPReward = 500
            });

            // Auto-start first quest
            qs.StartQuest("signal_fire");
        }
    }
}
