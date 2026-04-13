using System.Collections.Generic;
using ForeverEngine.RPG.Enums;
using UnityEngine;

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
        public string ModelId;
        public float ModelScale = 1f;
    }

    [System.Serializable]
    public class EncounterData
    {
        public string Id;
        public int GridWidth = 8, GridHeight = 8;
        public List<EnemyDef> Enemies = new();
        public int GoldReward;
        public int XPReward;
        public string Biome;

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
            if (id.Contains("Forest")) enc.Biome = "forest";
            else if (id.Contains("Dungeon") || id.Contains("dungeon") || id.Contains("Crypt")) enc.Biome = "dungeon";
            else if (id.Contains("Road")) enc.Biome = "ruins";
            else enc.Biome = "plains";

            // Get player level for XP budget calculation
            int playerLevel = GameManager.Instance?.Character?.TotalLevel
                ?? GameManager.Instance?.Player?.Level ?? 1;

            // Phase 3 pivot: AIDirector archived to Director Hub. Pacing
            // multiplier defaults to 1.0; will be reintroduced via
            // DirectorClient in a follow-up.
            float pacingMult = 1.0f;

            // XP budget: Medium difficulty baseline, night = Hard.
            // Rates read from GameConfig when available so they can be tuned in the Inspector.
            // Tamed from the original 50/75 per the combat-balance-audit.md Option C
            // recommendation. Player HP scales 20 + (level-1)*5 via PlayerData.LevelUp(),
            // so a level-5 player has 40 HP and can survive a tamed encounter without
            // dying in round 2. The audit's player-first-in-round-1 fix from c6fc22d
            // handles "no turn"; this handles "still dies after one turn."
            var config = Resources.Load<GameConfig>("GameConfig");
            int dayMult = config != null ? config.DayXPBudgetPerLevel : 40;
            int nightMult = config != null ? config.NightXPBudgetPerLevel : 60;
            int maxEnemies = config != null ? config.MaxEnemiesPerEncounter : 4;
            int xpBudget = (int)((night ? nightMult : dayMult) * playerLevel * pacingMult);

            // Parse tier from dungeon encounter ID (e.g. "random_dungeon_t2_room3")
            int tier = 1; // default
            if (id.Contains("_t1_")) tier = 1;
            else if (id.Contains("_t2_")) tier = 2;
            else if (id.Contains("_t3_")) tier = 3;

            // Tier scaling: budget multiplier and minimum XP floor per enemy
            float tierBudgetMult = tier switch { 1 => 0.8f, 2 => 1.2f, 3 => 1.6f, _ => 1f };
            int tierMinXP = tier switch { 1 => 0, 2 => 50, 3 => 100, _ => 0 }; // skip weak enemies
            float tierRewardMult = tier switch { 1 => 1.0f, 2 => 1.3f, 3 => 1.6f, _ => 1f };

            // Only apply tier scaling to dungeon encounters
            if (id.Contains("dungeon") || id.Contains("Dungeon"))
                xpBudget = (int)(xpBudget * tierBudgetMult);

            // RNG seeded off the encounter ID + player level so the same hex
            // doesn't always spawn the same composition.
            var rng = new System.Random(id.GetHashCode() ^ (playerLevel * 7919));

            // Template-based encounter selection — produces thematic heterogeneous groups.
            float templateChance = config != null ? config.EncounterTemplateChance : 0.6f;
            var templates = EncounterTemplate.FindMatching(xpBudget, enc.Biome ?? "plains");
            if (templates.Count > 0 && rng.NextDouble() < templateChance)
            {
                var template = templates[rng.Next(templates.Count)];
                int added = 0;
                foreach (var slot in template.Slots)
                {
                    if (added >= maxEnemies) break;
                    var def = MakeCREnemyDef(slot.Name, slot.XPCost, slot.Behavior, enc.Biome ?? "plains",
                        DamageType.Slashing);
                    // MakeCREnemyDef already called ModelRegistry.Resolve with the slot name
                    enc.Enemies.Add(def);
                    added++;
                }
                enc.GoldReward = template.TotalXP / 4;
                enc.XPReward = template.TotalXP;
                return enc;
            }

            if (id.Contains("Forest"))
            {
                // Wolf encounters. 50% pack of CR1/4 wolves, 25% pack with a
                // CR1/2 lead Dire Wolf, 15% (level 3+) lone CR2 Alpha + 1-2 pups,
                // 10% Orc raiding party.
                int roll = rng.Next(100);
                int count;
                if (playerLevel >= 3 && roll < 15)
                {
                    // Alpha pack
                    enc.Enemies.Add(MakeCREnemyDef("Alpha Wolf", 200, "guard", "Forest", DamageType.Piercing));
                    int pups = 1 + rng.Next(2);
                    for (int i = 0; i < pups; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Wolf Pup", 25, "chase", "Forest", DamageType.Piercing));
                    enc.GoldReward = 30; enc.XPReward = 200 + 25 * pups;
                }
                else if (roll < 40)
                {
                    // Dire Wolf + pack
                    enc.Enemies.Add(MakeCREnemyDef("Dire Wolf", 100, "chase", "Forest", DamageType.Piercing));
                    count = System.Math.Max(0, (xpBudget - 100) / 25);
                    count = System.Math.Min(count, 3);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Wolf", 25, "chase", "Forest", DamageType.Piercing));
                    enc.GoldReward = 15 + 5 * count; enc.XPReward = 100 + 25 * count;
                }
                else if (roll < 90 || playerLevel < 2)
                {
                    // Standard pack — capped at 4 (was 5) per balance audit Option C
                    count = System.Math.Max(1, xpBudget / 25);
                    count = System.Math.Min(count, 4);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Wolf", 25, "chase", "Forest", DamageType.Piercing));
                    enc.GoldReward = 5 * count; enc.XPReward = 25 * count;
                }
                else
                {
                    // Orc raiding party — 10% chance (level 2+) for variety
                    enc.Enemies.Add(MakeCREnemyDef("Orc", 100, "guard", "Forest", DamageType.Slashing));
                    count = System.Math.Max(0, (xpBudget - 100) / 25);
                    count = System.Math.Min(count, maxEnemies - 1);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Kobold", 25, "chase", "Forest", DamageType.Slashing));
                    enc.GoldReward = 20 + 5 * count; enc.XPReward = 100 + 25 * count;
                }
            }
            else if (id.Contains("Dungeon") || id.Contains("dungeon") || id.Contains("Crypt"))
            {
                // Dungeon / crypt encounters — undead and lizardfolk patrols.
                float rollF = (float)rng.NextDouble();
                if (rollF < 0.3f)
                {
                    // Skeleton garrison
                    int effectiveXP = System.Math.Max(50, tierMinXP);
                    int count = System.Math.Min(xpBudget / effectiveXP, maxEnemies);
                    count = System.Math.Max(1, count);
                    for (int i = 0; i < count; i++)
                    {
                        var def = MakeCREnemyDef(
                            rng.NextDouble() < 0.5 ? "Skeleton" : "Skeleton Archer",
                            50, "guard", "Ruins", DamageType.Slashing);
                        def.Vulnerabilities = DamageType.Bludgeoning;
                        def.Resistances = DamageType.Piercing;
                        enc.Enemies.Add(def);
                    }
                    enc.GoldReward = 10 * count; enc.XPReward = 50 * count;
                }
                else if (rollF < 0.6f)
                {
                    // Mummy + minions
                    int remainBudget = xpBudget;
                    if (remainBudget >= 200)
                    {
                        var boss = MakeCREnemyDef("Mummy", 200, "chase", "Ruins", DamageType.Bludgeoning);
                        enc.Enemies.Add(boss);
                        remainBudget -= 200;
                    }
                    int minionXP = System.Math.Max(25, tierMinXP);
                    int minions = System.Math.Min(remainBudget / minionXP, maxEnemies - enc.Enemies.Count);
                    minions = System.Math.Max(0, minions);
                    for (int i = 0; i < minions; i++)
                    {
                        var def = MakeCREnemyDef("Skeleton", minionXP, "guard", "Ruins", DamageType.Slashing);
                        def.Vulnerabilities = DamageType.Bludgeoning;
                        def.Resistances = DamageType.Piercing;
                        enc.Enemies.Add(def);
                    }
                    bool hasMummy = enc.Enemies.Count > 0;
                    enc.GoldReward = 25 + 5 * minions; enc.XPReward = (hasMummy ? 200 : 0) + 25 * minions;
                }
                else
                {
                    // Lizardfolk patrol
                    int effectiveXP2 = System.Math.Max(50, tierMinXP);
                    int count = System.Math.Min(xpBudget / effectiveXP2, maxEnemies);
                    count = System.Math.Max(1, count);
                    for (int i = 0; i < count; i++)
                    {
                        var def = MakeCREnemyDef(
                            rng.NextDouble() < 0.4 ? "Lizard Folk Archer" : "Lizard Folk",
                            50, i == 0 ? "guard" : "chase", "Ruins", DamageType.Slashing);
                        enc.Enemies.Add(def);
                    }
                    enc.GoldReward = 15 * count; enc.XPReward = 50 * count;
                }
            }
            else if (id.Contains("Road")) // Ruins
            {
                if (night)
                {
                    int roll = rng.Next(100);
                    if (roll < 30 && playerLevel >= 2)
                    {
                        // Mutant Hulk + plague rats
                        enc.Enemies.Add(MakeCREnemyDef("Mutant Hulk", 200, "chase", "Ruins", DamageType.Bludgeoning));
                        int rats = 1 + rng.Next(3);
                        for (int i = 0; i < rats; i++)
                            enc.Enemies.Add(MakeCREnemyDef("Plague Rat", 25, "chase", "Ruins", DamageType.Piercing));
                        enc.GoldReward = 20; enc.XPReward = 200 + 25 * rats;
                    }
                    else
                    {
                        int count = System.Math.Max(1, xpBudget / 100);
                        count = System.Math.Min(count, 4);
                        for (int i = 0; i < count; i++)
                            enc.Enemies.Add(MakeCREnemyDef("Mutant", 100, "chase", "Ruins", DamageType.Bludgeoning));
                        enc.GoldReward = 15 * count; enc.XPReward = 100 * count;
                    }
                }
                else
                {
                    int roll = rng.Next(100);
                    if (roll < 25 && playerLevel >= 2)
                    {
                        // Skeleton Archer mixed in with footmen
                        int archers = 1 + rng.Next(2);
                        for (int i = 0; i < archers; i++)
                        {
                            var sa = MakeCREnemyDef("Skeleton Archer", 50, "guard", "Ruins", DamageType.Piercing);
                            sa.Vulnerabilities = DamageType.Bludgeoning;
                            enc.Enemies.Add(sa);
                        }
                        int foot = System.Math.Max(0, (xpBudget - 50 * archers) / 25);
                        foot = System.Math.Min(foot, 3);
                        for (int i = 0; i < foot; i++)
                        {
                            var skel = MakeCREnemyDef("Skeleton", 25, "guard", "Ruins", DamageType.Slashing);
                            skel.Vulnerabilities = DamageType.Bludgeoning;
                            skel.Resistances = DamageType.Piercing;
                            enc.Enemies.Add(skel);
                        }
                        enc.GoldReward = 15 + 5 * foot; enc.XPReward = 50 * archers + 25 * foot;
                    }
                    else
                    {
                        int count = System.Math.Max(1, xpBudget / 25);
                        count = System.Math.Min(count, 4);
                        for (int i = 0; i < count; i++)
                        {
                            var skel = MakeCREnemyDef("Skeleton", 25, "guard", "Ruins", DamageType.Slashing);
                            skel.Vulnerabilities = DamageType.Bludgeoning;
                            skel.Resistances = DamageType.Piercing;
                            enc.Enemies.Add(skel);
                        }
                        enc.GoldReward = 10 * count; enc.XPReward = 25 * count;
                    }
                }
            }
            else // Plains
            {
                int roll = rng.Next(100);
                if (roll < 20 && playerLevel >= 2)
                {
                    // Bandit Captain + crew
                    enc.Enemies.Add(MakeCREnemyDef("Bandit Captain", 200, "guard", "Plains", DamageType.Slashing));
                    int crew = System.Math.Max(0, (xpBudget - 200) / 50);
                    crew = System.Math.Min(crew, 2);
                    for (int i = 0; i < crew; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Bandit", 50, "chase", "Plains", DamageType.Slashing));
                    enc.GoldReward = 50 + 15 * crew; enc.XPReward = 200 + 50 * crew;
                }
                else if (night && roll < 50 && playerLevel >= 2)
                {
                    // Cultist ambush — pierces armor with daggers
                    int count = System.Math.Max(1, xpBudget / 50);
                    count = System.Math.Min(count, 3);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Cultist", 50, "chase", "Plains", DamageType.Piercing));
                    enc.GoldReward = 10 * count; enc.XPReward = 50 * count;
                }
                else
                {
                    int count = System.Math.Max(1, xpBudget / 50);
                    count = System.Math.Min(count, 4);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Bandit", 50, "chase", "Plains", DamageType.Slashing));
                    enc.GoldReward = 15 * count; enc.XPReward = 50 * count;
                }
            }

            // Apply tier reward scaling for dungeon encounters
            if ((id.Contains("dungeon") || id.Contains("Dungeon")) && tierRewardMult != 1f)
            {
                enc.GoldReward = (int)(enc.GoldReward * tierRewardMult);
                enc.XPReward = (int)(enc.XPReward * tierRewardMult);
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
            var def = xp switch
            {
                <= 25  => new EnemyDef { Name = name, HP = 10, AC = 11, Str = 12, Dex = 14, Spd = 6, AtkDice = "1d6+1",  Behavior = behavior, CR = 0, AttackDamageType = atkDmgType },
                <= 50  => new EnemyDef { Name = name, HP = 15, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d8+1",  Behavior = behavior, CR = 1, AttackDamageType = atkDmgType },
                <= 100 => new EnemyDef { Name = name, HP = 25, AC = 13, Str = 14, Dex = 10, Spd = 5, AtkDice = "1d10+2", Behavior = behavior, CR = 1, AttackDamageType = atkDmgType },
                <= 200 => new EnemyDef { Name = name, HP = 40, AC = 14, Str = 15, Dex = 10, Spd = 5, AtkDice = "2d6+3",  Behavior = behavior, CR = 2, AttackDamageType = atkDmgType },
                <= 450 => new EnemyDef { Name = name, HP = 55, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "2d8+3",  Behavior = behavior, CR = 3, AttackDamageType = atkDmgType },
                _      => new EnemyDef { Name = name, HP = 80, AC = 16, Str = 18, Dex = 12, Spd = 5, AtkDice = "2d10+4", Behavior = behavior, CR = 5, AttackDamageType = atkDmgType },
            };
            var (modelPath, modelScale) = ForeverEngine.Demo.Battle.ModelRegistry.Resolve(def.Name);
            if (modelPath != null)
            {
                def.ModelId = modelPath;
                def.ModelScale = modelScale;
            }
            return def;
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
                ["boss_dungeon"] = () => new EncounterData
                {
                    Id = "boss_dungeon", GridWidth = 12, GridHeight = 12, GoldReward = 100, XPReward = 200,
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
