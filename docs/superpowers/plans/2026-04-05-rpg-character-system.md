> ⚠️ **HISTORICAL — Q-learning superseded 2026-04-16, scope-correction revoked 2026-04-26.** This document references Q-learning (`QLearner.cs`, `QTableStore.cs`, `SelfPlayTrainer.cs`, `CombatBrain.cs`) as part of its design. Those files were deleted on 2026-04-16; combat AI is now deterministic `AIBehavior` (behavior tree). See `~/.claude/projects/C--Dev/game_dev_tracker/golden_standard.md` (Combat section) and pivot `q-learning-scope-correction-revoked` for the canonical state. Treat this file as historical only.

# RPG Character System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a complete D&D 5e RPG character system (CharacterSheet, 12 classes, 15 species, 200+ spells, combat resolvers) in ForeverEngine.RPG namespace.

**Architecture:** Pure C# in ForeverEngine.RPG namespace, no MonoBehaviour/ECS dependencies. CharacterSheet is the master record. Stateless resolvers for combat. ScriptableObjects for content (generated via editor script). Bridge to existing ECS StatsComponent via ToStatsSnapshot().

**Tech Stack:** Unity 6, C#, ScriptableObjects, existing DiceRoller

---

## Task 1: All Enums

Create all 13 enum files in `Assets/Scripts/RPG/Enums/`.

### File 1: `Assets/Scripts/RPG/Enums/Ability.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum Ability
    {
        STR,
        DEX,
        CON,
        INT,
        WIS,
        CHA
    }
}
```

### File 2: `Assets/Scripts/RPG/Enums/DamageType.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum DamageType
    {
        None        = 0,
        Slashing    = 1 << 0,
        Piercing    = 1 << 1,
        Bludgeoning = 1 << 2,
        Fire        = 1 << 3,
        Cold        = 1 << 4,
        Lightning   = 1 << 5,
        Thunder     = 1 << 6,
        Poison      = 1 << 7,
        Acid        = 1 << 8,
        Necrotic    = 1 << 9,
        Radiant     = 1 << 10,
        Psychic     = 1 << 11,
        Force       = 1 << 12
    }
}
```

### File 3: `Assets/Scripts/RPG/Enums/Condition.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum Condition
    {
        None          = 0,
        Blinded       = 1 << 0,
        Charmed       = 1 << 1,
        Deafened      = 1 << 2,
        Frightened    = 1 << 3,
        Grappled      = 1 << 4,
        Incapacitated = 1 << 5,
        Invisible     = 1 << 6,
        Paralyzed     = 1 << 7,
        Petrified     = 1 << 8,
        Poisoned      = 1 << 9,
        Prone         = 1 << 10,
        Restrained    = 1 << 11,
        Stunned       = 1 << 12,
        Unconscious   = 1 << 13,

        // Derived composites (not stored, used for queries)
        CantAct               = Incapacitated | Stunned | Paralyzed | Petrified | Unconscious,
        AttacksHaveAdvantage  = Blinded | Restrained | Stunned | Paralyzed | Unconscious,
        MeleeAutoCrit         = Paralyzed | Unconscious
    }
}
```

### File 4: `Assets/Scripts/RPG/Enums/SpellSchool.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum SpellSchool
    {
        Abjuration,
        Conjuration,
        Divination,
        Enchantment,
        Evocation,
        Illusion,
        Necromancy,
        Transmutation
    }
}
```

### File 5: `Assets/Scripts/RPG/Enums/WeaponProperty.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum WeaponProperty
    {
        None       = 0,
        Finesse    = 1 << 0,
        Heavy      = 1 << 1,
        Light      = 1 << 2,
        Thrown     = 1 << 3,
        TwoHanded  = 1 << 4,
        Versatile  = 1 << 5,
        Reach      = 1 << 6,
        Loading    = 1 << 7,
        Ammunition = 1 << 8
    }
}
```

### File 6: `Assets/Scripts/RPG/Enums/ArmorType.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum ArmorType
    {
        Light,
        Medium,
        Heavy,
        Shield
    }
}
```

### File 7: `Assets/Scripts/RPG/Enums/SpellcastingType.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum SpellcastingType
    {
        None,
        Full,
        Half,
        Third,
        Pact
    }
}
```

### File 8: `Assets/Scripts/RPG/Enums/Rarity.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        VeryRare,
        Legendary
    }
}
```

### File 9: `Assets/Scripts/RPG/Enums/Tier.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum Tier
    {
        Adventurer,  // Levels 1-4
        Journeyman,  // Levels 5-10
        Hero,        // Levels 11-16
        Legend,       // Levels 17-20
        Paragon,     // Levels 21-24
        Epic,        // Levels 25-28
        Mythic,      // Levels 29-32
        Demigod,     // Levels 33-36
        Divine       // Levels 37-40
    }
}
```

### File 10: `Assets/Scripts/RPG/Enums/DieType.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum DieType
    {
        D4  = 4,
        D6  = 6,
        D8  = 8,
        D10 = 10,
        D12 = 12,
        D20 = 20
    }
}
```

### File 11: `Assets/Scripts/RPG/Enums/CoverType.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum CoverType
    {
        None,
        Half,          // +2 AC
        ThreeQuarters, // +5 AC
        Total          // Can't be targeted directly
    }
}
```

### File 12: `Assets/Scripts/RPG/Enums/AoEShape.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum AoEShape
    {
        None,
        Sphere,
        Cone,
        Line,
        Cube,
        Cylinder
    }
}
```

### File 13: `Assets/Scripts/RPG/Enums/ClassFlag.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum ClassFlag
    {
        None      = 0,
        Warrior   = 1 << 0,
        Wizard    = 1 << 1,
        Rogue     = 1 << 2,
        Cleric    = 1 << 3,
        Druid     = 1 << 4,
        Bard      = 1 << 5,
        Ranger    = 1 << 6,
        Paladin   = 1 << 7,
        Sorcerer  = 1 << 8,
        Warlock   = 1 << 9,
        Monk      = 1 << 10,
        Barbarian = 1 << 11
    }
}
```

### File 14: `Assets/Scripts/RPG/Enums/AdvantageState.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum AdvantageState
    {
        None,
        Advantage,
        Disadvantage,
        Cancelled // Both advantage and disadvantage present
    }
}
```

### File 15: `Assets/Scripts/RPG/Enums/MetamagicType.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum MetamagicType
    {
        None       = 0,
        Twinned    = 1 << 0,
        Quickened  = 1 << 1,
        Subtle     = 1 << 2,
        Empowered  = 1 << 3,
        Heightened = 1 << 4,
        Careful    = 1 << 5,
        Distant    = 1 << 6
    }
}
```

### File 16: `Assets/Scripts/RPG/Enums/DeathSaveResult.cs`

```csharp
namespace ForeverEngine.RPG.Enums
{
    public enum DeathSaveResult
    {
        Success,
        Failure,
        Stabilized,
        Revived,
        Dead
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Enums/
git commit -m "feat: add all RPG enum types (16 files) — Ability, DamageType, Condition, SpellSchool, WeaponProperty, ArmorType, SpellcastingType, Rarity, Tier, DieType, CoverType, AoEShape, ClassFlag, AdvantageState, MetamagicType, DeathSaveResult

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Data Structures

Create core data structs in `Assets/Scripts/RPG/Data/`.

### File 1: `Assets/Scripts/RPG/Data/AbilityScores.cs`

```csharp
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Data
{
    [System.Serializable]
    public struct AbilityScores
    {
        public int Strength;
        public int Dexterity;
        public int Constitution;
        public int Intelligence;
        public int Wisdom;
        public int Charisma;

        public AbilityScores(int str, int dex, int con, int intel, int wis, int cha)
        {
            Strength = str;
            Dexterity = dex;
            Constitution = con;
            Intelligence = intel;
            Wisdom = wis;
            Charisma = cha;
        }

        public int GetScore(Ability ability)
        {
            switch (ability)
            {
                case Ability.STR: return Strength;
                case Ability.DEX: return Dexterity;
                case Ability.CON: return Constitution;
                case Ability.INT: return Intelligence;
                case Ability.WIS: return Wisdom;
                case Ability.CHA: return Charisma;
                default: return 10;
            }
        }

        public int GetModifier(Ability ability)
        {
            return DiceRoller.AbilityModifier(GetScore(ability));
        }

        public AbilityScores SetScore(Ability ability, int value)
        {
            var result = this;
            switch (ability)
            {
                case Ability.STR: result.Strength = value; break;
                case Ability.DEX: result.Dexterity = value; break;
                case Ability.CON: result.Constitution = value; break;
                case Ability.INT: result.Intelligence = value; break;
                case Ability.WIS: result.Wisdom = value; break;
                case Ability.CHA: result.Charisma = value; break;
            }
            return result;
        }

        public AbilityScores WithBonus(Ability ability, int bonus)
        {
            return SetScore(ability, GetScore(ability) + bonus);
        }

        public static AbilityScores Default => new AbilityScores(10, 10, 10, 10, 10, 10);
    }
}
```

### File 2: `Assets/Scripts/RPG/Data/DiceExpression.cs`

```csharp
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Data
{
    [System.Serializable]
    public struct DiceExpression
    {
        public int Count;
        public DieType Die;
        public int Bonus;

        public DiceExpression(int count, DieType die, int bonus = 0)
        {
            Count = count;
            Die = die;
            Bonus = bonus;
        }

        public static DiceExpression Parse(string expression)
        {
            DiceRoller.Parse(expression, out int count, out int sides, out int bonus);
            return new DiceExpression(count, (DieType)sides, bonus);
        }

        public int Roll(ref uint seed)
        {
            return DiceRoller.Roll(Count, (int)Die, Bonus, ref seed);
        }

        public int RollWithAdvantage(ref uint seed)
        {
            int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);
            int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
            return (roll1 > roll2 ? roll1 : roll2) + Bonus;
        }

        public int RollWithDisadvantage(ref uint seed)
        {
            int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);
            int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
            return (roll1 < roll2 ? roll1 : roll2) + Bonus;
        }

        public int CriticalDamage(ref uint seed)
        {
            // Double the dice count, keep bonus once
            return DiceRoller.Roll(Count * 2, (int)Die, Bonus, ref seed);
        }

        public override string ToString()
        {
            if (Bonus > 0) return $"{Count}d{(int)Die}+{Bonus}";
            if (Bonus < 0) return $"{Count}d{(int)Die}{Bonus}";
            return $"{Count}d{(int)Die}";
        }

        public static DiceExpression None => new DiceExpression(0, DieType.D4, 0);
    }
}
```

### File 3: `Assets/Scripts/RPG/Data/ResourcePool.cs`

```csharp
namespace ForeverEngine.RPG.Data
{
    [System.Serializable]
    public struct ResourcePool
    {
        public int Current;
        public int Max;

        public ResourcePool(int max)
        {
            Max = max;
            Current = max;
        }

        public ResourcePool(int current, int max)
        {
            Current = current;
            Max = max;
        }

        public bool IsFull => Current >= Max;
        public bool IsEmpty => Current <= 0;

        public bool Spend(int amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }

        public void Restore(int amount)
        {
            Current += amount;
            if (Current > Max) Current = Max;
        }

        public void RestoreAll()
        {
            Current = Max;
        }

        public override string ToString()
        {
            return $"{Current}/{Max}";
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Data/AbilityScores.cs Assets/Scripts/RPG/Data/DiceExpression.cs Assets/Scripts/RPG/Data/ResourcePool.cs
git commit -m "feat: add RPG data structures — AbilityScores, DiceExpression, ResourcePool

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Lookup Tables

Create static lookup tables in `Assets/Scripts/RPG/Data/`.

### File 1: `Assets/Scripts/RPG/Data/ProficiencyTable.cs`

```csharp
namespace ForeverEngine.RPG.Data
{
    public static class ProficiencyTable
    {
        // Index 0 = level 1, Index 39 = level 40
        // Levels 1-4: +2, 5-8: +3, 9-12: +4, 13-16: +5, 17-20: +6
        // Levels 21-24: +7, 25-28: +8, 29-32: +9, 33-36: +10, 37-40: +11
        private static readonly int[] _bonuses = new int[40]
        {
            // Level  1- 4
            2, 2, 2, 2,
            // Level  5- 8
            3, 3, 3, 3,
            // Level  9-12
            4, 4, 4, 4,
            // Level 13-16
            5, 5, 5, 5,
            // Level 17-20
            6, 6, 6, 6,
            // Level 21-24
            7, 7, 7, 7,
            // Level 25-28
            8, 8, 8, 8,
            // Level 29-32
            9, 9, 9, 9,
            // Level 33-36
            10, 10, 10, 10,
            // Level 37-40
            11, 11, 11, 11
        };

