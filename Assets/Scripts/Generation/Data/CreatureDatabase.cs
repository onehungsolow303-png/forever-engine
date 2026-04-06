using System.Collections.Generic;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Generation.Data
{
    public struct CreatureStatBlock
    {
        public int HP, AC;
        public int STR, DEX, CON, INT, WIS, CHA;
        public int Speed;
        public string AtkDice;
        public DamageType AttackDamageType;
        public DamageType Resistances;
        public DamageType Vulnerabilities;
        public DamageType Immunities;
        public int CR;
        public int XP;
        public string AiBehavior;
    }

    public static class CreatureDatabase
    {
        private static Dictionary<string, CreatureStatBlock> _creatures;

        public static CreatureStatBlock GetStats(string variant)
        {
            if (_creatures == null) Init();
            if (!string.IsNullOrEmpty(variant) && _creatures.TryGetValue(variant, out var stats)) return stats;
            // Fallback for unknown variants
            return new CreatureStatBlock
            {
                HP = 8, AC = 11, STR = 10, DEX = 10, CON = 10,
                INT = 10, WIS = 10, CHA = 10, Speed = 6,
                AtkDice = "1d4", AttackDamageType = DamageType.Bludgeoning,
                CR = 0, XP = 10, AiBehavior = "chase"
            };
        }

        private static void Init()
        {
            _creatures = new Dictionary<string, CreatureStatBlock>
            {
                ["goblin"] = new CreatureStatBlock
                {
                    HP = 7, AC = 15, STR = 8, DEX = 14, CON = 10,
                    INT = 10, WIS = 8, CHA = 8, Speed = 6,
                    AtkDice = "1d6+2", AttackDamageType = DamageType.Slashing,
                    CR = 25, XP = 50, AiBehavior = "chase"
                },
                ["skeleton"] = new CreatureStatBlock
                {
                    HP = 13, AC = 13, STR = 10, DEX = 14, CON = 15,
                    INT = 6, WIS = 8, CHA = 5, Speed = 6,
                    AtkDice = "1d6+2", AttackDamageType = DamageType.Piercing,
                    Vulnerabilities = DamageType.Bludgeoning,
                    Immunities = DamageType.Poison,
                    CR = 25, XP = 50, AiBehavior = "chase"
                },
                ["zombie"] = new CreatureStatBlock
                {
                    HP = 22, AC = 8, STR = 13, DEX = 6, CON = 16,
                    INT = 3, WIS = 6, CHA = 5, Speed = 4,
                    AtkDice = "1d6+1", AttackDamageType = DamageType.Bludgeoning,
                    Immunities = DamageType.Poison,
                    CR = 25, XP = 50, AiBehavior = "chase"
                },
                ["rat"] = new CreatureStatBlock
                {
                    HP = 1, AC = 10, STR = 2, DEX = 11, CON = 9,
                    INT = 2, WIS = 10, CHA = 4, Speed = 6,
                    AtkDice = "1d1", AttackDamageType = DamageType.Piercing,
                    CR = 0, XP = 10, AiBehavior = "flee"
                },
                ["spider"] = new CreatureStatBlock
                {
                    HP = 1, AC = 12, STR = 2, DEX = 14, CON = 8,
                    INT = 1, WIS = 10, CHA = 2, Speed = 6,
                    AtkDice = "1d1", AttackDamageType = DamageType.Piercing,
                    CR = 0, XP = 10, AiBehavior = "chase"
                },
                ["bat"] = new CreatureStatBlock
                {
                    HP = 1, AC = 12, STR = 2, DEX = 15, CON = 8,
                    INT = 2, WIS = 12, CHA = 4, Speed = 6,
                    AtkDice = "1d1", AttackDamageType = DamageType.Piercing,
                    CR = 0, XP = 10, AiBehavior = "flee"
                },
                ["beetle"] = new CreatureStatBlock
                {
                    HP = 4, AC = 14, STR = 8, DEX = 10, CON = 13,
                    INT = 1, WIS = 7, CHA = 3, Speed = 4,
                    AtkDice = "1d4+1", AttackDamageType = DamageType.Piercing,
                    CR = 12, XP = 25, AiBehavior = "chase"
                },
                ["slime"] = new CreatureStatBlock
                {
                    HP = 22, AC = 8, STR = 12, DEX = 8, CON = 16,
                    INT = 1, WIS = 6, CHA = 2, Speed = 4,
                    AtkDice = "1d6+1", AttackDamageType = DamageType.Acid,
                    Resistances = DamageType.Acid,
                    Immunities = DamageType.Poison | DamageType.Lightning,
                    CR = 50, XP = 100, AiBehavior = "chase"
                },
                ["guard"] = new CreatureStatBlock
                {
                    HP = 11, AC = 16, STR = 13, DEX = 12, CON = 12,
                    INT = 10, WIS = 11, CHA = 10, Speed = 6,
                    AtkDice = "1d6+1", AttackDamageType = DamageType.Piercing,
                    CR = 12, XP = 25, AiBehavior = "guard"
                },
                ["knight"] = new CreatureStatBlock
                {
                    HP = 52, AC = 18, STR = 16, DEX = 11, CON = 14,
                    INT = 11, WIS = 11, CHA = 15, Speed = 6,
                    AtkDice = "2d6+3", AttackDamageType = DamageType.Slashing,
                    CR = 300, XP = 700, AiBehavior = "guard"
                },
                ["mage"] = new CreatureStatBlock
                {
                    HP = 40, AC = 12, STR = 9, DEX = 14, CON = 11,
                    INT = 17, WIS = 12, CHA = 11, Speed = 6,
                    AtkDice = "1d4", AttackDamageType = DamageType.Bludgeoning,
                    CR = 600, XP = 2300, AiBehavior = "chase"
                },
                ["servant"] = new CreatureStatBlock
                {
                    HP = 4, AC = 10, STR = 10, DEX = 10, CON = 10,
                    INT = 10, WIS = 10, CHA = 10, Speed = 6,
                    AtkDice = "1d2", AttackDamageType = DamageType.Bludgeoning,
                    CR = 0, XP = 10, AiBehavior = "flee"
                },
                ["wraith"] = new CreatureStatBlock
                {
                    HP = 67, AC = 13, STR = 6, DEX = 16, CON = 16,
                    INT = 12, WIS = 14, CHA = 15, Speed = 6,
                    AtkDice = "3d6+3", AttackDamageType = DamageType.Necrotic,
                    Resistances = DamageType.Acid | DamageType.Cold | DamageType.Fire | DamageType.Lightning,
                    Immunities = DamageType.Necrotic | DamageType.Poison,
                    CR = 500, XP = 1800, AiBehavior = "chase"
                },
                ["vampire_spawn"] = new CreatureStatBlock
                {
                    HP = 82, AC = 15, STR = 16, DEX = 16, CON = 16,
                    INT = 11, WIS = 10, CHA = 12, Speed = 6,
                    AtkDice = "2d6+3", AttackDamageType = DamageType.Necrotic,
                    Resistances = DamageType.Necrotic,
                    Immunities = DamageType.Poison,
                    CR = 500, XP = 1800, AiBehavior = "chase"
                },
                ["crocodile"] = new CreatureStatBlock
                {
                    HP = 19, AC = 12, STR = 15, DEX = 10, CON = 13,
                    INT = 2, WIS = 10, CHA = 5, Speed = 4,
                    AtkDice = "1d10+2", AttackDamageType = DamageType.Piercing,
                    CR = 50, XP = 100, AiBehavior = "chase"
                },
                ["thief"] = new CreatureStatBlock
                {
                    HP = 16, AC = 14, STR = 10, DEX = 16, CON = 12,
                    INT = 12, WIS = 10, CHA = 14, Speed = 6,
                    AtkDice = "1d6+3", AttackDamageType = DamageType.Piercing,
                    CR = 50, XP = 100, AiBehavior = "chase"
                },
                ["cultist"] = new CreatureStatBlock
                {
                    HP = 9, AC = 12, STR = 11, DEX = 12, CON = 10,
                    INT = 10, WIS = 11, CHA = 10, Speed = 6,
                    AtkDice = "1d6+1", AttackDamageType = DamageType.Slashing,
                    CR = 12, XP = 25, AiBehavior = "chase"
                },
                ["golem"] = new CreatureStatBlock
                {
                    HP = 93, AC = 17, STR = 19, DEX = 9, CON = 18,
                    INT = 3, WIS = 11, CHA = 1, Speed = 5,
                    AtkDice = "2d8+5", AttackDamageType = DamageType.Bludgeoning,
                    Immunities = DamageType.Poison | DamageType.Psychic,
                    CR = 500, XP = 1800, AiBehavior = "guard"
                },
                ["elemental"] = new CreatureStatBlock
                {
                    HP = 90, AC = 15, STR = 18, DEX = 14, CON = 18,
                    INT = 6, WIS = 10, CHA = 6, Speed = 6,
                    AtkDice = "2d8+4", AttackDamageType = DamageType.Fire,
                    Immunities = DamageType.Fire | DamageType.Poison,
                    CR = 500, XP = 1800, AiBehavior = "chase"
                },
                ["priest"] = new CreatureStatBlock
                {
                    HP = 27, AC = 13, STR = 10, DEX = 10, CON = 12,
                    INT = 13, WIS = 16, CHA = 13, Speed = 6,
                    AtkDice = "1d6", AttackDamageType = DamageType.Bludgeoning,
                    CR = 200, XP = 450, AiBehavior = "guard"
                },
                ["kobold"] = new CreatureStatBlock
                {
                    HP = 5, AC = 12, STR = 7, DEX = 15, CON = 9,
                    INT = 8, WIS = 7, CHA = 8, Speed = 6,
                    AtkDice = "1d4+2", AttackDamageType = DamageType.Piercing,
                    CR = 12, XP = 25, AiBehavior = "flee"
                },
                ["rust_monster"] = new CreatureStatBlock
                {
                    HP = 27, AC = 14, STR = 13, DEX = 12, CON = 13,
                    INT = 2, WIS = 13, CHA = 6, Speed = 8,
                    AtkDice = "1d6+1", AttackDamageType = DamageType.Piercing,
                    CR = 50, XP = 100, AiBehavior = "chase"
                },
                ["earth_elemental"] = new CreatureStatBlock
                {
                    HP = 126, AC = 17, STR = 20, DEX = 8, CON = 20,
                    INT = 5, WIS = 10, CHA = 5, Speed = 5,
                    AtkDice = "2d8+5", AttackDamageType = DamageType.Bludgeoning,
                    Resistances = DamageType.Piercing | DamageType.Slashing,
                    Immunities = DamageType.Poison,
                    CR = 500, XP = 1800, AiBehavior = "chase"
                },
                ["wolf"] = new CreatureStatBlock
                {
                    HP = 11, AC = 13, STR = 12, DEX = 15, CON = 12,
                    INT = 3, WIS = 12, CHA = 6, Speed = 8,
                    AtkDice = "2d4+2", AttackDamageType = DamageType.Piercing,
                    CR = 25, XP = 50, AiBehavior = "chase"
                },
                ["bear"] = new CreatureStatBlock
                {
                    HP = 34, AC = 11, STR = 19, DEX = 10, CON = 16,
                    INT = 2, WIS = 13, CHA = 7, Speed = 8,
                    AtkDice = "2d6+4", AttackDamageType = DamageType.Slashing,
                    CR = 100, XP = 200, AiBehavior = "chase"
                },
                ["treant"] = new CreatureStatBlock
                {
                    HP = 138, AC = 16, STR = 23, DEX = 8, CON = 21,
                    INT = 12, WIS = 16, CHA = 12, Speed = 6,
                    AtkDice = "3d6+6", AttackDamageType = DamageType.Bludgeoning,
                    Vulnerabilities = DamageType.Fire,
                    Resistances = DamageType.Bludgeoning | DamageType.Piercing,
                    CR = 900, XP = 5000, AiBehavior = "guard"
                },
                ["fairy"] = new CreatureStatBlock
                {
                    HP = 1, AC = 15, STR = 3, DEX = 18, CON = 8,
                    INT = 14, WIS = 12, CHA = 16, Speed = 6,
                    AtkDice = "1d1", AttackDamageType = DamageType.Radiant,
                    CR = 12, XP = 25, AiBehavior = "flee"
                },
                ["bandit"] = new CreatureStatBlock
                {
                    HP = 11, AC = 12, STR = 11, DEX = 12, CON = 12,
                    INT = 10, WIS = 10, CHA = 10, Speed = 6,
                    AtkDice = "1d6+1", AttackDamageType = DamageType.Slashing,
                    CR = 12, XP = 25, AiBehavior = "chase"
                },
                ["villager"] = new CreatureStatBlock
                {
                    HP = 4, AC = 10, STR = 10, DEX = 10, CON = 10,
                    INT = 10, WIS = 10, CHA = 10, Speed = 6,
                    AtkDice = "1d2", AttackDamageType = DamageType.Bludgeoning,
                    CR = 0, XP = 10, AiBehavior = "flee"
                },
                ["merchant"] = new CreatureStatBlock
                {
                    HP = 9, AC = 11, STR = 10, DEX = 12, CON = 10,
                    INT = 13, WIS = 14, CHA = 14, Speed = 6,
                    AtkDice = "1d4", AttackDamageType = DamageType.Bludgeoning,
                    CR = 0, XP = 10, AiBehavior = "flee"
                },
                ["imp"] = new CreatureStatBlock
                {
                    HP = 10, AC = 13, STR = 6, DEX = 17, CON = 13,
                    INT = 11, WIS = 12, CHA = 14, Speed = 4,
                    AtkDice = "1d4+3", AttackDamageType = DamageType.Piercing,
                    Resistances = DamageType.Cold,
                    Immunities = DamageType.Fire | DamageType.Poison,
                    CR = 100, XP = 200, AiBehavior = "chase"
                },
                ["animated_armor"] = new CreatureStatBlock
                {
                    HP = 33, AC = 18, STR = 14, DEX = 11, CON = 13,
                    INT = 1, WIS = 3, CHA = 1, Speed = 5,
                    AtkDice = "2d6+2", AttackDamageType = DamageType.Bludgeoning,
                    Immunities = DamageType.Poison | DamageType.Psychic,
                    CR = 100, XP = 200, AiBehavior = "guard"
                },
            };
        }
    }
}
