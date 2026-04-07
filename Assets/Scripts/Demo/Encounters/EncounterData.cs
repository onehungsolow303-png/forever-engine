using System.Collections.Generic;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Demo.Encounters
{
    [System.Serializable]
    public class EnemyDef
    {
        public string Name;
        public int HP, AC, Str, Dex, Spd;
        public string AtkDice;
        public string Behavior;
        public int CR; // Challenge Rating (0 = CR 0, 25 = CR 1/4 scaled by 100 for int, or just use 1-30)
        public DamageType AttackDamageType = DamageType.Slashing;
        public DamageType Resistances;
        public DamageType Vulnerabilities;
        public DamageType Immunities;
    }

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

            // Get player level for XP budget calculation
            int playerLevel = GameManager.Instance?.Character?.TotalLevel
                ?? GameManager.Instance?.Player?.Level ?? 1;

            // Phase 3 pivot: AIDirector archived to Director Hub. Pacing
            // multiplier defaults to 1.0; will be reintroduced via
            // DirectorClient in a follow-up.
            float pacingMult = 1.0f;

            // XP budget: Medium difficulty baseline (50 x level), night = Hard (75 x level)
            int xpBudget = (int)((night ? 75 : 50) * playerLevel * pacingMult);

            if (id.Contains("Forest"))
            {
                // Wolves: CR 1/4 (25 XP each), ~10 HP, AC 11
                int count = System.Math.Max(1, xpBudget / 25);
                count = System.Math.Min(count, 5); // Cap at 5
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(MakeCREnemyDef("Wolf", 25, "chase", "Forest",
                        DamageType.Piercing));
                enc.GoldReward = 5 * count; enc.XPReward = 25 * count;
            }
            else if (id.Contains("Road")) // Ruins
            {
                if (night)
                {
                    // Mutants: CR 1 (100 XP), ~25 HP, AC 13
                    int count = System.Math.Max(1, xpBudget / 100);
                    count = System.Math.Min(count, 4);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Mutant", 100, "chase", "Ruins",
                            DamageType.Bludgeoning));
                    enc.GoldReward = 15 * count; enc.XPReward = 100 * count;
                }
                else
                {
                    // Skeletons: CR 1/4 (25 XP), vulnerable to bludgeoning, resistant to piercing
                    int count = System.Math.Max(1, xpBudget / 25);
                    count = System.Math.Min(count, 4);
                    for (int i = 0; i < count; i++)
                    {
                        var skel = MakeCREnemyDef("Skeleton", 25, "guard", "Ruins",
                            DamageType.Slashing);
                        skel.Vulnerabilities = DamageType.Bludgeoning;
                        skel.Resistances = DamageType.Piercing;
                        enc.Enemies.Add(skel);
                    }
                    enc.GoldReward = 10 * count; enc.XPReward = 25 * count;
                }
            }
            else // Plains
            {
                // Bandits: CR 1/2 (50 XP), ~15 HP, AC 12
                int count = System.Math.Max(1, xpBudget / 50);
                count = System.Math.Min(count, 4);
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(MakeCREnemyDef("Bandit", 50, "chase", "Plains",
                        DamageType.Slashing));
                enc.GoldReward = 15 * count; enc.XPReward = 50 * count;
            }

            return enc;
        }

        /// <summary>
        /// Create an EnemyDef from a CR-based stat block.
        /// CR lookup table:
        ///   XP 25  (CR 1/4): ~10 HP, AC 11, STR 12, DEX 14, Spd 6, 1d6+1
        ///   XP 50  (CR 1/2): ~15 HP, AC 12, STR 12, DEX 12, Spd 6, 1d8+1
        ///   XP 100 (CR 1):   ~25 HP, AC 13, STR 14, DEX 10, Spd 5, 1d10+2
        ///   XP 200 (CR 2):   ~40 HP, AC 14, STR 15, DEX 10, Spd 5, 2d6+3
        ///   XP 450 (CR 3):   ~55 HP, AC 15, STR 16, DEX 10, Spd 5, 2d8+3
        ///   XP 900 (CR 5):   ~80 HP, AC 16, STR 18, DEX 12, Spd 5, 2d10+4
        /// </summary>
        private static EnemyDef MakeCREnemyDef(string name, int xp, string behavior, string biome,
            DamageType atkDmgType)
        {
            // Stat block by XP tier
            return xp switch
            {
                <= 25  => new EnemyDef { Name = name, HP = 10, AC = 11, Str = 12, Dex = 14, Spd = 6, AtkDice = "1d6+1",  Behavior = behavior, CR = 0, AttackDamageType = atkDmgType },
                <= 50  => new EnemyDef { Name = name, HP = 15, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d8+1",  Behavior = behavior, CR = 1, AttackDamageType = atkDmgType },
                <= 100 => new EnemyDef { Name = name, HP = 25, AC = 13, Str = 14, Dex = 10, Spd = 5, AtkDice = "1d10+2", Behavior = behavior, CR = 1, AttackDamageType = atkDmgType },
                <= 200 => new EnemyDef { Name = name, HP = 40, AC = 14, Str = 15, Dex = 10, Spd = 5, AtkDice = "2d6+3",  Behavior = behavior, CR = 2, AttackDamageType = atkDmgType },
                <= 450 => new EnemyDef { Name = name, HP = 55, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "2d8+3",  Behavior = behavior, CR = 3, AttackDamageType = atkDmgType },
                _      => new EnemyDef { Name = name, HP = 80, AC = 16, Str = 18, Dex = 12, Spd = 5, AtkDice = "2d10+4", Behavior = behavior, CR = 5, AttackDamageType = atkDmgType },
            };
        }

        private static EncounterData DefaultEncounter()
        {
            var enc = new EncounterData { Id = "default", GoldReward = 5, XPReward = 10 };
            enc.Enemies.Add(new EnemyDef { Name = "Rat", HP = 4, AC = 10, Str = 8, Dex = 12, Spd = 6, AtkDice = "1d4", Behavior = "chase", AttackDamageType = DamageType.Piercing, CR = 0 });
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
                        new EnemyDef { Name = "Hollow Guardian", HP = 40, AC = 15, Str = 16, Dex = 10, Spd = 4, AtkDice = "2d6+3", Behavior = "guard", AttackDamageType = DamageType.Bludgeoning, CR = 3 },
                        new EnemyDef { Name = "Skeleton", HP = 10, AC = 10, Str = 10, Dex = 10, Spd = 5, AtkDice = "1d6", Behavior = "chase", AttackDamageType = DamageType.Slashing, CR = 0, Vulnerabilities = DamageType.Bludgeoning, Resistances = DamageType.Piercing },
                        new EnemyDef { Name = "Skeleton", HP = 10, AC = 10, Str = 10, Dex = 10, Spd = 5, AtkDice = "1d6", Behavior = "chase", AttackDamageType = DamageType.Slashing, CR = 0, Vulnerabilities = DamageType.Bludgeoning, Resistances = DamageType.Piercing }
                    }
                },
                ["castle_boss"] = () => new EncounterData
                {
                    Id = "castle_boss", GridWidth = 16, GridHeight = 16, GoldReward = 250, XPReward = 500,
                    Enemies = new List<EnemyDef>
                    {
                        new EnemyDef { Name = "The Rot King", HP = 80, AC = 18, Str = 20, Dex = 12, Spd = 5, AtkDice = "2d8+5", Behavior = "guard", AttackDamageType = DamageType.Necrotic, CR = 5, Resistances = DamageType.Necrotic },
                        new EnemyDef { Name = "Rot Knight", HP = 25, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "1d8+3", Behavior = "chase", AttackDamageType = DamageType.Slashing, CR = 2 },
                        new EnemyDef { Name = "Rot Knight", HP = 25, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "1d8+3", Behavior = "chase", AttackDamageType = DamageType.Slashing, CR = 2 },
                        new EnemyDef { Name = "Plague Rat", HP = 8, AC = 11, Str = 8, Dex = 14, Spd = 7, AtkDice = "1d4+1", Behavior = "chase", AttackDamageType = DamageType.Piercing, CR = 0 },
                        new EnemyDef { Name = "Plague Rat", HP = 8, AC = 11, Str = 8, Dex = 14, Spd = 7, AtkDice = "1d4+1", Behavior = "chase", AttackDamageType = DamageType.Piercing, CR = 0 }
                    }
                }
            };
        }
    }
}