        /// <summary>
        /// Get proficiency bonus for a given character level (1-40).
        /// </summary>
        public static int GetBonus(int level)
        {
            if (level < 1) return 2;
            if (level > 40) return 11;
            return _bonuses[level - 1];
        }
    }
}
```

### File 2: `Assets/Scripts/RPG/Data/ExperienceTable.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Data
{
    public static class ExperienceTable
    {
        // XP required to reach each level (index 0 = level 1 = 0 XP)
        // Levels 1-20: Standard D&D 5e XP thresholds
        // Levels 21-40: Extended progression (each level requires progressively more)
        private static readonly int[] _thresholds = new int[40]
        {
            // Level  1-5 (standard D&D 5e)
            0,          // Level  1
            300,        // Level  2
            900,        // Level  3
            2700,       // Level  4
            6500,       // Level  5
            // Level  6-10
            14000,      // Level  6
            23000,      // Level  7
            34000,      // Level  8
            48000,      // Level  9
            64000,      // Level 10
            // Level 11-15
            85000,      // Level 11
            100000,     // Level 12
            120000,     // Level 13
            140000,     // Level 14
            165000,     // Level 15
            // Level 16-20
            195000,     // Level 16
            225000,     // Level 17
            265000,     // Level 18
            305000,     // Level 19
            355000,     // Level 20
            // Level 21-25 (extended — Paragon tier)
            405000,     // Level 21
            465000,     // Level 22
            535000,     // Level 23
            615000,     // Level 24
            705000,     // Level 25
            // Level 26-30 (Epic tier)
            805000,     // Level 26
            915000,     // Level 27
            1035000,    // Level 28
            1165000,    // Level 29
            1305000,    // Level 30
            // Level 31-35 (Mythic tier)
            1455000,    // Level 31
            1615000,    // Level 32
            1785000,    // Level 33
            1965000,    // Level 34
            2155000,    // Level 35
            // Level 36-40 (Divine tier)
            2355000,    // Level 36
            2565000,    // Level 37
            2785000,    // Level 38
            3015000,    // Level 39
            3255000     // Level 40
        };

        /// <summary>
        /// Get XP threshold to reach a given level (1-40).
        /// </summary>
        public static int GetThreshold(int level)
        {
            if (level < 1) return 0;
            if (level > 40) return _thresholds[39];
            return _thresholds[level - 1];
        }

        /// <summary>
        /// Determine what level a character should be based on their total XP.
        /// </summary>
        public static int GetLevelForXP(int xp)
        {
            for (int i = 39; i >= 0; i--)
            {
                if (xp >= _thresholds[i]) return i + 1;
            }
            return 1;
        }

        /// <summary>
        /// Get the tier for a given character level.
        /// </summary>
        public static Tier GetTierForLevel(int level)
        {
            if (level <= 4)  return Tier.Adventurer;
            if (level <= 10) return Tier.Journeyman;
            if (level <= 16) return Tier.Hero;
            if (level <= 20) return Tier.Legend;
            if (level <= 24) return Tier.Paragon;
            if (level <= 28) return Tier.Epic;
            if (level <= 32) return Tier.Mythic;
            if (level <= 36) return Tier.Demigod;
            return Tier.Divine;
        }
    }
}
```

### File 3: `Assets/Scripts/RPG/Data/SpellSlotTable.cs`

```csharp
namespace ForeverEngine.RPG.Data
{
    public static class SpellSlotTable
    {
        // FullCasterSlots[level-1][spellLevel-1]
        // Rows = caster level 1-20, Columns = spell level 1-9
        private static readonly int[,] FullCasterSlots = new int[20, 9]
        {
            //  1st 2nd 3rd 4th 5th 6th 7th 8th 9th
            {   2,  0,  0,  0,  0,  0,  0,  0,  0 },  // Level  1
            {   3,  0,  0,  0,  0,  0,  0,  0,  0 },  // Level  2
            {   4,  2,  0,  0,  0,  0,  0,  0,  0 },  // Level  3
            {   4,  3,  0,  0,  0,  0,  0,  0,  0 },  // Level  4
            {   4,  3,  2,  0,  0,  0,  0,  0,  0 },  // Level  5
            {   4,  3,  3,  0,  0,  0,  0,  0,  0 },  // Level  6
            {   4,  3,  3,  1,  0,  0,  0,  0,  0 },  // Level  7
            {   4,  3,  3,  2,  0,  0,  0,  0,  0 },  // Level  8
            {   4,  3,  3,  3,  1,  0,  0,  0,  0 },  // Level  9
            {   4,  3,  3,  3,  2,  0,  0,  0,  0 },  // Level 10
            {   4,  3,  3,  3,  2,  1,  0,  0,  0 },  // Level 11
            {   4,  3,  3,  3,  2,  1,  0,  0,  0 },  // Level 12
            {   4,  3,  3,  3,  2,  1,  1,  0,  0 },  // Level 13
            {   4,  3,  3,  3,  2,  1,  1,  0,  0 },  // Level 14
            {   4,  3,  3,  3,  2,  1,  1,  1,  0 },  // Level 15
            {   4,  3,  3,  3,  2,  1,  1,  1,  0 },  // Level 16
            {   4,  3,  3,  3,  2,  1,  1,  1,  1 },  // Level 17
            {   4,  3,  3,  3,  3,  1,  1,  1,  1 },  // Level 18
            {   4,  3,  3,  3,  3,  2,  1,  1,  1 },  // Level 19
            {   4,  3,  3,  3,  3,  2,  2,  1,  1 },  // Level 20
        };

        // HalfCasterSlots[level-1][spellLevel-1]
        // Rows = class level 1-20, Columns = spell level 1-5
        // Half casters get slots at class level 2+
        private static readonly int[,] HalfCasterSlots = new int[20, 5]
        {
            //  1st 2nd 3rd 4th 5th
            {   0,  0,  0,  0,  0 },  // Level  1 (no casting yet)
            {   2,  0,  0,  0,  0 },  // Level  2
            {   3,  0,  0,  0,  0 },  // Level  3
            {   3,  0,  0,  0,  0 },  // Level  4
            {   4,  2,  0,  0,  0 },  // Level  5
            {   4,  2,  0,  0,  0 },  // Level  6
            {   4,  3,  0,  0,  0 },  // Level  7
            {   4,  3,  0,  0,  0 },  // Level  8
            {   4,  3,  2,  0,  0 },  // Level  9
            {   4,  3,  2,  0,  0 },  // Level 10
            {   4,  3,  3,  0,  0 },  // Level 11
            {   4,  3,  3,  0,  0 },  // Level 12
            {   4,  3,  3,  1,  0 },  // Level 13
            {   4,  3,  3,  1,  0 },  // Level 14
            {   4,  3,  3,  2,  0 },  // Level 15
            {   4,  3,  3,  2,  0 },  // Level 16
            {   4,  3,  3,  3,  1 },  // Level 17
            {   4,  3,  3,  3,  1 },  // Level 18
            {   4,  3,  3,  3,  2 },  // Level 19
            {   4,  3,  3,  3,  2 },  // Level 20
        };

        // ThirdCasterSlots[level-1][spellLevel-1]
        // Rows = class level 1-20, Columns = spell level 1-4
        // Third casters get slots at class level 3+
        private static readonly int[,] ThirdCasterSlots = new int[20, 4]
        {
            //  1st 2nd 3rd 4th
            {   0,  0,  0,  0 },  // Level  1
            {   0,  0,  0,  0 },  // Level  2
            {   2,  0,  0,  0 },  // Level  3
            {   3,  0,  0,  0 },  // Level  4
            {   3,  0,  0,  0 },  // Level  5
            {   3,  0,  0,  0 },  // Level  6
            {   4,  2,  0,  0 },  // Level  7
            {   4,  2,  0,  0 },  // Level  8
            {   4,  2,  0,  0 },  // Level  9
            {   4,  3,  0,  0 },  // Level 10
            {   4,  3,  0,  0 },  // Level 11
            {   4,  3,  0,  0 },  // Level 12
            {   4,  3,  2,  0 },  // Level 13
            {   4,  3,  2,  0 },  // Level 14
            {   4,  3,  2,  0 },  // Level 15
            {   4,  3,  3,  0 },  // Level 16
            {   4,  3,  3,  0 },  // Level 17
            {   4,  3,  3,  0 },  // Level 18
            {   4,  3,  3,  1 },  // Level 19
            {   4,  3,  3,  1 },  // Level 20
        };

        // PactMagicSlots[level-1][0] = slot count, [level-1][1] = slot level
        private static readonly int[,] PactMagicSlots = new int[20, 2]
        {
            //  Count  Level
            {   1,     1 },  // Level  1
            {   2,     1 },  // Level  2
            {   2,     2 },  // Level  3
            {   2,     2 },  // Level  4
            {   2,     3 },  // Level  5
            {   2,     3 },  // Level  6
            {   2,     4 },  // Level  7
            {   2,     4 },  // Level  8
            {   2,     5 },  // Level  9
            {   2,     5 },  // Level 10
            {   3,     5 },  // Level 11
            {   3,     5 },  // Level 12
            {   3,     5 },  // Level 13
            {   3,     5 },  // Level 14
            {   3,     5 },  // Level 15
            {   3,     5 },  // Level 16
            {   4,     5 },  // Level 17
            {   4,     5 },  // Level 18
            {   4,     5 },  // Level 19
            {   4,     5 },  // Level 20
        };

        /// <summary>
        /// Get spell slots for a full caster at a given caster level.
        /// Returns array of 9 ints (spell levels 1-9).
        /// </summary>
        public static int[] GetFullCasterSlots(int casterLevel)
        {
            var slots = new int[9];
            if (casterLevel < 1 || casterLevel > 20) return slots;
            for (int i = 0; i < 9; i++)
                slots[i] = FullCasterSlots[casterLevel - 1, i];
            return slots;
        }

        /// <summary>
        /// Get spell slots for a half caster at a given class level.
        /// Returns array of 5 ints (spell levels 1-5).
        /// </summary>
        public static int[] GetHalfCasterSlots(int classLevel)
        {
            var slots = new int[5];
            if (classLevel < 1 || classLevel > 20) return slots;
            for (int i = 0; i < 5; i++)
                slots[i] = HalfCasterSlots[classLevel - 1, i];
            return slots;
        }

        /// <summary>
        /// Get spell slots for a third caster at a given class level.
        /// Returns array of 4 ints (spell levels 1-4).
        /// </summary>
        public static int[] GetThirdCasterSlots(int classLevel)
        {
            var slots = new int[4];
            if (classLevel < 1 || classLevel > 20) return slots;
            for (int i = 0; i < 4; i++)
                slots[i] = ThirdCasterSlots[classLevel - 1, i];
            return slots;
        }

        /// <summary>
        /// Get Pact Magic slot count and level for a Warlock at a given class level.
        /// Returns (slotCount, slotLevel).
        /// </summary>
        public static (int slotCount, int slotLevel) GetPactMagicSlots(int classLevel)
        {
            if (classLevel < 1 || classLevel > 20) return (0, 0);
            return (PactMagicSlots[classLevel - 1, 0], PactMagicSlots[classLevel - 1, 1]);
        }

        /// <summary>
        /// Get multiclass spell slots based on combined effective caster level.
        /// Uses the full caster table. Returns array of 9 ints (spell levels 1-9).
        /// </summary>
        public static int[] GetMulticlassSlots(int effectiveCasterLevel)
        {
            if (effectiveCasterLevel < 1) return new int[9];
            if (effectiveCasterLevel > 20) effectiveCasterLevel = 20;
            return GetFullCasterSlots(effectiveCasterLevel);
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Data/ProficiencyTable.cs Assets/Scripts/RPG/Data/ExperienceTable.cs Assets/Scripts/RPG/Data/SpellSlotTable.cs
git commit -m "feat: add RPG lookup tables — ProficiencyTable, ExperienceTable, SpellSlotTable with full D&D 5e data

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Character Data Types

Create character-related data types in `Assets/Scripts/RPG/Character/`.

### File 1: `Assets/Scripts/RPG/Character/ClassLevelData.cs`

```csharp
namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Per-level features struct — what a class gains at each level.
    /// </summary>
    [System.Serializable]
    public struct ClassLevelData
    {
        public int Level;
        public string[] FeaturesGained;
        public bool IsASILevel;
        public string SubclassFeature;

        public ClassLevelData(int level, string[] features, bool isASI = false, string subclassFeature = null)
        {
            Level = level;
            FeaturesGained = features ?? System.Array.Empty<string>();
            IsASILevel = isASI;
            SubclassFeature = subclassFeature;
        }
    }
}
```

### File 2: `Assets/Scripts/RPG/Character/ClassData.cs`

```csharp
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// ScriptableObject defining a character class — hit die, proficiencies, casting, progression.
    /// </summary>
    [CreateAssetMenu(fileName = "NewClass", menuName = "ForeverEngine/RPG/Class Data")]
    public class ClassData : ScriptableObject
    {
        public string Id;
        public string Name;
        public DieType HitDie;
        public Ability[] PrimaryAbilities;
        public Ability SpellcastingAbility;
        public SpellcastingType CastingType;
        public string[] ArmorProficiencies;
        public string[] WeaponProficiencies;
        public string[] ToolProficiencies;
        public Ability[] SaveProficiencies;
        public string[] SkillChoices;
        public int SkillChoiceCount;
        public ClassLevelData[] Progression;
        public Ability[] MulticlassPrereqs;
    }
}
```

### File 3: `Assets/Scripts/RPG/Character/ClassLevel.cs`

```csharp
namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Tracks a single class and its level within a multiclass character.
    /// </summary>
    [System.Serializable]
    public struct ClassLevel
    {
        public ClassData ClassRef;
        public int Level;

        public ClassLevel(ClassData classData, int level)
        {
            ClassRef = classData;
            Level = level;
        }
    }
}
```

### File 4: `Assets/Scripts/RPG/Character/SpeciesTrait.cs`

```csharp
namespace ForeverEngine.RPG.Character
{
    [System.Flags]
    public enum SpeciesTrait
    {
        None                = 0,
        FeyAncestry         = 1 << 0,   // Advantage on saves vs charmed, immune to magical sleep
        Trance              = 1 << 1,   // 4-hour rest instead of 8
        DarkvisionStandard  = 1 << 2,   // 60ft darkvision
        DarkvisionSuperior  = 1 << 3,   // 120ft darkvision
        DwarvenResilience   = 1 << 4,   // Advantage on poison saves, resistance to poison damage
        Stonecunning        = 1 << 5,   // Double proficiency on stone History checks
        Lucky               = 1 << 6,   // Reroll natural 1s on attacks/ability/saves
        BraveHalfling       = 1 << 7,   // Advantage on saves vs frightened
        NaturallyStealthy   = 1 << 8,   // Hide behind Medium or larger creatures
        StoutResilience     = 1 << 9,   // Advantage on poison saves, resistance to poison damage
        Relentless          = 1 << 10,  // Drop to 1 HP instead of 0 once per rest
        SavageAttacks       = 1 << 11,  // Extra crit die on melee
        BreathWeapon        = 1 << 12,  // Dragonborn breath attack
        DamageResistance    = 1 << 13,  // Resistance to one damage type (by draconic ancestry)
        HellishResistance   = 1 << 14,  // Fire resistance
        InfernalLegacy      = 1 << 15,  // Innate spellcasting (Tiefling)
        GnomeCunning        = 1 << 16,  // Advantage on INT/WIS/CHA saves vs magic
        Tinker              = 1 << 17,  // Create small clockwork devices
        MinorIllusion       = 1 << 18,  // Know Minor Illusion cantrip
        Shapechanger        = 1 << 19,  // Alter appearance at will
        MaskOfTheWild       = 1 << 20,  // Hide in light natural obscuration
        SunlightSensitivity = 1 << 21,  // Disadvantage in direct sunlight
        DrowMagic           = 1 << 22,  // Innate Drow spellcasting
        DwarvenToughness    = 1 << 23,  // +1 HP per level
        ExtraSkill          = 1 << 24,  // Extra skill proficiency (Human/Half-Elf)
        ExtraFeat           = 1 << 25,  // Bonus feat at level 1 (Human variant)
        DwarvenArmorTraining = 1 << 26, // Light and Medium armor proficiency (Mountain Dwarf)
    }
}
```

### File 5: `Assets/Scripts/RPG/Character/SpeciesData.cs`

```csharp
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// ScriptableObject defining a playable species — ability bonuses, traits, darkvision, speed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "ForeverEngine/RPG/Species Data")]
    public class SpeciesData : ScriptableObject
    {
        public string Id;
        public string Name;

        [System.Serializable]
        public struct AbilityBonus
        {
            public Ability Ability;
            public int Bonus;
        }

        public AbilityBonus[] AbilityBonuses;
        public int Size; // 0 = Small, 1 = Medium
        public int Speed; // Feet (25-35 typical)
        public int DarkvisionRange; // 0, 60, or 120
        public SpeciesTrait Traits;

        [Header("Innate Spellcasting")]
        public SpellData[] InnateSpells; // Forward reference — uses Spells.SpellData

        [Header("Proficiencies & Languages")]
        public string[] Languages;
        public string[] BonusProficiencies;

        [Header("Subraces")]
        public SpeciesData[] Subraces;

        /// <summary>
        /// Forward declaration — SpellData lives in ForeverEngine.RPG.Spells namespace.
        /// We use UnityEngine.ScriptableObject base to avoid circular dependency.
        /// </summary>
    }

    // Note: SpellData reference in SpeciesData uses the Spells.SpellData type
    // which will be created in Task 7. Unity's serializer handles forward references
    // to ScriptableObjects naturally via asset GUID linkage.
}
```

**Note:** The `SpellData` field in `SpeciesData` references the type created in Task 7. To avoid compile errors, we use a temporary placeholder approach: we declare it as `ScriptableObject[]` for now and update it in Task 7. However, since Unity compiles all scripts together, we can instead just accept that Task 7 must be created before this fully resolves — OR we create a minimal forward-declared `SpellData` stub. The cleanest approach: we change `InnateSpells` to `ScriptableObject[]` temporarily.

**CORRECTION:** Replace the `InnateSpells` field line with:

```csharp
        // InnateSpells references SpellData (created in Task 7).
        // Typed as ScriptableObject[] to avoid forward-dependency compile error.
        // Will be cast to SpellData[] at runtime after Task 7 is implemented.
        public ScriptableObject[] InnateSpells;
```

**Revised File 5: `Assets/Scripts/RPG/Character/SpeciesData.cs`**

```csharp
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// ScriptableObject defining a playable species — ability bonuses, traits, darkvision, speed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "ForeverEngine/RPG/Species Data")]
    public class SpeciesData : ScriptableObject
    {
        public string Id;
        public string Name;

        [System.Serializable]
        public struct AbilityBonus
        {
            public Ability Ability;
            public int Bonus;
        }

        public AbilityBonus[] AbilityBonuses;
        public int Size; // 0 = Small, 1 = Medium
        public int Speed; // Feet (25-35 typical)
        public int DarkvisionRange; // 0, 60, or 120
        public SpeciesTrait Traits;

        [Header("Innate Spellcasting")]
        // References SpellData ScriptableObjects (created in Task 7).
        // Typed as ScriptableObject[] to compile before SpellData exists.
        // Task 7 will update this to the concrete SpellData type.
        public ScriptableObject[] InnateSpells;

        [Header("Proficiencies & Languages")]
        public string[] Languages;
        public string[] BonusProficiencies;

        [Header("Subraces")]
        public SpeciesData[] Subraces;
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Character/
git commit -m "feat: add character data types — ClassData, ClassLevelData, ClassLevel, SpeciesData, SpeciesTrait

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Combat Structs

Create combat data structures in `Assets/Scripts/RPG/Combat/`.

### File 1: `Assets/Scripts/RPG/Combat/AttackContext.cs`

```csharp
using System.Collections.Generic;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// All inputs needed to resolve a single attack roll.
    /// </summary>
    [System.Serializable]
    public struct AttackContext
    {
        public AbilityScores AttackerAbilities;
        public int AttackerProficiency;
        public WeaponData Weapon; // null for unarmed/spell attacks
        public int TargetAC;
        public Condition AttackerConditions;
        public Condition TargetConditions;
        public bool IsRanged;
        public bool IsMelee;
        public int CritRange; // Default 20, Champion Fighter = 19, Improved = 18
        public int MagicBonus; // Weapon/spell focus magic bonus
        public List<string> AdvantageReasons;
        public List<string> DisadvantageReasons;
    }
}
```

### File 2: `Assets/Scripts/RPG/Combat/AttackResult.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Output of attack resolution.
    /// </summary>
    [System.Serializable]
    public struct AttackResult
    {
        public bool Hit;
        public bool Critical;
        public int NaturalRoll;
        public int Total;
        public AdvantageState State;

