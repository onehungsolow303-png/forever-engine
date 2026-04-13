using System;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Encounters
{
    [Serializable]
    public class EnemySlot
    {
        public string Name;
        public string Behavior;
        public int XPCost;

        public EnemySlot(string name, string behavior, int xpCost)
        {
            Name = name;
            Behavior = behavior;
            XPCost = xpCost;
        }
    }

    public class EncounterTemplate
    {
        public string Name;
        public int MinXP;
        public int MaxXP;
        public string[] Biomes;
        public EnemySlot[] Slots;

        public EncounterTemplate(string name, int minXP, int maxXP, string[] biomes, params EnemySlot[] slots)
        {
            Name = name;
            MinXP = minXP;
            MaxXP = maxXP;
            Biomes = biomes;
            Slots = slots;
        }

        public int TotalXP
        {
            get
            {
                int sum = 0;
                foreach (var s in Slots) sum += s.XPCost;
                return sum;
            }
        }

        public static readonly EncounterTemplate[] All = new[]
        {
            new EncounterTemplate("Goblin Raiding Party", 150, 300,
                new[] { "dungeon", "ruins" },
                new EnemySlot("Goblin King", "guard", 100),
                new EnemySlot("Goblin", "chase", 50),
                new EnemySlot("Goblin", "chase", 50)),

            new EncounterTemplate("Undead Patrol", 200, 400,
                new[] { "dungeon", "ruins", "crypt" },
                new EnemySlot("Mummy", "chase", 200),
                new EnemySlot("Skeleton", "guard", 50),
                new EnemySlot("Skeleton", "guard", 50)),

            new EncounterTemplate("Bandit Ambush", 125, 250,
                new[] { "road", "plains", "ruins" },
                new EnemySlot("Bandit Captain", "guard", 100),
                new EnemySlot("Bandit", "chase", 25),
                new EnemySlot("Bandit", "chase", 25),
                new EnemySlot("Bandit", "chase", 25)),

            new EncounterTemplate("Wolf Pack", 125, 250,
                new[] { "forest" },
                new EnemySlot("Alpha Wolf", "guard", 100),
                new EnemySlot("Wolf", "chase", 25),
                new EnemySlot("Wolf", "chase", 25),
                new EnemySlot("Wolf", "chase", 25)),

            new EncounterTemplate("Cultist Cell", 100, 200,
                new[] { "ruins", "dungeon", "crypt" },
                new EnemySlot("Cultist", "chase", 50),
                new EnemySlot("Cultist", "chase", 50),
                new EnemySlot("Skeleton", "guard", 50)),

            new EncounterTemplate("Lizardfolk Warband", 150, 350,
                new[] { "dungeon" },
                new EnemySlot("Lizard Folk Archer", "guard", 100),
                new EnemySlot("Lizard Folk", "chase", 50),
                new EnemySlot("Lizard Folk", "chase", 50)),

            new EncounterTemplate("Orc Raiders", 200, 400,
                new[] { "dungeon", "plains", "forest" },
                new EnemySlot("Orc", "guard", 100),
                new EnemySlot("Orc", "chase", 100),
                new EnemySlot("Kobold", "chase", 25),
                new EnemySlot("Kobold", "chase", 25)),
        };

        public static List<EncounterTemplate> FindMatching(int xpBudget, string biome)
        {
            var results = new List<EncounterTemplate>();
            foreach (var t in All)
            {
                if (t.TotalXP > xpBudget) continue;
                bool biomeMatch = false;
                foreach (var b in t.Biomes)
                {
                    if (b == biome) { biomeMatch = true; break; }
                }
                if (biomeMatch) results.Add(t);
            }
            return results;
        }
    }
}
