using System.Collections.Generic;

namespace ForeverEngine.Demo.Encounters
{
    [System.Serializable]
    public class EnemyDef { public string Name; public int HP; public int AC; public int Str; public int Dex; public int Spd; public string AtkDice; public string Behavior; }

    [System.Serializable]
    public class EncounterData
    {
        public string Id;
        public int GridWidth = 8, GridHeight = 8;
        public List<EnemyDef> Enemies = new();
        public int GoldReward;
        public int XPReward;

        private static Dictionary<string, System.Func<EncounterData>> _templates;

        public static EncounterData Get(string id)
        {
            if (_templates == null) Init();
            if (_templates.TryGetValue(id, out var factory)) return factory();
            // Parse dynamic IDs like "random_Forest_day"
            if (id.StartsWith("random_")) return GenerateRandom(id);
            return DefaultEncounter();
        }

        private static EncounterData GenerateRandom(string id)
        {
            bool night = id.Contains("night");
            var enc = new EncounterData { Id = id, GridWidth = 8, GridHeight = 8 };

            if (id.Contains("Forest"))
            {
                int count = night ? 3 : 2;
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(new EnemyDef { Name = "Wolf", HP = 8, AC = 11, Str = 12, Dex = 14, Spd = 6, AtkDice = "1d6+1", Behavior = "chase" });
                enc.GoldReward = 5 * count; enc.XPReward = 25 * count;
            }
            else if (id.Contains("Road")) // Ruins
            {
                int count = night ? 4 : 2;
                string type = night ? "Mutant" : "Skeleton";
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(new EnemyDef { Name = type, HP = night ? 15 : 10, AC = night ? 13 : 10, Str = night ? 14 : 10, Dex = 10, Spd = 5, AtkDice = night ? "1d8+2" : "1d6", Behavior = night ? "chase" : "guard" });
                enc.GoldReward = 10 * count; enc.XPReward = 50 * count;
            }
            else // Plains
            {
                enc.Enemies.Add(new EnemyDef { Name = "Bandit", HP = 12, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d6+2", Behavior = "chase" });
                if (night) enc.Enemies.Add(new EnemyDef { Name = "Bandit", HP = 12, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d6+2", Behavior = "chase" });
                enc.GoldReward = 15; enc.XPReward = 50;
            }

            return enc;
        }

        private static EncounterData DefaultEncounter()
        {
            var enc = new EncounterData { Id = "default", GoldReward = 5, XPReward = 10 };
            enc.Enemies.Add(new EnemyDef { Name = "Rat", HP = 4, AC = 10, Str = 8, Dex = 12, Spd = 6, AtkDice = "1d4", Behavior = "chase" });
            return enc;
        }

        private static void Init()
        {
            _templates = new Dictionary<string, System.Func<EncounterData>>
            {
                ["dungeon_boss"] = () => new EncounterData
                {
                    Id = "dungeon_boss", GridWidth = 12, GridHeight = 12, GoldReward = 100, XPReward = 200,
                    Enemies = new List<EnemyDef>
                    {
                        new EnemyDef { Name = "Hollow Guardian", HP = 40, AC = 15, Str = 16, Dex = 10, Spd = 4, AtkDice = "2d6+3", Behavior = "guard" },
                        new EnemyDef { Name = "Skeleton", HP = 10, AC = 10, Str = 10, Dex = 10, Spd = 5, AtkDice = "1d6", Behavior = "chase" },
                        new EnemyDef { Name = "Skeleton", HP = 10, AC = 10, Str = 10, Dex = 10, Spd = 5, AtkDice = "1d6", Behavior = "chase" }
                    }
                },
                ["castle_boss"] = () => new EncounterData
                {
                    Id = "castle_boss", GridWidth = 16, GridHeight = 16, GoldReward = 250, XPReward = 500,
                    Enemies = new List<EnemyDef>
                    {
                        new EnemyDef { Name = "The Rot King", HP = 80, AC = 18, Str = 20, Dex = 12, Spd = 5, AtkDice = "2d8+5", Behavior = "guard" },
                        new EnemyDef { Name = "Rot Knight", HP = 25, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "1d8+3", Behavior = "chase" },
                        new EnemyDef { Name = "Rot Knight", HP = 25, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "1d8+3", Behavior = "chase" },
                        new EnemyDef { Name = "Plague Rat", HP = 8, AC = 11, Str = 8, Dex = 14, Spd = 7, AtkDice = "1d4+1", Behavior = "chase" },
                        new EnemyDef { Name = "Plague Rat", HP = 8, AC = 11, Str = 8, Dex = 14, Spd = 7, AtkDice = "1d4+1", Behavior = "chase" }
                    }
                }
            };
        }
    }
}