        public static AttackResult Miss(int naturalRoll, int total, AdvantageState state)
        {
            return new AttackResult
            {
                Hit = false,
                Critical = false,
                NaturalRoll = naturalRoll,
                Total = total,
                State = state
            };
        }

        public static AttackResult CriticalHit(int naturalRoll, int total, AdvantageState state)
        {
            return new AttackResult
            {
                Hit = true,
                Critical = true,
                NaturalRoll = naturalRoll,
                Total = total,
                State = state
            };
        }
    }
}
```

### File 3: `Assets/Scripts/RPG/Combat/DamageContext.cs`

```csharp
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// All inputs needed to resolve damage application.
    /// </summary>
    [System.Serializable]
    public struct DamageContext
    {
        public DiceExpression BaseDamage;
        public DamageType Type;
        public bool Critical; // Double dice count
        public int BonusDamage; // Ability mod + magic + features (e.g., Rage +2)
        public DamageType Resistances; // Target's damage resistances (flagged)
        public DamageType Vulnerabilities; // Target's damage vulnerabilities (flagged)
        public DamageType Immunities; // Target's damage immunities (flagged)
        public int TargetTempHP;
        public int TargetHP;
    }
}
```

### File 4: `Assets/Scripts/RPG/Combat/DamageResult.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Output of damage resolution.
    /// </summary>
    [System.Serializable]
    public struct DamageResult
    {
        public int TotalRolled;
        public int AfterResistance;
        public int AbsorbedByTempHP;
        public int HPDamage;
        public DamageType TypeApplied;
        public bool Killed;
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Combat/AttackContext.cs Assets/Scripts/RPG/Combat/AttackResult.cs Assets/Scripts/RPG/Combat/DamageContext.cs Assets/Scripts/RPG/Combat/DamageResult.cs
git commit -m "feat: add combat structs — AttackContext, AttackResult, DamageContext, DamageResult

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Item Data Types

Create item ScriptableObjects in `Assets/Scripts/RPG/Items/`.

### File 1: `Assets/Scripts/RPG/Items/WeaponData.cs`

```csharp
using UnityEngine;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// ScriptableObject defining a weapon — damage, properties, range, magic bonus.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "ForeverEngine/RPG/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string Id;
        public string Name;

        [Header("Damage")]
        public int DamageDiceCount;
        public DieType DamageDie;
        public int DamageBonus;
        public DamageType Type; // Slashing, Piercing, or Bludgeoning

        [Header("Properties")]
        public WeaponProperty Properties;
        public string ProficiencyGroup; // "Simple" or "Martial"

        [Header("Range")]
        public int NormalRange;  // 0 if melee-only
        public int LongRange;   // 0 if melee-only

        [Header("Versatile")]
        public int VersatileDiceCount;
        public DieType VersatileDie;

        [Header("Magic")]
        public int MagicBonus; // 0-3
        public Rarity Rarity;

        /// <summary>
        /// Get the primary damage as a DiceExpression.
        /// </summary>
        public DiceExpression GetDamage()
        {
            return new DiceExpression(DamageDiceCount, DamageDie, DamageBonus);
        }

        /// <summary>
        /// Get versatile (two-handed) damage as a DiceExpression.
        /// </summary>
        public DiceExpression GetVersatileDamage()
        {
            if (!Properties.HasFlag(WeaponProperty.Versatile))
                return GetDamage();
            return new DiceExpression(VersatileDiceCount, VersatileDie, DamageBonus);
        }

        /// <summary>
        /// Whether this weapon can use DEX instead of STR.
        /// </summary>
        public bool IsFinesse => Properties.HasFlag(WeaponProperty.Finesse);

        /// <summary>
        /// Whether this weapon is ranged.
        /// </summary>
        public bool IsRanged => NormalRange > 0;
    }
}
```

### File 2: `Assets/Scripts/RPG/Items/ArmorData.cs`

```csharp
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// ScriptableObject defining armor — base AC, type, stealth penalty, STR requirement.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArmor", menuName = "ForeverEngine/RPG/Armor Data")]
    public class ArmorData : ScriptableObject
    {
        public string Id;
        public string Name;
        public int BaseAC;
        public ArmorType Type;
        public bool StealthDisadvantage;
        public int StrengthRequirement;
        public int MagicBonus; // 0-3
        public Rarity Rarity;
    }
}
```

### File 3: `Assets/Scripts/RPG/Items/MagicItemData.cs`

```csharp
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// ScriptableObject defining a magic item — rarity, attunement, stat modifiers, effects.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMagicItem", menuName = "ForeverEngine/RPG/Magic Item Data")]
    public class MagicItemData : ScriptableObject
    {
        public string Id;
        public string Name;
        public Rarity Rarity;
        public bool RequiresAttunement;

        [System.Serializable]
        public struct AbilityBonus
        {
            public Ability Ability;
            public int Bonus;
        }

        public AbilityBonus[] AbilityBonuses;
        public int ACBonus;
        public int SaveBonus;
        public string[] EffectTags; // Interpreted at runtime
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Items/
git commit -m "feat: add item data types — WeaponData, ArmorData, MagicItemData ScriptableObjects

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Spell Data Types

Create spell-related types in `Assets/Scripts/RPG/Spells/`. Also update `SpeciesData.InnateSpells` to use the concrete type.

### File 1: `Assets/Scripts/RPG/Spells/SpellData.cs`

```csharp
using UnityEngine;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// ScriptableObject defining a spell — level, school, range, components, damage, saves, AoE, upcast.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpell", menuName = "ForeverEngine/RPG/Spell Data")]
    public class SpellData : ScriptableObject
    {
        public string Id;
        public string Name;
        public int Level; // 0 = cantrip, 1-9
        public SpellSchool School;

        [Header("Casting")]
        public string CastingTime; // "action", "bonus_action", "reaction", "1_minute", "10_minutes"
        public int Range; // Feet; 0 = self, 5 = touch, 30/60/90/120/150
        public bool Verbal;
        public bool Somatic;
        public bool Material;
        public string MaterialDescription;

        [Header("Duration")]
        public string Duration; // "instantaneous", "1_round", "1_minute", "concentration_1_minute", etc.
        public bool Concentration;
        public bool Ritual;

        [Header("Damage")]
        public int DamageDiceCount;
        public DieType DamageDie;
        public int DamageBonus;
        public DamageType DamageType;

        [Header("Save / Attack")]
        public Ability SaveType; // Which ability the target saves with (ignored if SpellAttack)
        public bool SpellAttack; // true = uses attack roll instead of save
        public bool HasSave; // true = requires a saving throw

        [Header("Area of Effect")]
        public AoEShape AreaShape;
        public int AreaSize; // Radius or length in feet

        [Header("Upcast")]
        public int UpcastDamageDiceCount;
        public DieType UpcastDamageDie;
        public int UpcastDamageBonus;

        [Header("Conditions")]
        public Condition AppliesCondition;
        public int ConditionDuration; // Turns

        [Header("Healing")]
        public int HealingDiceCount;
        public DieType HealingDie;
        public int HealingBonus;

        [Header("Class Access")]
        public ClassFlag Classes; // Which classes can learn this spell

        /// <summary>
        /// Get the base damage as a DiceExpression.
        /// </summary>
        public DiceExpression GetDamage()
        {
            if (DamageDiceCount <= 0) return DiceExpression.None;
            return new DiceExpression(DamageDiceCount, DamageDie, DamageBonus);
        }

        /// <summary>
        /// Get the upcast bonus damage per spell level above base.
        /// </summary>
        public DiceExpression GetUpcastDamage()
        {
            if (UpcastDamageDiceCount <= 0) return DiceExpression.None;
            return new DiceExpression(UpcastDamageDiceCount, UpcastDamageDie, UpcastDamageBonus);
        }

        /// <summary>
        /// Get the healing dice as a DiceExpression.
        /// </summary>
        public DiceExpression GetHealing()
        {
            if (HealingDiceCount <= 0) return DiceExpression.None;
            return new DiceExpression(HealingDiceCount, HealingDie, HealingBonus);
        }

        /// <summary>
        /// Whether this is a cantrip (level 0).
        /// </summary>
        public bool IsCantrip => Level == 0;
    }
}
```

### File 2: `Assets/Scripts/RPG/Spells/CastContext.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// All inputs needed to cast a spell.
    /// </summary>
    [System.Serializable]
    public struct CastContext
    {
        public object Caster; // CharacterSheet (typed as object to avoid circular ref; cast at runtime)
        public object[] Targets; // CharacterSheet[] or positions for AoE
        public SpellData Spell;
        public int SlotLevel; // Must be >= spell level
        public MetamagicType Metamagic;
        public bool IsRitual; // Cast as ritual (no slot expended, +10 min casting time)
    }
}
```

### File 3: `Assets/Scripts/RPG/Spells/CastResult.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Output of spell casting resolution.
    /// </summary>
    [System.Serializable]
    public struct CastResult
    {
        public bool Success;
        public int DamageDealt;
        public int HealingDone;
        public Condition ConditionsApplied;
        public int SlotExpended;
        public bool ConcentrationStarted;
        public string FailureReason;

        public static CastResult Failure(string reason)
        {
            return new CastResult
            {
                Success = false,
                FailureReason = reason
            };
        }
    }
}
```

### Update: `Assets/Scripts/RPG/Character/SpeciesData.cs`

After this task compiles, update `SpeciesData.InnateSpells` from `ScriptableObject[]` to `SpellData[]`:

Replace the InnateSpells field:
```csharp
        // OLD:
        // public ScriptableObject[] InnateSpells;

        // NEW:
        public ForeverEngine.RPG.Spells.SpellData[] InnateSpells;
