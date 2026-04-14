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

            // --- Forest biome ---
            new EncounterTemplate("Spider Ambush", 150, 300,
                new[] { "forest" },
                new EnemySlot("Giant Spider", "guard", 200),
                new EnemySlot("Spider Swarm", "chase", 25),
                new EnemySlot("Spider Swarm", "chase", 25)),

            new EncounterTemplate("Treant's Grove", 400, 600,
                new[] { "forest" },
                new EnemySlot("Treant", "guard", 450),
                new EnemySlot("Twig Blight", "chase", 25),
                new EnemySlot("Twig Blight", "chase", 25)),

            // --- Dungeon biome ---
            new EncounterTemplate("Necromancer's Lab", 200, 400,
                new[] { "dungeon" },
                new EnemySlot("Necromancer", "guard", 200),
                new EnemySlot("Skeleton", "chase", 25),
                new EnemySlot("Skeleton", "chase", 25),
                new EnemySlot("Skeleton", "chase", 25)),

            new EncounterTemplate("Mimic Trap", 150, 300,
                new[] { "dungeon" },
                new EnemySlot("Mimic", "guard", 200)),

            new EncounterTemplate("Rat King", 150, 350,
                new[] { "dungeon" },
                new EnemySlot("Giant Rat", "guard", 100),
                new EnemySlot("Rat Swarm", "chase", 25),
                new EnemySlot("Rat Swarm", "chase", 25),
                new EnemySlot("Rat Swarm", "chase", 25),
                new EnemySlot("Rat Swarm", "chase", 25)),

            // --- Plains biome ---
            new EncounterTemplate("Merchant Guard", 200, 400,
                new[] { "plains" },
                new EnemySlot("Bandit Captain", "guard", 200),
                new EnemySlot("Bandit", "chase", 50),
                new EnemySlot("Bandit Archer", "guard", 50)),

            new EncounterTemplate("Gnoll Hunting Party", 300, 550,
                new[] { "plains" },
                new EnemySlot("Gnoll Pack Lord", "guard", 200),
                new EnemySlot("Gnoll", "chase", 100),
                new EnemySlot("Gnoll", "chase", 100)),

            // --- Ruins biome ---
            new EncounterTemplate("Ghoul Pack", 200, 400,
                new[] { "ruins" },
                new EnemySlot("Ghoul", "chase", 200),
                new EnemySlot("Zombie", "chase", 50),
                new EnemySlot("Zombie", "chase", 50)),

            new EncounterTemplate("Wraith Haunting", 400, 600,
                new[] { "ruins" },
                new EnemySlot("Wraith", "guard", 450)),

            new EncounterTemplate("Cursed Knights", 250, 450,
                new[] { "ruins" },
                new EnemySlot("Death Knight", "guard", 200),
                new EnemySlot("Skeleton Archer", "guard", 50),
                new EnemySlot("Skeleton Archer", "guard", 50)),
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