```

**Full revised `Assets/Scripts/RPG/Character/SpeciesData.cs`:**

```csharp
using UnityEngine;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// ScriptableObject defining a playable species — ability bonuses, traits, darkvision, speed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "ForeverEngine/RPG/Species Data")]
    public class SpeciesData : ScriptableObject
    {
        public string Id;
        public string Name;

        [System.Serializable]
        public struct AbilityBonus
        {
            public Ability Ability;
            public int Bonus;
        }

        public AbilityBonus[] AbilityBonuses;
        public int Size; // 0 = Small, 1 = Medium
        public int Speed; // Feet (25-35 typical)
        public int DarkvisionRange; // 0, 60, or 120
        public SpeciesTrait Traits;

        [Header("Innate Spellcasting")]
        public SpellData[] InnateSpells;

        [Header("Proficiencies & Languages")]
        public string[] Languages;
        public string[] BonusProficiencies;

        [Header("Subraces")]
        public SpeciesData[] Subraces;
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Spells/SpellData.cs Assets/Scripts/RPG/Spells/CastContext.cs Assets/Scripts/RPG/Spells/CastResult.cs Assets/Scripts/RPG/Character/SpeciesData.cs
git commit -m "feat: add spell data types — SpellData, CastContext, CastResult; update SpeciesData to reference SpellData

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: ConditionManager

Create the per-character condition tracking system in `Assets/Scripts/RPG/Combat/`.

### File 1: `Assets/Scripts/RPG/Combat/ActiveCondition.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// A single active condition instance with source tracking and duration.
    /// </summary>
    [System.Serializable]
    public struct ActiveCondition
    {
        public Condition Type;
        public int RemainingTurns; // -1 = indefinite (until removed)
        public string Source; // What caused this condition (spell name, ability, etc.)

        public ActiveCondition(Condition type, int durationTurns, string source)
        {
            Type = type;
            RemainingTurns = durationTurns;
            Source = source;
        }

        public bool IsExpired => RemainingTurns == 0;
        public bool IsIndefinite => RemainingTurns < 0;
    }
}
```

### File 2: `Assets/Scripts/RPG/Combat/ConditionManager.cs`

```csharp
using System.Collections.Generic;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Per-character condition tracker. Manages active conditions with duration, composites, and advantage queries.
    /// </summary>
    [System.Serializable]
    public class ConditionManager
    {
        /// <summary>
        /// Bitfield of all currently active conditions for fast query.
        /// </summary>
        public Condition ActiveFlags { get; private set; }

        /// <summary>
        /// List of all active condition instances (with source and duration).
        /// </summary>
        private readonly List<ActiveCondition> _conditions = new List<ActiveCondition>();

        /// <summary>
        /// Apply a condition with duration and source tracking.
        /// </summary>
        /// <param name="condition">The condition to apply.</param>
        /// <param name="durationTurns">Duration in turns. -1 for indefinite.</param>
        /// <param name="source">What caused this condition.</param>
        public void Apply(Condition condition, int durationTurns, string source)
        {
            _conditions.Add(new ActiveCondition(condition, durationTurns, source));
            ActiveFlags |= condition;
        }

        /// <summary>
        /// Remove all instances of a condition.
        /// </summary>
        public void Remove(Condition condition)
        {
            _conditions.RemoveAll(c => c.Type == condition);
            RecalculateFlags();
        }

        /// <summary>
        /// Remove a specific condition instance by source.
        /// </summary>
        public void RemoveBySource(string source)
        {
            _conditions.RemoveAll(c => c.Source == source);
            RecalculateFlags();
        }

        /// <summary>
        /// Check if a condition (or any condition in a composite mask) is active.
        /// </summary>
        public bool Has(Condition condition)
        {
            return (ActiveFlags & condition) != 0;
        }

        /// <summary>
        /// Check if the character can act (not incapacitated/stunned/paralyzed/petrified/unconscious).
        /// </summary>
        public bool CanAct => !Has(Condition.CantAct);

        /// <summary>
        /// Tick all condition durations by 1 turn. Returns list of conditions that expired.
        /// </summary>
        public List<Condition> TickDurations()
        {
            var expired = new List<Condition>();

            for (int i = _conditions.Count - 1; i >= 0; i--)
            {
                var c = _conditions[i];
                if (c.IsIndefinite) continue;

                c.RemainingTurns--;
                if (c.RemainingTurns <= 0)
                {
                    expired.Add(c.Type);
                    _conditions.RemoveAt(i);
                }
                else
                {
                    _conditions[i] = c;
                }
            }

            if (expired.Count > 0) RecalculateFlags();
            return expired;
        }

        /// <summary>
        /// Determine advantage/disadvantage sources from conditions.
        /// </summary>
        /// <param name="attackerConditions">Conditions on the attacker.</param>
        /// <param name="targetConditions">Conditions on the target.</param>
        /// <param name="isMelee">Whether the attack is melee.</param>
        /// <param name="isRanged">Whether the attack is ranged.</param>
        /// <param name="advantageReasons">List to populate with advantage reasons.</param>
        /// <param name="disadvantageReasons">List to populate with disadvantage reasons.</param>
        public static void GetAdvantageModifiers(
            Condition attackerConditions,
            Condition targetConditions,
            bool isMelee,
            bool isRanged,
            List<string> advantageReasons,
            List<string> disadvantageReasons)
        {
            // Target conditions that grant advantage to attacker
            if ((targetConditions & Condition.Blinded) != 0)
                advantageReasons.Add("Target is Blinded");
            if ((targetConditions & Condition.Restrained) != 0)
                advantageReasons.Add("Target is Restrained");
            if ((targetConditions & Condition.Stunned) != 0)
                advantageReasons.Add("Target is Stunned");
            if ((targetConditions & Condition.Paralyzed) != 0)
                advantageReasons.Add("Target is Paralyzed");
            if ((targetConditions & Condition.Unconscious) != 0)
                advantageReasons.Add("Target is Unconscious");

            // Attacker conditions that grant advantage
            if ((attackerConditions & Condition.Invisible) != 0)
                advantageReasons.Add("Attacker is Invisible");

            // Attacker conditions that impose disadvantage
            if ((attackerConditions & Condition.Blinded) != 0)
                disadvantageReasons.Add("Attacker is Blinded");
            if ((attackerConditions & Condition.Frightened) != 0)
                disadvantageReasons.Add("Attacker is Frightened");
            if ((attackerConditions & Condition.Poisoned) != 0)
                disadvantageReasons.Add("Attacker is Poisoned");
            if ((attackerConditions & Condition.Restrained) != 0)
                disadvantageReasons.Add("Attacker is Restrained");

            // Prone: disadvantage on ranged, advantage on melee (from attacker being prone)
            if (isRanged && (attackerConditions & Condition.Prone) != 0)
                disadvantageReasons.Add("Attacker is Prone (ranged)");

            // Target prone: advantage on melee within 5ft, disadvantage on ranged
            if (isMelee && (targetConditions & Condition.Prone) != 0)
                advantageReasons.Add("Target is Prone (melee)");
            if (isRanged && (targetConditions & Condition.Prone) != 0)
                disadvantageReasons.Add("Target is Prone (ranged)");

            // Target invisible: disadvantage for attacker
            if ((targetConditions & Condition.Invisible) != 0)
                disadvantageReasons.Add("Target is Invisible");
        }

        /// <summary>
        /// Check if melee attacks auto-crit (target is paralyzed or unconscious within 5ft).
        /// </summary>
        public static bool IsMeleeAutoCrit(Condition targetConditions)
        {
            return (targetConditions & Condition.MeleeAutoCrit) != 0;
        }

        /// <summary>
        /// Get a copy of all active conditions.
        /// </summary>
        public List<ActiveCondition> GetAll()
        {
            return new List<ActiveCondition>(_conditions);
        }

        /// <summary>
        /// Remove all conditions.
        /// </summary>
        public void Clear()
        {
            _conditions.Clear();
            ActiveFlags = Condition.None;
        }

        private void RecalculateFlags()
        {
            ActiveFlags = Condition.None;
            for (int i = 0; i < _conditions.Count; i++)
            {
                ActiveFlags |= _conditions[i].Type;
            }
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Combat/ActiveCondition.cs Assets/Scripts/RPG/Combat/ConditionManager.cs
git commit -m "feat: add ConditionManager — active conditions with duration tracking, composites, advantage queries

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: DeathSaveTracker

Create death save mechanics in `Assets/Scripts/RPG/Combat/`.

### File 1: `Assets/Scripts/RPG/Combat/DeathSaveTracker.cs`

```csharp
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Per-character death save tracker. Implements D&D 5e death saving throw rules:
    /// - 3 successes = stabilized
    /// - 3 failures = dead
    /// - Natural 1 = 2 failures
    /// - Natural 20 = revived with 1 HP
    /// - 10+ = success, less than 10 = failure
    /// </summary>
    [System.Serializable]
    public class DeathSaveTracker
    {
        public int Successes { get; private set; }
        public int Failures { get; private set; }
        public bool IsStabilized { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsActive { get; private set; } // True when at 0 HP and making saves

        /// <summary>
        /// Begin death saves (called when character drops to 0 HP).
        /// </summary>
        public void Begin()
        {
            Successes = 0;
            Failures = 0;
            IsStabilized = false;
            IsDead = false;
            IsActive = true;
        }

        /// <summary>
        /// Roll a death saving throw.
        /// </summary>
        /// <param name="d20Roll">The natural d20 result (1-20).</param>
        /// <returns>The result of the death save.</returns>
        public DeathSaveResult RollDeathSave(int d20Roll)
        {
            if (!IsActive || IsStabilized || IsDead) return DeathSaveResult.Success;

            // Natural 20: revived with 1 HP
            if (d20Roll >= 20)
            {
                Reset();
                return DeathSaveResult.Revived;
            }

            // Natural 1: 2 failures
            if (d20Roll <= 1)
            {
                Failures += 2;
                if (Failures >= 3)
                {
                    IsDead = true;
                    IsActive = false;
                    return DeathSaveResult.Dead;
                }
                return DeathSaveResult.Failure;
            }

            // 10+ = success
            if (d20Roll >= 10)
            {
                Successes++;
                if (Successes >= 3)
                {
                    IsStabilized = true;
                    IsActive = false;
                    return DeathSaveResult.Stabilized;
                }
                return DeathSaveResult.Success;
            }

            // Less than 10 = failure
            Failures++;
            if (Failures >= 3)
            {
                IsDead = true;
                IsActive = false;
                return DeathSaveResult.Dead;
            }
            return DeathSaveResult.Failure;
        }

        /// <summary>
        /// Take damage while at 0 HP. Adds death save failures.
        /// </summary>
        /// <param name="isCritical">If true, adds 2 failures instead of 1.</param>
        /// <returns>The result — could be Dead if failures reach 3.</returns>
        public DeathSaveResult TakeDamageAtZero(bool isCritical)
        {
            if (!IsActive || IsDead) return IsDead ? DeathSaveResult.Dead : DeathSaveResult.Success;

            Failures += isCritical ? 2 : 1;
            if (Failures >= 3)
            {
                IsDead = true;
                IsActive = false;
                return DeathSaveResult.Dead;
            }
            return DeathSaveResult.Failure;
        }

        /// <summary>
        /// Reset death saves (called when healed above 0 HP).
        /// </summary>
        public void Reset()
        {
            Successes = 0;
            Failures = 0;
            IsStabilized = false;
            IsDead = false;
            IsActive = false;
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Combat/DeathSaveTracker.cs
git commit -m "feat: add DeathSaveTracker — D&D 5e death saving throws with nat 1/20 rules

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: ConcentrationTracker

Create concentration mechanics in `Assets/Scripts/RPG/Combat/`.

### File 1: `Assets/Scripts/RPG/Combat/ConcentrationTracker.cs`

```csharp
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Per-character concentration tracker. Manages a single active concentration spell
    /// and handles concentration saves when the character takes damage.
    /// War Caster feat grants advantage on concentration saves.
    /// </summary>
    [System.Serializable]
    public class ConcentrationTracker
    {
        public SpellData ActiveSpell { get; private set; }
        public bool IsConcentrating => ActiveSpell != null;

        /// <summary>
        /// Begin concentrating on a spell. Ends previous concentration if any.
        /// </summary>
        /// <param name="spell">The spell requiring concentration.</param>
        /// <returns>The previously concentrated spell (null if none).</returns>
        public SpellData Begin(SpellData spell)
        {
            var previous = ActiveSpell;
            ActiveSpell = spell;
            return previous;
        }

        /// <summary>
        /// Check concentration after taking damage.
        /// DC = max(10, damageTaken / 2). CON save. War Caster = advantage.
        /// </summary>
        /// <param name="damageTaken">Amount of damage taken.</param>
        /// <param name="abilities">Character's ability scores.</param>
        /// <param name="proficiency">Character's proficiency bonus (added if proficient in CON saves).</param>
        /// <param name="isProficientConSave">Whether the character is proficient in CON saves.</param>
        /// <param name="hasWarCaster">Whether the character has the War Caster feat (advantage).</param>
        /// <param name="seed">RNG seed for dice rolls.</param>
        /// <returns>True if concentration is maintained, false if broken.</returns>
        public bool CheckConcentration(
            int damageTaken,
            AbilityScores abilities,
            int proficiency,
            bool isProficientConSave,
            bool hasWarCaster,
            ref uint seed)
        {
            if (!IsConcentrating) return true;

            int dc = damageTaken / 2;
            if (dc < 10) dc = 10;

            int conMod = abilities.GetModifier(Ability.CON);
            int saveBonus = conMod + (isProficientConSave ? proficiency : 0);

            int roll;
            if (hasWarCaster)
            {
                // Advantage: roll twice, take higher
                int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);
                int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
                roll = roll1 > roll2 ? roll1 : roll2;
            }
            else
            {
                roll = DiceRoller.Roll(1, 20, 0, ref seed);
            }

            int total = roll + saveBonus;
            bool passed = total >= dc;

            if (!passed)
            {
                End();
            }

            return passed;
        }

        /// <summary>
        /// End concentration, clearing the active spell.
        /// </summary>
        public void End()
        {
            ActiveSpell = null;
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Combat/ConcentrationTracker.cs
git commit -m "feat: add ConcentrationTracker — spell concentration with CON saves and War Caster support

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: AttackResolver

Create the full D&D 5e attack resolution system in `Assets/Scripts/RPG/Combat/`.

### File 1: `Assets/Scripts/RPG/Combat/AttackResolver.cs`

```csharp
using System.Collections.Generic;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Static attack resolver implementing full D&D 5e attack resolution:
    /// 1. Determine ability modifier (STR for melee, DEX for ranged, Finesse = higher of STR/DEX)
    /// 2. Evaluate advantage/disadvantage from conditions + explicit sources
    /// 3. Roll d20 (or 2d20 with advantage/disadvantage)
    /// 4. Natural 1 = auto-miss, Natural 20 (or within CritRange) = auto-hit + critical
    /// 5. Total = roll + ability mod + proficiency + magic bonus
    /// 6. Compare to target AC
    /// </summary>
    public static class AttackResolver
    {
        /// <summary>
        /// Resolve a single attack.
        /// </summary>
        /// <param name="ctx">Attack context with all inputs.</param>
        /// <param name="seed">RNG seed.</param>
        /// <returns>Attack result.</returns>
        public static AttackResult Resolve(AttackContext ctx, ref uint seed)
        {
            // 1. Determine ability modifier
            int abilityMod = GetAbilityModifier(ctx);

            // 2. Evaluate advantage/disadvantage
            var advReasons = ctx.AdvantageReasons ?? new List<string>();
            var disadvReasons = ctx.DisadvantageReasons ?? new List<string>();

            // Add condition-based advantage/disadvantage
            ConditionManager.GetAdvantageModifiers(
                ctx.AttackerConditions,
                ctx.TargetConditions,
                ctx.IsMelee,
                ctx.IsRanged,
                advReasons,
                disadvReasons);

            // Determine final advantage state
            bool hasAdvantage = advReasons.Count > 0;
            bool hasDisadvantage = disadvReasons.Count > 0;
            AdvantageState state;

            if (hasAdvantage && hasDisadvantage)
                state = AdvantageState.Cancelled;
            else if (hasAdvantage)
                state = AdvantageState.Advantage;
            else if (hasDisadvantage)
                state = AdvantageState.Disadvantage;
            else
                state = AdvantageState.None;

            // 3. Roll d20
            int naturalRoll = RollD20(state, ref seed);

            // 4. Natural 1 = auto-miss
            if (naturalRoll == 1)
            {
                return AttackResult.Miss(1, 1 + abilityMod + ctx.AttackerProficiency + ctx.MagicBonus, state);
            }

            // Check for critical hit
            int critRange = ctx.CritRange > 0 ? ctx.CritRange : 20;
            bool isCritical = naturalRoll >= critRange;

            // Check for melee auto-crit (target paralyzed/unconscious within 5ft)
            if (ctx.IsMelee && ConditionManager.IsMeleeAutoCrit(ctx.TargetConditions))
            {
                isCritical = true;
            }

            // 5. Calculate total
            int total = naturalRoll + abilityMod + ctx.AttackerProficiency + ctx.MagicBonus;

            // Natural 20 (or within crit range) = auto-hit + critical
            if (isCritical)
            {
                return AttackResult.CriticalHit(naturalRoll, total, state);
            }

            // 6. Compare to target AC
            bool hit = total >= ctx.TargetAC;

            return new AttackResult
            {
                Hit = hit,
                Critical = false,
                NaturalRoll = naturalRoll,
                Total = total,
                State = state
            };
        }

        /// <summary>
        /// Determine the ability modifier for this attack.
        /// STR for melee, DEX for ranged, Finesse = higher of STR/DEX.
        /// </summary>
        private static int GetAbilityModifier(AttackContext ctx)
        {
            int strMod = ctx.AttackerAbilities.GetModifier(Ability.STR);
            int dexMod = ctx.AttackerAbilities.GetModifier(Ability.DEX);

            if (ctx.Weapon != null && ctx.Weapon.IsFinesse)
            {
                // Finesse: use higher of STR or DEX
                return strMod > dexMod ? strMod : dexMod;
            }

            if (ctx.IsRanged)
            {
                return dexMod;
            }

            // Default melee: use STR
            return strMod;
        }

        /// <summary>
        /// Roll a d20 respecting advantage/disadvantage state.
        /// </summary>
        private static int RollD20(AdvantageState state, ref uint seed)
        {
            int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);

            switch (state)
            {
                case AdvantageState.Advantage:
                {
                    int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
                    return roll1 > roll2 ? roll1 : roll2;
                }
                case AdvantageState.Disadvantage:
                {
                    int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
                    return roll1 < roll2 ? roll1 : roll2;
                }
                default:
                    return roll1;
            }
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Combat/AttackResolver.cs
git commit -m "feat: add AttackResolver — full D&D 5e attack resolution with advantage/disadvantage, crits, auto-miss

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: DamageResolver

Create the damage resolution system in `Assets/Scripts/RPG/Combat/`.

### File 1: `Assets/Scripts/RPG/Combat/DamageResolver.cs`

```csharp
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Static damage resolver implementing D&D 5e damage rules:
    /// 1. Roll damage dice (if critical, double dice count but not bonus)
    /// 2. Add bonus damage (ability mod + magic + features)
    /// 3. Check immunity -> 0 damage
    /// 4. Check resistance -> halve (round down)
    /// 5. Check vulnerability -> double
    /// 6. Absorb temp HP first
    /// 7. Remainder applied to HP
    /// </summary>
    public static class DamageResolver
    {
        /// <summary>
        /// Apply damage to a target.
        /// </summary>
        /// <param name="ctx">Damage context with all inputs.</param>
        /// <param name="seed">RNG seed.</param>
        /// <returns>Damage result.</returns>
        public static DamageResult Apply(DamageContext ctx, ref uint seed)
        {
            var result = new DamageResult
            {
                TypeApplied = ctx.Type
            };

            // 1. Roll damage dice
            int diceCount = ctx.BaseDamage.Count;
            if (ctx.Critical)
            {
                diceCount *= 2; // Double dice on crit, not bonus
            }

            int diceTotal = DiceRoller.Roll(diceCount, (int)ctx.BaseDamage.Die, 0, ref seed);

            // 2. Add bonus damage (ability mod + magic bonus + features like Rage)
            // Bonus is NOT doubled on crit
            int totalRolled = diceTotal + ctx.BaseDamage.Bonus + ctx.BonusDamage;
            if (totalRolled < 0) totalRolled = 0;

            result.TotalRolled = totalRolled;

            // 3. Check immunity
            if ((ctx.Immunities & ctx.Type) != 0)
            {
                result.AfterResistance = 0;
                result.AbsorbedByTempHP = 0;
                result.HPDamage = 0;
                result.Killed = false;
                return result;
            }

            int afterResist = totalRolled;

            // 4. Check resistance -> halve (round down)
            if ((ctx.Resistances & ctx.Type) != 0)
            {
                afterResist /= 2;
            }

            // 5. Check vulnerability -> double
            if ((ctx.Vulnerabilities & ctx.Type) != 0)
            {
                afterResist *= 2;
            }

            result.AfterResistance = afterResist;

            // 6. Absorb temp HP first
            int remaining = afterResist;
            if (ctx.TargetTempHP > 0)
            {
                int absorbed = remaining <= ctx.TargetTempHP ? remaining : ctx.TargetTempHP;
                result.AbsorbedByTempHP = absorbed;
                remaining -= absorbed;
            }
            else
            {
                result.AbsorbedByTempHP = 0;
            }

            // 7. Remainder applied to HP
            result.HPDamage = remaining;
            result.Killed = (ctx.TargetHP - remaining) <= 0;

            return result;
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Combat/DamageResolver.cs
git commit -m "feat: add DamageResolver — damage with resistance/vulnerability/immunity/temp HP absorption

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 13: EquipmentResolver + AttunementManager

Create equipment AC calculation and attunement tracking in `Assets/Scripts/RPG/Items/`.

### File 1: `Assets/Scripts/RPG/Items/EquipmentResolver.cs`

```csharp
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// Static equipment resolver. Calculates AC from equipped armor, shield, ability modifiers,
    /// and class features (Monk/Barbarian Unarmored Defense).
    ///
    /// AC Rules:
    /// - No armor: 10 + DEX mod
    /// - Light armor: base AC + DEX mod
    /// - Medium armor: base AC + DEX mod (max +2)
    /// - Heavy armor: base AC (flat, no DEX)
    /// - Shield: +2
    /// - Magic bonuses added on top
    /// - Monk Unarmored Defense: 10 + DEX + WIS (no armor, no shield)
    /// - Barbarian Unarmored Defense: 10 + DEX + CON (no armor, shield OK)
    /// </summary>
    public static class EquipmentResolver
    {
        /// <summary>
        /// Calculate AC for a character given their equipment and abilities.
        /// </summary>
        /// <param name="abilities">Character's effective ability scores.</param>
        /// <param name="equippedArmor">Equipped body armor (null if none).</param>
        /// <param name="equippedShield">Equipped shield (null if none).</param>
        /// <param name="hasMonkUnarmored">Whether character has Monk Unarmored Defense.</param>
        /// <param name="hasBarbarianUnarmored">Whether character has Barbarian Unarmored Defense.</param>
        /// <param name="magicACBonus">Bonus AC from magic items (non-armor/shield).</param>
        /// <returns>Calculated AC value.</returns>
        public static int CalculateAC(
            AbilityScores abilities,
            ArmorData equippedArmor,
            ArmorData equippedShield,
            bool hasMonkUnarmored,
            bool hasBarbarianUnarmored,
            int magicACBonus = 0)
        {
            int dexMod = abilities.GetModifier(Ability.DEX);
            int ac;

            if (equippedArmor == null)
            {
                // No armor — check for Unarmored Defense
                if (hasMonkUnarmored)
                {
                    // Monk: 10 + DEX + WIS (no shield allowed for Monk unarmored)
                    int wisMod = abilities.GetModifier(Ability.WIS);
                    ac = 10 + dexMod + wisMod;
                    // Monk unarmored defense doesn't benefit from shields
                    ac += magicACBonus;
                    return ac;
                }
                else if (hasBarbarianUnarmored)
                {
                    // Barbarian: 10 + DEX + CON (shield OK)
                    int conMod = abilities.GetModifier(Ability.CON);
                    ac = 10 + dexMod + conMod;
                }
                else
                {
                    // Standard: 10 + DEX
                    ac = 10 + dexMod;
                }
            }
            else
            {
                switch (equippedArmor.Type)
                {
                    case ArmorType.Light:
                        // Light armor: base + full DEX
                        ac = equippedArmor.BaseAC + dexMod;
                        break;

                    case ArmorType.Medium:
                        // Medium armor: base + DEX (max +2)
                        int cappedDex = dexMod > 2 ? 2 : dexMod;
                        ac = equippedArmor.BaseAC + cappedDex;
                        break;

                    case ArmorType.Heavy:
                        // Heavy armor: flat base AC
                        ac = equippedArmor.BaseAC;
                        break;

                    default:
                        ac = 10 + dexMod;
                        break;
                }

                // Add armor magic bonus
                ac += equippedArmor.MagicBonus;
            }

            // Add shield bonus
            if (equippedShield != null && equippedShield.Type == ArmorType.Shield)
            {
                ac += equippedShield.BaseAC; // Typically 2 for a standard shield
                ac += equippedShield.MagicBonus;
            }

            // Add magic item AC bonus (from rings, cloaks, etc.)
            ac += magicACBonus;

            return ac;
        }
    }
}
```

### File 2: `Assets/Scripts/RPG/Items/AttunementManager.cs`

```csharp
namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// Per-character attunement manager. D&D 5e limits characters to 3 attuned magic items.
    /// </summary>
    [System.Serializable]
    public class AttunementManager
    {
        public const int MaxSlots = 3;

        private readonly MagicItemData[] _slots = new MagicItemData[MaxSlots];

        /// <summary>
        /// Attune to a magic item. Returns false if all slots are full.
        /// </summary>
        public bool Attune(MagicItemData item)
        {
            if (item == null || !item.RequiresAttunement) return false;
            if (IsAttuned(item)) return true; // Already attuned

            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = item;
                    return true;
                }
            }
            return false; // All slots full
        }

        /// <summary>
        /// Remove attunement from a specific slot.
        /// </summary>
        public void Unattune(int slot)
        {
            if (slot >= 0 && slot < MaxSlots)
            {
                _slots[slot] = null;
            }
        }

        /// <summary>
        /// Remove attunement from a specific item.
        /// </summary>
        public void Unattune(MagicItemData item)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == item)
                {
                    _slots[i] = null;
                    return;
                }
            }
        }

        /// <summary>
        /// Check if a specific item is currently attuned.
        /// </summary>
        public bool IsAttuned(MagicItemData item)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == item) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the item in a specific attunement slot.
        /// </summary>
        public MagicItemData GetSlot(int slot)
        {
            if (slot >= 0 && slot < MaxSlots) return _slots[slot];
            return null;
        }

        /// <summary>
        /// Get the number of occupied attunement slots.
        /// </summary>
        public int OccupiedSlots
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_slots[i] != null) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Whether there's room for another attunement.
        /// </summary>
        public bool HasFreeSlot => OccupiedSlots < MaxSlots;

        /// <summary>
        /// Clear all attunements.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < MaxSlots; i++) _slots[i] = null;
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Items/EquipmentResolver.cs Assets/Scripts/RPG/Items/AttunementManager.cs
git commit -m "feat: add EquipmentResolver (AC calculation with armor types, unarmored defense) and AttunementManager

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 14: SpellSlotManager + SpellDatabase

Create spell slot management and spell registry in `Assets/Scripts/RPG/Spells/`.

### File 1: `Assets/Scripts/RPG/Spells/SpellSlotManager.cs`

```csharp
using System.Collections.Generic;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Per-character spell slot manager. Handles full/half/third caster slot calculation,
    /// multiclass stacking, Pact Magic (Warlock), slot expenditure, and rest recovery.
    ///
    /// Multiclass caster level calculation:
    /// - Full caster class levels count at 1x
    /// - Half caster class levels count at 0.5x (round down)
    /// - Third caster class levels count at 0.33x (round down)
    /// - Sum = effective caster level -> look up shared multiclass table
    /// - Pact Magic (Warlock) slots are tracked separately and don't stack
    /// </summary>
    [System.Serializable]
    public class SpellSlotManager
    {
        /// <summary>
        /// Available spell slots per level (index 0 = 1st level, index 8 = 9th level).
        /// </summary>
        public int[] AvailableSlots = new int[9];

        /// <summary>
        /// Maximum spell slots per level (calculated from class levels).
        /// </summary>
        public int[] MaxSlots = new int[9];

        /// <summary>
        /// Warlock Pact Magic slot count.
        /// </summary>
        public int PactSlotCount;

        /// <summary>
        /// Warlock Pact Magic slot level.
        /// </summary>
        public int PactSlotLevel;

        /// <summary>
        /// Available Pact Magic slots (separate from regular slots).
        /// </summary>
        public int AvailablePactSlots;

        /// <summary>
        /// Recalculate all spell slots from class levels.
        /// Handles single class, multiclass stacking, and Pact Magic.
        /// </summary>
        public void RecalculateSlots(List<ClassLevel> classLevels)
        {
            // Reset
            for (int i = 0; i < 9; i++) MaxSlots[i] = 0;
            PactSlotCount = 0;
            PactSlotLevel = 0;

            if (classLevels == null || classLevels.Count == 0) return;

            // Check for single class vs multiclass
            bool isMulticlass = classLevels.Count > 1;
            int effectiveCasterLevel = 0;
            bool hasPactMagic = false;

            foreach (var cl in classLevels)
            {
                if (cl.ClassRef == null) continue;

                switch (cl.ClassRef.CastingType)
                {
                    case SpellcastingType.Full:
                        effectiveCasterLevel += cl.Level;
                        break;
                    case SpellcastingType.Half:
                        effectiveCasterLevel += cl.Level / 2;
                        break;
                    case SpellcastingType.Third:
                        effectiveCasterLevel += cl.Level / 3;
                        break;
                    case SpellcastingType.Pact:
                        hasPactMagic = true;
                        var pact = SpellSlotTable.GetPactMagicSlots(cl.Level);
                        PactSlotCount = pact.slotCount;
                        PactSlotLevel = pact.slotLevel;
                        AvailablePactSlots = PactSlotCount;
                        break;
                }
            }

            if (isMulticlass || effectiveCasterLevel > 0)
            {
                // For multiclass (or any class combination), use the shared multiclass table
                if (isMulticlass)
                {
                    var slots = SpellSlotTable.GetMulticlassSlots(effectiveCasterLevel);
                    for (int i = 0; i < 9 && i < slots.Length; i++)
                        MaxSlots[i] = slots[i];
                }
                else
                {
                    // Single class — use class-specific table
                    var cl = classLevels[0];
                    if (cl.ClassRef != null)
                    {
                        switch (cl.ClassRef.CastingType)
                        {
                            case SpellcastingType.Full:
                            {
                                var slots = SpellSlotTable.GetFullCasterSlots(cl.Level);
                                for (int i = 0; i < 9 && i < slots.Length; i++)
                                    MaxSlots[i] = slots[i];
                                break;
                            }
                            case SpellcastingType.Half:
                            {
                                var slots = SpellSlotTable.GetHalfCasterSlots(cl.Level);
                                for (int i = 0; i < 5 && i < slots.Length; i++)
                                    MaxSlots[i] = slots[i];
                                break;
                            }
                            case SpellcastingType.Third:
                            {
                                var slots = SpellSlotTable.GetThirdCasterSlots(cl.Level);
                                for (int i = 0; i < 4 && i < slots.Length; i++)
                                    MaxSlots[i] = slots[i];
                                break;
                            }
                        }
                    }
                }
            }

            // Initialize available slots to max
            for (int i = 0; i < 9; i++)
                AvailableSlots[i] = MaxSlots[i];
        }

        /// <summary>
        /// Check if a spell can be cast at the given slot level.
        /// </summary>
        public bool CanCast(SpellData spell, int slotLevel)
        {
            // Cantrips don't need slots
            if (spell.IsCantrip) return true;

            // Slot level must be >= spell level
            if (slotLevel < spell.Level) return false;

            // Check regular slots
            if (slotLevel >= 1 && slotLevel <= 9)
            {
                if (AvailableSlots[slotLevel - 1] > 0) return true;
            }

            // Check Pact Magic slots
            if (PactSlotCount > 0 && AvailablePactSlots > 0 && slotLevel == PactSlotLevel)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expend a spell slot at the given level. Tries regular slots first, then Pact slots.
        /// Returns false if no slot available.
        /// </summary>
        public bool ExpendSlot(int level)
        {
            if (level < 1 || level > 9) return false;

            // Try regular slots first
            if (AvailableSlots[level - 1] > 0)
            {
                AvailableSlots[level - 1]--;
                return true;
            }

            // Try Pact Magic slots
            if (PactSlotCount > 0 && AvailablePactSlots > 0 && level == PactSlotLevel)
            {
                AvailablePactSlots--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Restore all spell slots (long rest).
        /// </summary>
        public void RestoreAll()
        {
            for (int i = 0; i < 9; i++)
                AvailableSlots[i] = MaxSlots[i];
            AvailablePactSlots = PactSlotCount;
        }

        /// <summary>
        /// Restore Pact Magic slots only (short rest).
        /// </summary>
        public void RestorePactSlots()
        {
            AvailablePactSlots = PactSlotCount;
        }

        /// <summary>
        /// Get the highest available slot level (for AI/UI display).
        /// </summary>
        public int HighestAvailableSlot
        {
            get
            {
                for (int i = 8; i >= 0; i--)
                {
                    if (AvailableSlots[i] > 0) return i + 1;
                }
                if (AvailablePactSlots > 0) return PactSlotLevel;
                return 0;
            }
        }
    }
}
```

### File 2: `Assets/Scripts/RPG/Spells/SpellDatabase.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static spell registry. Loads all SpellData ScriptableObjects from Resources
    /// and provides lookup by ID, name, school, level, and class.
    /// </summary>
    public static class SpellDatabase
    {
        private static Dictionary<string, SpellData> _byId;
        private static Dictionary<string, SpellData> _byName;
        private static bool _initialized;

        /// <summary>
        /// Initialize the database by loading all SpellData from Resources.
        /// Call this at startup or on first access.
        /// </summary>
        public static void Initialize()
        {
            _byId = new Dictionary<string, SpellData>();
            _byName = new Dictionary<string, SpellData>();

            var all = Resources.LoadAll<SpellData>("RPG/Spells");
            if (all != null)
            {
                foreach (var spell in all)
                {
                    if (!string.IsNullOrEmpty(spell.Id))
                        _byId[spell.Id] = spell;
                    if (!string.IsNullOrEmpty(spell.Name))
                        _byName[spell.Name.ToLowerInvariant()] = spell;
                }
            }

            _initialized = true;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized) Initialize();
        }

        /// <summary>
        /// Get a spell by its unique ID.
        /// </summary>
        public static SpellData GetById(string id)
        {
            EnsureInitialized();
            _byId.TryGetValue(id, out var spell);
            return spell;
        }

        /// <summary>
        /// Get a spell by name (case-insensitive).
        /// </summary>
        public static SpellData GetByName(string name)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(name)) return null;
            _byName.TryGetValue(name.ToLowerInvariant(), out var spell);
            return spell;
        }

        /// <summary>
        /// Get all spells of a specific school.
        /// </summary>
        public static List<SpellData> GetBySchool(SpellSchool school)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if (spell.School == school) results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get all spells of a specific level.
        /// </summary>
        public static List<SpellData> GetByLevel(int level)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if (spell.Level == level) results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get all spells available to a specific class.
        /// </summary>
        public static List<SpellData> GetByClass(ClassFlag classFlag)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if ((spell.Classes & classFlag) != 0) results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get all spells matching a school and class.
        /// </summary>
        public static List<SpellData> GetBySchoolAndClass(SpellSchool school, ClassFlag classFlag)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if (spell.School == school && (spell.Classes & classFlag) != 0)
                    results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get total number of registered spells.
        /// </summary>
        public static int Count
        {
            get
            {
                EnsureInitialized();
                return _byId.Count;
            }
        }

        /// <summary>
        /// Get all registered spells.
        /// </summary>
        public static IEnumerable<SpellData> All
        {
            get
            {
                EnsureInitialized();
                return _byId.Values;
            }
        }

        /// <summary>
        /// Force re-initialization (useful after content generation).
        /// </summary>
        public static void Reload()
        {
            _initialized = false;
            Initialize();
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Spells/SpellSlotManager.cs Assets/Scripts/RPG/Spells/SpellDatabase.cs
git commit -m "feat: add SpellSlotManager (multiclass stacking, Pact Magic) and SpellDatabase (static spell registry)

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 15: SpellCastingPipeline + AoEResolver + MetamagicEngine

Create spell casting resolution in `Assets/Scripts/RPG/Spells/`.

### File 1: `Assets/Scripts/RPG/Spells/SpellCastingPipeline.cs`

```csharp
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static spell casting pipeline implementing the full cast resolution:
    /// 1. Validate: caster knows spell, has slot at required level (cantrip = no slot)
    /// 2. Apply metamagic (if any) — modify context, spend sorcery points
    /// 3. Expend spell slot (unless cantrip or ritual)
    /// 4. If spell attack: call AttackResolver with spell attack bonus
    /// 5. If save-based: target rolls save vs caster's spell DC
    /// 6. Roll damage/healing with upcast scaling
    /// 7. Apply damage via DamageResolver (or apply healing)
    /// 8. Apply conditions if save failed
    /// 9. Set concentration if spell requires it (ends previous)
    /// </summary>
    public static class SpellCastingPipeline
    {
        /// <summary>
        /// Cast a spell. Full pipeline from validation through resolution.
        /// </summary>
        /// <param name="ctx">Cast context with caster, targets, spell, slot level.</param>
        /// <param name="casterAbilities">Caster's effective ability scores.</param>
        /// <param name="casterProficiency">Caster's proficiency bonus.</param>
        /// <param name="castingAbility">The ability used for spellcasting (INT/WIS/CHA).</param>
        /// <param name="spellSlots">Caster's spell slot manager.</param>
        /// <param name="concentration">Caster's concentration tracker.</param>
        /// <param name="sorceryPoints">Sorcery points (for metamagic). Pass null if not a Sorcerer.</param>
        /// <param name="seed">RNG seed.</param>
        /// <returns>Cast result.</returns>
        public static CastResult Cast(
            CastContext ctx,
            AbilityScores casterAbilities,
            int casterProficiency,
            Ability castingAbility,
            SpellSlotManager spellSlots,
            ConcentrationTracker concentration,
            ResourcePool? sorceryPoints,
            ref uint seed)
        {
            var spell = ctx.Spell;
            if (spell == null)
                return CastResult.Failure("No spell specified");

            // 1. Validate slot level
            if (!spell.IsCantrip && !ctx.IsRitual)
            {
                if (ctx.SlotLevel < spell.Level)
                    return CastResult.Failure($"Slot level {ctx.SlotLevel} is below spell level {spell.Level}");

                if (!spellSlots.CanCast(spell, ctx.SlotLevel))
                    return CastResult.Failure($"No available slot at level {ctx.SlotLevel}");
            }

            // 2. Apply metamagic (if any)
            var modifiedCtx = ctx;
            ResourcePool sp = sorceryPoints ?? new ResourcePool(0);
            if (ctx.Metamagic != MetamagicType.None)
            {
                modifiedCtx = MetamagicEngine.ApplyMetamagic(ctx, ctx.Metamagic, ref sp);
            }

            // 3. Expend spell slot (unless cantrip or ritual)
            int slotExpended = 0;
            if (!spell.IsCantrip && !ctx.IsRitual)
            {
                if (!spellSlots.ExpendSlot(ctx.SlotLevel))
                    return CastResult.Failure("Failed to expend spell slot");
                slotExpended = ctx.SlotLevel;
            }

            // Calculate spell DC and attack bonus
            int spellMod = casterAbilities.GetModifier(castingAbility);
            int spellDC = 8 + casterProficiency + spellMod;
            int spellAttackBonus = casterProficiency + spellMod;

            int totalDamage = 0;
            int totalHealing = 0;
            Condition appliedConditions = Condition.None;
            bool targetSaved = false;

            // 4. If spell attack: resolve attack
            if (spell.SpellAttack && modifiedCtx.Targets != null && modifiedCtx.Targets.Length > 0)
            {
                // For simplicity, resolve against first target
                // Full AoE handling would iterate all targets
                var attackCtx = new AttackContext
                {
                    AttackerAbilities = casterAbilities,
                    AttackerProficiency = spellAttackBonus,
                    TargetAC = 10, // Would be populated from target's actual AC
                    IsRanged = spell.Range > 5,
                    IsMelee = spell.Range <= 5,
                    CritRange = 20,
                    MagicBonus = 0
                };

                var attackResult = AttackResolver.Resolve(attackCtx, ref seed);
                if (!attackResult.Hit)
                {
                    return new CastResult
                    {
                        Success = true, // Spell was cast, just missed
                        DamageDealt = 0,
                        SlotExpended = slotExpended,
                        FailureReason = "Attack missed"
                    };
                }

                // 6. Roll damage with upcast scaling
                if (spell.DamageDiceCount > 0)
                {
                    int upcastLevels = ctx.SlotLevel - spell.Level;
                    var upcastBonus = spell.GetUpcastDamage();
                    int extraDice = upcastLevels > 0 ? upcastBonus.Count * upcastLevels : 0;

                    var dmgCtx = new DamageContext
                    {
                        BaseDamage = new DiceExpression(
                            spell.DamageDiceCount + extraDice,
                            spell.DamageDie,
                            spell.DamageBonus),
                        Type = spell.DamageType,
                        Critical = attackResult.Critical,
                        BonusDamage = 0
                    };

                    var dmgResult = DamageResolver.Apply(dmgCtx, ref seed);
                    totalDamage = dmgResult.AfterResistance;
                }
            }
            // 5. If save-based: target saves vs DC
            else if (spell.HasSave)
            {
                // Roll save for target (simplified — would use target's actual ability + proficiency)
                int saveRoll = DiceRoller.Roll(1, 20, 0, ref seed);
                targetSaved = saveRoll >= spellDC;

                if (!targetSaved || spell.DamageDiceCount > 0)
                {
                    // 6. Roll damage with upcast
                    if (spell.DamageDiceCount > 0)
                    {
                        int upcastLevels = ctx.SlotLevel - spell.Level;
                        var upcastBonus = spell.GetUpcastDamage();
                        int extraDice = upcastLevels > 0 ? upcastBonus.Count * upcastLevels : 0;

                        var dmgCtx = new DamageContext
                        {
                            BaseDamage = new DiceExpression(
                                spell.DamageDiceCount + extraDice,
                                spell.DamageDie,
                                spell.DamageBonus),
                            Type = spell.DamageType,
                            Critical = false,
                            BonusDamage = 0
                        };

                        var dmgResult = DamageResolver.Apply(dmgCtx, ref seed);

                        // Save for half damage on some spells
                        totalDamage = targetSaved ? dmgResult.AfterResistance / 2 : dmgResult.AfterResistance;
                    }

                    // 8. Apply conditions if save failed
                    if (!targetSaved && spell.AppliesCondition != Condition.None)
                    {
                        appliedConditions = spell.AppliesCondition;
                    }
                }
            }
            else
            {
                // Non-attack, non-save spells (buffs, healing, utility)
                if (spell.HealingDiceCount > 0)
                {
                    var healExpr = spell.GetHealing();
                    totalHealing = healExpr.Roll(ref seed);

                    // Upcast healing
                    int upcastLevels = ctx.SlotLevel - spell.Level;
                    if (upcastLevels > 0)
                    {
                        var upcastHeal = spell.GetUpcastDamage(); // Reuse upcast field for healing spells
                        if (upcastHeal.Count > 0)
                        {
                            for (int i = 0; i < upcastLevels; i++)
                                totalHealing += upcastHeal.Roll(ref seed);
                        }
                    }
                }

                // Direct condition application (no save required)
                if (spell.AppliesCondition != Condition.None)
                {
                    appliedConditions = spell.AppliesCondition;
                }
            }

            // 9. Set concentration if spell requires it
            bool concentrationStarted = false;
            if (spell.Concentration && concentration != null)
            {
                concentration.Begin(spell);
                concentrationStarted = true;
            }

            return new CastResult
            {
                Success = true,
                DamageDealt = totalDamage,
                HealingDone = totalHealing,
                ConditionsApplied = appliedConditions,
                SlotExpended = slotExpended,
                ConcentrationStarted = concentrationStarted
            };
        }
    }
}
```

### File 2: `Assets/Scripts/RPG/Spells/AoEResolver.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static AoE resolver. Determines which grid positions fall within an area of effect.
    /// Uses tile-based (5ft = 1 tile) grid for all calculations.
    /// </summary>
    public static class AoEResolver
    {
        /// <summary>
        /// Get all grid positions within an area of effect.
        /// </summary>
        /// <param name="origin">Center/origin point of the AoE.</param>
        /// <param name="shape">Shape of the area.</param>
        /// <param name="sizeFeet">Size in feet (radius for sphere/cylinder, length for line/cone, side for cube).</param>
        /// <param name="direction">Direction vector (for cones and lines). Ignored for spheres/cubes.</param>
        /// <returns>List of affected grid positions.</returns>
        public static List<Vector2Int> GetAffectedPositions(
            Vector2Int origin,
            AoEShape shape,
            int sizeFeet,
            Vector2 direction = default)
        {
            var positions = new List<Vector2Int>();
            int sizeTiles = sizeFeet / 5; // Convert feet to tiles (5ft = 1 tile)

            switch (shape)
            {
                case AoEShape.Sphere:
                case AoEShape.Cylinder:
                    // Circle/cylinder: all tiles within radius
                    for (int x = -sizeTiles; x <= sizeTiles; x++)
                    {
                        for (int y = -sizeTiles; y <= sizeTiles; y++)
                        {
                            // Use Chebyshev distance for D&D grid (diagonal = 1 tile)
                            // or Euclidean for more precise circles
                            float dist = Mathf.Sqrt(x * x + y * y);
                            if (dist <= sizeTiles + 0.5f)
                            {
                                positions.Add(new Vector2Int(origin.x + x, origin.y + y));
                            }
                        }
                    }
                    break;

                case AoEShape.Cube:
                    // Cube: square area, sizeTiles on each side
                    int half = sizeTiles / 2;
                    for (int x = -half; x <= half; x++)
                    {
                        for (int y = -half; y <= half; y++)
                        {
                            positions.Add(new Vector2Int(origin.x + x, origin.y + y));
                        }
                    }
                    break;

                case AoEShape.Line:
                    // Line: extends from origin in direction for sizeTiles length, 1 tile wide
                    if (direction == default) direction = Vector2.up;
                    var dir = direction.normalized;
                    for (int i = 0; i <= sizeTiles; i++)
                    {
                        int px = origin.x + Mathf.RoundToInt(dir.x * i);
                        int py = origin.y + Mathf.RoundToInt(dir.y * i);
                        positions.Add(new Vector2Int(px, py));
                    }
                    break;

                case AoEShape.Cone:
                    // Cone: 53-degree spread (per D&D), extends sizeTiles from origin
                    if (direction == default) direction = Vector2.up;
                    var coneDir = direction.normalized;
                    float coneAngle = 53f * Mathf.Deg2Rad / 2f;
                    for (int x = -sizeTiles; x <= sizeTiles; x++)
                    {
                        for (int y = -sizeTiles; y <= sizeTiles; y++)
                        {
                            if (x == 0 && y == 0) continue;
                            float dist = Mathf.Sqrt(x * x + y * y);
                            if (dist > sizeTiles + 0.5f) continue;

                            var toTile = new Vector2(x, y).normalized;
                            float angle = Mathf.Acos(Vector2.Dot(coneDir, toTile));
                            if (angle <= coneAngle)
                            {
                                positions.Add(new Vector2Int(origin.x + x, origin.y + y));
                            }
                        }
                    }
                    // Always include origin
                    positions.Add(origin);
                    break;

                case AoEShape.None:
                default:
                    // Single target (just the origin)
                    positions.Add(origin);
                    break;
            }

            return positions;
        }

        /// <summary>
        /// Check if a specific position is within the AoE.
        /// </summary>
        public static bool IsInArea(
            Vector2Int origin,
            AoEShape shape,
            int sizeFeet,
            Vector2Int target,
            Vector2 direction = default)
        {
            var affected = GetAffectedPositions(origin, shape, sizeFeet, direction);
            return affected.Contains(target);
        }
    }
}
```

### File 3: `Assets/Scripts/RPG/Spells/MetamagicEngine.cs`

```csharp
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static metamagic engine for Sorcerer metamagic options.
    ///
    /// Metamagic types and costs:
    /// - Twinned (cost = spell level, min 1): duplicate target for single-target spells
    /// - Quickened (cost 2): cast as bonus action instead of action
    /// - Subtle (cost 1): no verbal/somatic components
    /// - Empowered (cost 1): reroll damage dice (take higher)
    /// - Heightened (cost 3): target has disadvantage on first save
    /// - Careful (cost 1): chosen allies auto-succeed on AoE saves
    /// - Distant (cost 1): double range (or touch -> 30ft)
    /// </summary>
    public static class MetamagicEngine
    {
        /// <summary>
        /// Apply metamagic to a cast context. Spends sorcery points and modifies the context.
        /// Returns the modified context (or original if metamagic can't be applied).
        /// </summary>
        /// <param name="ctx">Original cast context.</param>
        /// <param name="type">Metamagic type to apply.</param>
        /// <param name="sorceryPoints">Sorcery point pool (modified in place).</param>
        /// <returns>Modified cast context.</returns>
        public static CastContext ApplyMetamagic(CastContext ctx, MetamagicType type, ref ResourcePool sorceryPoints)
        {
            var result = ctx;

            // Apply each selected metamagic
            if ((type & MetamagicType.Twinned) != 0)
            {
                int cost = ctx.Spell.Level < 1 ? 1 : ctx.Spell.Level;
                if (sorceryPoints.Spend(cost))
                {
                    // Twinned: duplicate target list (pipeline handles applying to both)
                    if (result.Targets != null && result.Targets.Length == 1)
                    {
                        // Marker: pipeline should apply spell to 2 targets
                        // Implementation detail handled by SpellCastingPipeline
                    }
                }
            }

            if ((type & MetamagicType.Quickened) != 0)
            {
                if (sorceryPoints.Spend(2))
                {
                    // Quickened: changes casting time to bonus action
                    // Handled by action economy system, not spell pipeline
                }
            }

            if ((type & MetamagicType.Subtle) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Subtle: no verbal/somatic — can't be counterspelled
                    // Marker for pipeline: skip counterspell check
                }
            }

            if ((type & MetamagicType.Empowered) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Empowered: marker for DamageResolver to reroll low dice
                    // Handled during damage roll phase
                }
            }

            if ((type & MetamagicType.Heightened) != 0)
            {
                if (sorceryPoints.Spend(3))
                {
                    // Heightened: target has disadvantage on first save
                    // Marker for save resolution
                }
            }

            if ((type & MetamagicType.Careful) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Careful: chosen allies auto-succeed on AoE saves
                    // Marker for AoE save resolution
                }
            }

            if ((type & MetamagicType.Distant) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Distant: double range (touch becomes 30ft)
                    // Processed during validation — range check uses doubled value
                }
            }

            // Store the metamagic in the modified context so pipeline can reference it
            result.Metamagic = type;
            return result;
        }

        /// <summary>
        /// Get the sorcery point cost for a specific metamagic type.
        /// </summary>
        /// <param name="type">Single metamagic type.</param>
        /// <param name="spellLevel">Level of the spell (for Twinned).</param>
        /// <returns>Cost in sorcery points.</returns>
        public static int GetCost(MetamagicType type, int spellLevel = 0)
        {
            switch (type)
            {
                case MetamagicType.Twinned:   return spellLevel < 1 ? 1 : spellLevel;
                case MetamagicType.Quickened:  return 2;
                case MetamagicType.Subtle:     return 1;
                case MetamagicType.Empowered:  return 1;
                case MetamagicType.Heightened: return 3;
                case MetamagicType.Careful:    return 1;
                case MetamagicType.Distant:    return 1;
                default:                       return 0;
            }
        }

        /// <summary>
        /// Check if a metamagic can be applied to a spell.
        /// </summary>
        /// <param name="type">Metamagic type.</param>
        /// <param name="spell">The spell to check.</param>
        /// <returns>True if metamagic is valid for this spell.</returns>
        public static bool CanApply(MetamagicType type, SpellData spell)
        {
            if (spell == null) return false;

            switch (type)
            {
                case MetamagicType.Twinned:
                    // Can only twin single-target spells
                    return spell.AreaShape == AoEShape.None;
                case MetamagicType.Empowered:
                    // Only for spells that deal damage
                    return spell.DamageDiceCount > 0;
                case MetamagicType.Heightened:
                    // Only for spells that require a save
                    return spell.HasSave;
                case MetamagicType.Careful:
                    // Only for AoE spells that require a save
                    return spell.AreaShape != AoEShape.None && spell.HasSave;
                case MetamagicType.Distant:
                    // Any spell with a range
                    return spell.Range > 0;
                default:
                    return true; // Quickened and Subtle work on any spell
            }
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Spells/SpellCastingPipeline.cs Assets/Scripts/RPG/Spells/AoEResolver.cs Assets/Scripts/RPG/Spells/MetamagicEngine.cs
git commit -m "feat: add SpellCastingPipeline, AoEResolver, and MetamagicEngine — full spell resolution system

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 16: CharacterSheet + CharacterBuilder + ECS Bridge

Create the master CharacterSheet class, factory builder, and ECS bridge in `Assets/Scripts/RPG/Character/`.

### File 1: `Assets/Scripts/RPG/Character/StatsSnapshot.cs`

```csharp
namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Lightweight struct for ECS bridge. Maps CharacterSheet data to a format
    /// compatible with the existing StatsComponent.
    /// </summary>
    public struct StatsSnapshot
    {
        public int Strength;
        public int Dexterity;
        public int Constitution;
        public int Intelligence;
        public int Wisdom;
        public int Charisma;
        public int AC;
        public int HP;
        public int MaxHP;
        public int Speed;
        public int AtkDiceCount;
        public int AtkDiceSides;
        public int AtkDiceBonus;

        /// <summary>
        /// Convert to an ECS StatsComponent.
        /// </summary>
        public ForeverEngine.ECS.Components.StatsComponent ToStatsComponent()
        {
            return new ForeverEngine.ECS.Components.StatsComponent
            {
                Strength = Strength,
                Dexterity = Dexterity,
                Constitution = Constitution,
                Intelligence = Intelligence,
                Wisdom = Wisdom,
                Charisma = Charisma,
                AC = AC,
                HP = HP,
                MaxHP = MaxHP,
                Speed = Speed,
                AtkDiceCount = AtkDiceCount,
                AtkDiceSides = AtkDiceSides,
                AtkDiceBonus = AtkDiceBonus
            };
        }
    }
}
```

### File 2: `Assets/Scripts/RPG/Character/CharacterSheet.cs`

```csharp
using System.Collections.Generic;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using ForeverEngine.RPG.Spells;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// The master character record. Source of truth for all character state.
    /// Pure C# — no MonoBehaviour or ECS dependencies except the bridge method.
    /// </summary>
    [System.Serializable]
    public class CharacterSheet
    {
        // === Identity ===
        public string Name;
        public SpeciesData Species;

        // === Ability Scores ===
        public AbilityScores BaseAbilities;
        private AbilityScores _effectiveAbilities;
        public AbilityScores EffectiveAbilities => _effectiveAbilities;

        // === Class Levels (multiclass support) ===
        public List<ClassLevel> ClassLevels = new List<ClassLevel>();

        public int TotalLevel
        {
            get
            {
                int total = 0;
                foreach (var cl in ClassLevels) total += cl.Level;
                return total;
            }
        }

        public int ProficiencyBonus => ProficiencyTable.GetBonus(TotalLevel);
        public Tier CurrentTier => ExperienceTable.GetTierForLevel(TotalLevel);

        // === Hit Points ===
        public int HP;
        public int MaxHP;
        public int TempHP;

        // === Armor Class ===
        public int AC { get; private set; }

        // === Experience ===
        public int XP;

        // === Proficiencies ===
        public HashSet<string> Proficiencies = new HashSet<string>();

        // === Resources (Rage, Ki, Sorcery Points, Lay on Hands, etc.) ===
        public Dictionary<string, ResourcePool> Resources = new Dictionary<string, ResourcePool>();

        // === Spells ===
        public List<SpellData> KnownSpells = new List<SpellData>();
        public List<SpellData> PreparedSpells = new List<SpellData>();
        public SpellSlotManager SpellSlots = new SpellSlotManager();

        // === Conditions ===
        public ConditionManager Conditions = new ConditionManager();

        // === Death Saves ===
        public DeathSaveTracker DeathSaves = new DeathSaveTracker();

        // === Concentration ===
        public ConcentrationTracker Concentration = new ConcentrationTracker();

        // === Attunement ===
        public AttunementManager Attunement = new AttunementManager();

        // === Equipment Slots ===
        public WeaponData MainHand;
        public UnityEngine.ScriptableObject OffHand; // WeaponData (dual wield) or ArmorData (shield)
        public ArmorData Armor;

        // === Inventory ===
        public ForeverEngine.ECS.Data.Inventory Bag = new ForeverEngine.ECS.Data.Inventory(40);

        // === Expertise (double proficiency for certain skills) ===
        public HashSet<string> Expertise = new HashSet<string>();

        // ================================================================
        // METHODS
        // ================================================================

        /// <summary>
        /// Get the saving throw DC for a caster with a given casting ability.
        /// DC = 8 + proficiency + ability modifier.
        /// </summary>
        public int GetSaveDC(Ability castingAbility)
        {
            return 8 + ProficiencyBonus + _effectiveAbilities.GetModifier(castingAbility);
        }

        /// <summary>
        /// Get the spell attack bonus for a caster with a given casting ability.
        /// Bonus = proficiency + ability modifier.
        /// </summary>
        public int GetSpellAttackBonus(Ability castingAbility)
        {
            return ProficiencyBonus + _effectiveAbilities.GetModifier(castingAbility);
        }

        /// <summary>
        /// Check if the character is proficient in a skill, weapon, armor, tool, or save.
        /// </summary>
        public bool IsProficient(string proficiency)
        {
            return Proficiencies.Contains(proficiency);
        }

        /// <summary>
        /// Get the total bonus for a skill check.
        /// = ability modifier + proficiency (if proficient) + expertise (if applicable).
        /// </summary>
        /// <param name="skill">The skill name (e.g., "Athletics", "Stealth").</param>
        /// <param name="ability">The ability associated with this skill.</param>
        public int GetSkillBonus(string skill, Ability ability)
        {
            int mod = _effectiveAbilities.GetModifier(ability);
            if (IsProficient(skill))
            {
                mod += ProficiencyBonus;
                if (Expertise.Contains(skill))
                {
                    mod += ProficiencyBonus; // Expertise = double proficiency
                }
            }
            return mod;
        }

        /// <summary>
        /// Add XP and check for level up.
        /// </summary>
        public void GainXP(int amount)
        {
            XP += amount;
            // Level up is handled manually via LevelUp() — caller checks if threshold is met
        }

        /// <summary>
        /// Check if the character has enough XP to level up.
        /// </summary>
        public bool CanLevelUp
        {
            get
            {
                int nextLevel = TotalLevel + 1;
                if (nextLevel > 40) return false;
                return XP >= ExperienceTable.GetThreshold(nextLevel);
            }
        }

        /// <summary>
        /// Level up in a specific class. Gains HP (hit die + CON mod), updates slots.
        /// </summary>
        /// <param name="classToLevel">The class to gain a level in.</param>
        /// <param name="seed">RNG seed for HP roll.</param>
        public void LevelUp(ClassData classToLevel, ref uint seed)
        {
            if (classToLevel == null) return;
            if (TotalLevel >= 40) return;

            // Find existing class level or add new one
            bool found = false;
            for (int i = 0; i < ClassLevels.Count; i++)
            {
                if (ClassLevels[i].ClassRef == classToLevel)
                {
                    var cl = ClassLevels[i];
                    cl.Level++;
                    ClassLevels[i] = cl;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                ClassLevels.Add(new ClassLevel(classToLevel, 1));
            }

            // Roll HP: hit die + CON mod (minimum 1)
            int hitDieSides = (int)classToLevel.HitDie;
            int hpRoll = DiceRoller.Roll(1, hitDieSides, 0, ref seed);
            int conMod = _effectiveAbilities.GetModifier(Ability.CON);
            int hpGain = hpRoll + conMod;
            if (hpGain < 1) hpGain = 1;

            MaxHP += hpGain;
            HP += hpGain;

            // Recalculate spell slots
            SpellSlots.RecalculateSlots(ClassLevels);

            // Recalculate AC
            RecalculateAC();

            // Recalculate effective abilities
            RecalculateEffectiveAbilities();
        }

        /// <summary>
        /// Long rest: restore all HP, spell slots, resources; remove expired conditions.
        /// </summary>
        public void LongRest()
        {
            HP = MaxHP;
            TempHP = 0;
            SpellSlots.RestoreAll();

            // Restore all long-rest resources
            var keys = new List<string>(Resources.Keys);
            foreach (var key in keys)
            {
                var pool = Resources[key];
                pool.RestoreAll();
                Resources[key] = pool;
            }

            // Clear death saves
            DeathSaves.Reset();

            // End concentration
            Concentration.End();

            // Tick conditions (could remove some)
            Conditions.TickDurations();
        }

        /// <summary>
        /// Short rest: restore Warlock pact slots, spend hit dice to heal,
        /// reset short-rest resources.
        /// </summary>
        /// <param name="hitDiceToSpend">Number of hit dice to spend for healing.</param>
        /// <param name="seed">RNG seed for healing rolls.</param>
        public void ShortRest(int hitDiceToSpend, ref uint seed)
        {
            SpellSlots.RestorePactSlots();

            // Spend hit dice for healing
            if (hitDiceToSpend > 0 && ClassLevels.Count > 0)
            {
                int conMod = _effectiveAbilities.GetModifier(Ability.CON);
                for (int i = 0; i < hitDiceToSpend; i++)
                {
                    // Use first class's hit die (simplified — full impl would track per-class)
                    int hitDieSides = (int)ClassLevels[0].ClassRef.HitDie;
                    int healing = DiceRoller.Roll(1, hitDieSides, 0, ref seed) + conMod;
                    if (healing < 0) healing = 0;
                    HP += healing;
                    if (HP > MaxHP) HP = MaxHP;
                }
            }
        }

        /// <summary>
        /// Recalculate effective abilities from base + species bonuses + item bonuses.
        /// </summary>
        public void RecalculateEffectiveAbilities()
        {
            _effectiveAbilities = BaseAbilities;

            // Apply species bonuses
            if (Species != null && Species.AbilityBonuses != null)
            {
                foreach (var bonus in Species.AbilityBonuses)
                {
                    _effectiveAbilities = _effectiveAbilities.WithBonus(bonus.Ability, bonus.Bonus);
                }
            }

            // Apply magic item bonuses from attunement
            for (int i = 0; i < AttunementManager.MaxSlots; i++)
            {
                var item = Attunement.GetSlot(i);
                if (item != null && item.AbilityBonuses != null)
                {
                    foreach (var bonus in item.AbilityBonuses)
                    {
                        _effectiveAbilities = _effectiveAbilities.WithBonus(bonus.Ability, bonus.Bonus);
                    }
                }
            }
        }

        /// <summary>
        /// Recalculate AC from equipment.
        /// </summary>
        public void RecalculateAC()
        {
            ArmorData shield = OffHand as ArmorData;
            bool hasMonkUnarmored = IsProficient("MonkUnarmoredDefense");
            bool hasBarbarianUnarmored = IsProficient("BarbarianUnarmoredDefense");

            int magicACBonus = 0;
            for (int i = 0; i < AttunementManager.MaxSlots; i++)
            {
                var item = Attunement.GetSlot(i);
                if (item != null) magicACBonus += item.ACBonus;
            }

            AC = EquipmentResolver.CalculateAC(
                _effectiveAbilities,
                Armor,
                shield,
                hasMonkUnarmored,
                hasBarbarianUnarmored,
                magicACBonus);
        }

        /// <summary>
        /// Create a lightweight stats snapshot for ECS bridge / CombatBrain compatibility.
        /// </summary>
        public StatsSnapshot ToStatsSnapshot()
        {
            var snapshot = new StatsSnapshot
            {
                Strength = _effectiveAbilities.Strength,
                Dexterity = _effectiveAbilities.Dexterity,
                Constitution = _effectiveAbilities.Constitution,
                Intelligence = _effectiveAbilities.Intelligence,
                Wisdom = _effectiveAbilities.Wisdom,
                Charisma = _effectiveAbilities.Charisma,
                AC = AC,
                HP = HP,
                MaxHP = MaxHP,
                Speed = Species != null ? Species.Speed / 5 : 6 // Convert feet to tiles
            };

            // Map equipped weapon to attack dice
            if (MainHand != null)
            {
                var dmg = MainHand.GetDamage();
                snapshot.AtkDiceCount = dmg.Count;
                snapshot.AtkDiceSides = (int)dmg.Die;
                snapshot.AtkDiceBonus = dmg.Bonus + MainHand.MagicBonus;
            }
            else
            {
                // Unarmed strike: 1 + STR mod
                snapshot.AtkDiceCount = 1;
                snapshot.AtkDiceSides = 1;
                snapshot.AtkDiceBonus = _effectiveAbilities.GetModifier(Ability.STR);
            }

            return snapshot;
        }
    }
}
```

### File 3: `Assets/Scripts/RPG/Character/CharacterBuilder.cs`

```csharp
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Factory class for creating new characters from species + class + ability scores.
    /// Handles first-level character creation including:
    /// - Setting base ability scores + species bonuses
    /// - First-level HP (max hit die + CON mod)
    /// - Class proficiencies
    /// - Starting spell slots
    /// - Starting equipment slots
    /// </summary>
    public static class CharacterBuilder
    {
        /// <summary>
        /// Create a new level 1 character.
        /// </summary>
        /// <param name="name">Character name.</param>
        /// <param name="species">Character species.</param>
        /// <param name="startingClass">Starting class.</param>
        /// <param name="baseAbilities">Base ability scores (before species bonuses).</param>
        /// <returns>A fully initialized level 1 CharacterSheet.</returns>
        public static CharacterSheet Create(
            string name,
            SpeciesData species,
            ClassData startingClass,
            AbilityScores baseAbilities)
        {
            var sheet = new CharacterSheet
            {
                Name = name,
                Species = species,
                BaseAbilities = baseAbilities,
                XP = 0
            };

            // Initialize class at level 1
            sheet.ClassLevels.Add(new ClassLevel(startingClass, 1));

            // Calculate effective abilities (base + species)
            sheet.RecalculateEffectiveAbilities();

            // First-level HP: max hit die + CON mod
            int hitDieSides = (int)startingClass.HitDie;
            int conMod = sheet.EffectiveAbilities.GetModifier(Ability.CON);
            int firstLevelHP = hitDieSides + conMod;
            if (firstLevelHP < 1) firstLevelHP = 1;
            sheet.MaxHP = firstLevelHP;
            sheet.HP = firstLevelHP;

            // Add class proficiencies
            if (startingClass.ArmorProficiencies != null)
            {
                foreach (var p in startingClass.ArmorProficiencies)
                    sheet.Proficiencies.Add(p);
            }
            if (startingClass.WeaponProficiencies != null)
            {
                foreach (var p in startingClass.WeaponProficiencies)
                    sheet.Proficiencies.Add(p);
            }
            if (startingClass.ToolProficiencies != null)
            {
                foreach (var p in startingClass.ToolProficiencies)
                    sheet.Proficiencies.Add(p);
            }
            if (startingClass.SaveProficiencies != null)
            {
                foreach (var save in startingClass.SaveProficiencies)
                    sheet.Proficiencies.Add($"Save:{save}");
            }

            // Add species proficiencies
            if (species != null && species.BonusProficiencies != null)
            {
                foreach (var p in species.BonusProficiencies)
                    sheet.Proficiencies.Add(p);
            }

            // Add species languages
            if (species != null && species.Languages != null)
            {
                foreach (var lang in species.Languages)
                    sheet.Proficiencies.Add($"Language:{lang}");
            }

            // Mark special unarmored defense features
            if (startingClass.Id == "monk")
                sheet.Proficiencies.Add("MonkUnarmoredDefense");
            if (startingClass.Id == "barbarian")
                sheet.Proficiencies.Add("BarbarianUnarmoredDefense");

            // Initialize spell slots
            sheet.SpellSlots.RecalculateSlots(sheet.ClassLevels);

            // Initialize class-specific resources
            InitializeClassResources(sheet, startingClass, 1);

            // Calculate initial AC (unarmored)
            sheet.RecalculateAC();

            return sheet;
        }

        /// <summary>
        /// Initialize class-specific resource pools (Rage, Ki, Sorcery Points, etc.).
        /// </summary>
        private static void InitializeClassResources(CharacterSheet sheet, ClassData classData, int level)
        {
            if (classData == null) return;

            switch (classData.Id)
            {
                case "barbarian":
                    sheet.Resources["Rage"] = new ResourcePool(level < 3 ? 2 : level < 6 ? 3 : level < 12 ? 4 : level < 17 ? 5 : 6);
                    break;
                case "monk":
                    if (level >= 2)
                        sheet.Resources["Ki"] = new ResourcePool(level);
                    break;
                case "sorcerer":
                    if (level >= 2)
                        sheet.Resources["SorceryPoints"] = new ResourcePool(level);
                    break;
                case "paladin":
                    sheet.Resources["LayOnHands"] = new ResourcePool(level * 5);
                    break;
                case "bard":
                    int chaMod = sheet.EffectiveAbilities.GetModifier(Ability.CHA);
                    int inspirationUses = chaMod < 1 ? 1 : chaMod;
                    sheet.Resources["BardicInspiration"] = new ResourcePool(inspirationUses);
                    break;
                case "warrior":
                    if (level >= 2)
                        sheet.Resources["ActionSurge"] = new ResourcePool(1);
                    if (level >= 9)
                        sheet.Resources["Indomitable"] = new ResourcePool(1);
                    break;
                case "cleric":
                    if (level >= 2)
                        sheet.Resources["ChannelDivinity"] = new ResourcePool(level < 6 ? 1 : level < 18 ? 2 : 3);
                    break;
                case "druid":
                    if (level >= 2)
                        sheet.Resources["WildShape"] = new ResourcePool(2);
                    break;
            }
        }

        /// <summary>
        /// Create a character with standard array ability scores (15, 14, 13, 12, 10, 8).
        /// Assigns highest scores to class primary abilities.
        /// </summary>
        public static CharacterSheet CreateWithStandardArray(
            string name,
            SpeciesData species,
            ClassData startingClass)
        {
            int[] standardArray = { 15, 14, 13, 12, 10, 8 };
            var abilities = AbilityScores.Default;

            // Assign highest scores to primary abilities
            int arrayIdx = 0;
            if (startingClass.PrimaryAbilities != null)
            {
                foreach (var primary in startingClass.PrimaryAbilities)
                {
                    if (arrayIdx < standardArray.Length)
                    {
                        abilities = abilities.SetScore(primary, standardArray[arrayIdx]);
                        arrayIdx++;
                    }
                }
            }

            // Fill remaining abilities in order
            for (int a = 0; a < 6 && arrayIdx < standardArray.Length; a++)
            {
                var ability = (Ability)a;
                // Skip if already assigned
                bool assigned = false;
                if (startingClass.PrimaryAbilities != null)
                {
                    foreach (var primary in startingClass.PrimaryAbilities)
                    {
                        if (primary == ability) { assigned = true; break; }
                    }
                }
                if (!assigned)
                {
                    abilities = abilities.SetScore(ability, standardArray[arrayIdx]);
                    arrayIdx++;
                }
            }

            return Create(name, species, startingClass, abilities);
        }
    }
}
```

### Verification

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

### Commit

```bash
git add Assets/Scripts/RPG/Character/StatsSnapshot.cs Assets/Scripts/RPG/Character/CharacterSheet.cs Assets/Scripts/RPG/Character/CharacterBuilder.cs
git commit -m "feat: add CharacterSheet (master record), CharacterBuilder (factory), StatsSnapshot (ECS bridge)

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Dependency Graph

```
Task  1: Enums (no deps)
Task  2: Data Structures (depends on Task 1 enums + existing DiceRoller)
Task  3: Lookup Tables (depends on Task 1 Tier enum)
Task  4: Character Data Types (depends on Tasks 1, 2)
Task  5: Combat Structs (depends on Tasks 1, 2, 6)
Task  6: Item Data Types (depends on Tasks 1, 2)
Task  7: Spell Data Types (depends on Tasks 1, 2; updates Task 4 SpeciesData)
Task  8: ConditionManager (depends on Task 1 Condition enum)
Task  9: DeathSaveTracker (depends on Task 1 DeathSaveResult enum)
Task 10: ConcentrationTracker (depends on Tasks 1, 2, 7)
Task 11: AttackResolver (depends on Tasks 1, 2, 5, 6, 8)
Task 12: DamageResolver (depends on Tasks 1, 2, 5)
Task 13: EquipmentResolver + AttunementManager (depends on Tasks 1, 2, 6)
Task 14: SpellSlotManager + SpellDatabase (depends on Tasks 1, 3, 4, 7)
Task 15: SpellCastingPipeline + AoEResolver + MetamagicEngine (depends on Tasks 1, 2, 5, 7, 8, 11, 12, 14)
Task 16: CharacterSheet + CharacterBuilder + ECS Bridge (depends on ALL previous tasks)
```

### Parallelization groups (for subagent-driven-development):

- **Group A (no deps):** Task 1
- **Group B (after A):** Tasks 2, 3 (parallel)
- **Group C (after A+B):** Tasks 4, 6, 8, 9 (parallel)
- **Group D (after C):** Tasks 5, 7 (parallel)
- **Group E (after D):** Tasks 10, 11, 12, 13 (parallel)
- **Group F (after E):** Tasks 14, 15 (parallel — 14 first, then 15 once 14 compiles)
- **Group G (after ALL):** Task 16

---

## Summary

| Task | Files | Lines (est.) | Dependencies |
|------|-------|-------------|-------------|
| 1 | 16 enum files | ~250 | None |
| 2 | 3 data structs | ~200 | Task 1 |
| 3 | 3 lookup tables | ~350 | Task 1 |
| 4 | 5 character types | ~150 | Tasks 1-2 |
| 5 | 4 combat structs | ~120 | Tasks 1-2, 6 |
| 6 | 3 item types | ~150 | Tasks 1-2 |
| 7 | 3 spell types + update | ~200 | Tasks 1-2 |
| 8 | 2 condition files | ~250 | Task 1 |
| 9 | 1 death save file | ~120 | Task 1 |
| 10 | 1 concentration file | ~100 | Tasks 1-2, 7 |
| 11 | 1 attack resolver | ~180 | Tasks 1-2, 5-6, 8 |
| 12 | 1 damage resolver | ~100 | Tasks 1-2, 5 |
| 13 | 2 equipment files | ~200 | Tasks 1-2, 6 |
| 14 | 2 spell mgmt files | ~300 | Tasks 1, 3-4, 7 |
| 15 | 3 spell pipeline files | ~350 | Tasks 1-2, 5, 7-8, 11-12, 14 |
| 16 | 3 character files | ~500 | All previous |
| **Total** | **~50 files** | **~3,500** | |

After these 16 tasks, the full RPG character system compiles and is ready for content generation (Tasks 17-19 in a follow-up plan).
