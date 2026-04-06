# Combat Completeness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace generic enemy stats with authentic D&D 5e creature stat blocks and expand equipment loading from 8 hardcoded items to the full 203-asset pool.

**Architecture:** New `CreatureDatabase` static class provides per-creature stat lookups (30 variants). `MapSerializer.BuildSpawns()` calls `CreatureDatabase.GetStats()` instead of scaled defaults. `RPGBridge` switches from field-based equipment loading to cache-based `Resources.Load` on demand.

**Tech Stack:** Unity 6 C# (Resources.Load, ScriptableObjects), existing RPG enum types (DamageType flags).

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Generation/Data/CreatureDatabase.cs` | **Create** | Static lookup: variant string -> D&D 5e stat block |
| `Assets/Scripts/Generation/MapSerializer.cs` | **Modify** | Use CreatureDatabase in BuildSpawns instead of scaled defaults |
| `Assets/Scripts/Demo/RPGBridge.cs` | **Modify** | Cache-based equipment loading, remove hardcoded fields |

---

### Task 1: Create CreatureDatabase.cs

**Files:**
- Create: `Assets/Scripts/Generation/Data/CreatureDatabase.cs`

**Context:** The `ForeverEngine.Generation.Data` namespace already contains `GameTables.cs`, `MapProfile.cs`, `GenerationRequest.cs`, and `RoomGraph.cs` — all static data classes. `CreatureDatabase` follows the same pattern. It uses `DamageType` flags from `ForeverEngine.RPG.Enums` for resistances/vulnerabilities/immunities.

- [ ] **Step 1: Create CreatureDatabase.cs**

Write to `Assets/Scripts/Generation/Data/CreatureDatabase.cs`:

```csharp
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
            if (_creatures.TryGetValue(variant, out var stats)) return stats;
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
```

- [ ] **Step 2: Commit CreatureDatabase**

```bash
cd "C:/Dev/Forever engin"
git add Assets/Scripts/Generation/Data/CreatureDatabase.cs
git commit -m "feat: add CreatureDatabase with 30 D&D 5e creature stat blocks

Each creature variant (goblin, skeleton, wraith, etc.) has authentic
stats: HP, AC, 6 abilities, speed, attack dice, damage type,
resistances, vulnerabilities, immunities, CR, XP, and default AI."
```

---

### Task 2: Wire MapSerializer to use CreatureDatabase

**Files:**
- Modify: `Assets/Scripts/Generation/MapSerializer.cs` (lines 1-6 usings, lines 132-155 BuildSpawns)

**Context:** `BuildSpawns()` currently creates enemy SSpawn entries with hardcoded scaled defaults. Replace the enemy loop body with a `CreatureDatabase.GetStats()` call. The `using ForeverEngine.Generation.Data;` import is already present (MapGenerationRequest is in that namespace). `DamageType` is NOT currently imported — it's in `ForeverEngine.RPG.Enums` and not needed in the JSON output (MapImporter doesn't read damage types from JSON, only stats). So no new usings needed.

- [ ] **Step 1: Replace enemy spawn block in BuildSpawns**

In `Assets/Scripts/Generation/MapSerializer.cs`, replace lines 132-155 (the enemy encounters block):

**Old code (lines 132-155):**
```csharp
            // Enemy encounters — scaled defaults per spec
            if (pop.Encounters != null)
            {
                foreach (var enc in pop.Encounters)
                {
                    spawns.Add(new SSpawn
                    {
                        name = enc.Variant ?? "creature",
                        x = enc.X,
                        y = enc.Y,
                        z = 0,
                        token_type = "enemy",
                        ai_behavior = "chase",
                        stats = new SStats
                        {
                            hp = partyLevel * 4 + 4,
                            ac = 10 + partyLevel / 2,
                            strength = 10, dexterity = 10, constitution = 10,
                            intelligence = 10, wisdom = 10, charisma = 10,
                            speed = 6,
                            atk_dice = $"1d6+{partyLevel / 2}"
                        }
                    });
                }
            }
```

**New code:**
```csharp
            // Enemy encounters — real D&D 5e stats from CreatureDatabase
            if (pop.Encounters != null)
            {
                foreach (var enc in pop.Encounters)
                {
                    var cs = CreatureDatabase.GetStats(enc.Variant);
                    spawns.Add(new SSpawn
                    {
                        name = enc.Variant ?? "creature",
                        x = enc.X,
                        y = enc.Y,
                        z = 0,
                        token_type = "enemy",
                        ai_behavior = cs.AiBehavior,
                        stats = new SStats
                        {
                            hp = cs.HP,
                            ac = cs.AC,
                            strength = cs.STR,
                            dexterity = cs.DEX,
                            constitution = cs.CON,
                            intelligence = cs.INT,
                            wisdom = cs.WIS,
                            charisma = cs.CHA,
                            speed = cs.Speed,
                            atk_dice = cs.AtkDice
                        }
                    });
                }
            }
```

- [ ] **Step 2: Commit MapSerializer changes**

```bash
cd "C:/Dev/Forever engin"
git add Assets/Scripts/Generation/MapSerializer.cs
git commit -m "feat: use CreatureDatabase for real enemy stats in MapSerializer

BuildSpawns now calls CreatureDatabase.GetStats() for each creature
variant instead of using generic scaled defaults. Goblins, skeletons,
wraiths etc. all have authentic D&D 5e stats."
```

---

### Task 3: Expand RPGBridge to cache-based equipment loading

**Files:**
- Modify: `Assets/Scripts/Demo/RPGBridge.cs`

**Context:** RPGBridge currently has 4 weapon fields (`_longsword`, `_quarterstaff`, `_mace`, `_shortsword`) and 4 armor fields (`_chainMail`, `_scaleMail`, `_leather`, `_shield`) loaded in `EnsureLoaded()`. Replace with cache dictionaries and `GetWeapon(id)` / `GetArmor(id)` methods. Keep class/species loading unchanged (only 4 of each, no expansion needed).

The premade character factory methods change from `_longsword` to `GetWeapon("longsword")`, etc.

- [ ] **Step 1: Replace RPGBridge equipment loading**

Replace the full file `Assets/Scripts/Demo/RPGBridge.cs` with:

```csharp
using System.Collections.Generic;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using UnityEngine;

namespace ForeverEngine.Demo
{
    /// <summary>
    /// Bridge between RPG CharacterSheet and Demo PlayerData.
    /// Creates premade characters and syncs CharacterSheet state to PlayerData.
    /// </summary>
    public static class RPGBridge
    {
        // Cached ScriptableObjects (loaded once from Resources)
        private static ClassData _warrior, _wizard, _cleric, _rogue;
        private static SpeciesData _human, _highElf, _hillDwarf, _lightfootHalfling;

        // Equipment caches — load any weapon/armor by ID on demand
        private static readonly Dictionary<string, WeaponData> _weaponCache = new();
        private static readonly Dictionary<string, ArmorData> _armorCache = new();

        /// <summary>
        /// Get a weapon by ID from Resources/RPG/Content/Weapons/.
        /// Caches the result for subsequent calls.
        /// </summary>
        public static WeaponData GetWeapon(string id)
        {
            if (_weaponCache.TryGetValue(id, out var w)) return w;
            w = Resources.Load<WeaponData>($"RPG/Content/Weapons/{id}");
            if (w == null) Debug.LogWarning($"[RPGBridge] Weapon not found: {id}");
            _weaponCache[id] = w;
            return w;
        }

        /// <summary>
        /// Get armor by ID from Resources/RPG/Content/Armor/.
        /// Caches the result for subsequent calls.
        /// </summary>
        public static ArmorData GetArmor(string id)
        {
            if (_armorCache.TryGetValue(id, out var a)) return a;
            a = Resources.Load<ArmorData>($"RPG/Content/Armor/{id}");
            if (a == null) Debug.LogWarning($"[RPGBridge] Armor not found: {id}");
            _armorCache[id] = a;
            return a;
        }

        /// <summary>
        /// Load class/species ScriptableObject assets from Resources folders.
        /// Call once before creating any premade characters.
        /// </summary>
        private static void EnsureClassesLoaded()
        {
            if (_warrior != null) return;

            _warrior = Resources.Load<ClassData>("RPG/Content/Classes/warrior");
            _wizard  = Resources.Load<ClassData>("RPG/Content/Classes/wizard");
            _cleric  = Resources.Load<ClassData>("RPG/Content/Classes/cleric");
            _rogue   = Resources.Load<ClassData>("RPG/Content/Classes/rogue");

            _human              = Resources.Load<SpeciesData>("RPG/Content/Species/human");
            _highElf            = Resources.Load<SpeciesData>("RPG/Content/Species/high_elf");
            _hillDwarf          = Resources.Load<SpeciesData>("RPG/Content/Species/hill_dwarf");
            _lightfootHalfling  = Resources.Load<SpeciesData>("RPG/Content/Species/lightfoot_halfling");
        }

        public static CharacterSheet CreateHumanWarrior()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(15, 13, 14, 8, 10, 12);
            var sheet = CharacterBuilder.Create("Human Warrior", _human, _warrior, abilities);

            sheet.MainHand = GetWeapon("longsword");
            sheet.OffHand  = GetArmor("shield");
            sheet.Armor    = GetArmor("chain_mail");
            sheet.RecalculateAC();
            return sheet;
        }

        public static CharacterSheet CreateElfWizard()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(8, 14, 13, 15, 10, 12);
            var sheet = CharacterBuilder.Create("Elf Wizard", _highElf, _wizard, abilities);

            sheet.MainHand = GetWeapon("quarterstaff");
            sheet.Armor    = null;
            sheet.RecalculateAC();

            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/flame_dart",
                "RPG/Content/Spells/arcane_bolt",
                "RPG/Content/Spells/ray_of_frost"
            }, isCantrip: true);
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/flame_burst",
                "RPG/Content/Spells/arcane_bolt",
                "RPG/Content/Spells/force_barrier",
                "RPG/Content/Spells/slumber",
                "RPG/Content/Spells/mage_armor",
                "RPG/Content/Spells/shockwave"
            }, isCantrip: false);

            return sheet;
        }

        public static CharacterSheet CreateDwarfCleric()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(13, 8, 14, 10, 15, 12);
            var sheet = CharacterBuilder.Create("Dwarf Cleric", _hillDwarf, _cleric, abilities);

            sheet.MainHand = GetWeapon("mace");
            sheet.OffHand  = GetArmor("shield");
            sheet.Armor    = GetArmor("scale_mail");
            sheet.RecalculateAC();

            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/holy_spark",
                "RPG/Content/Spells/glow"
            }, isCantrip: true);
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/mending_touch",
                "RPG/Content/Spells/sanctuary",
                "RPG/Content/Spells/guiding_bolt"
            }, isCantrip: false);

            return sheet;
        }

        public static CharacterSheet CreateHalflingRogue()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(10, 15, 13, 14, 8, 12);
            var sheet = CharacterBuilder.Create("Halfling Rogue", _lightfootHalfling, _rogue, abilities);

            sheet.MainHand = GetWeapon("shortsword");
            sheet.Armor    = GetArmor("leather");
            sheet.RecalculateAC();

            return sheet;
        }

        private static void LoadSpellsFromResources(CharacterSheet sheet, string[] paths, bool isCantrip)
        {
            foreach (var path in paths)
            {
                var spell = Resources.Load<ForeverEngine.RPG.Spells.SpellData>(path);
                if (spell == null)
                {
                    Debug.LogWarning($"[RPGBridge] Spell not found at Resources/{path}");
                    continue;
                }
                sheet.KnownSpells.Add(spell);
                sheet.PreparedSpells.Add(spell);
            }
        }

        public static void SyncPlayerFromCharacter(CharacterSheet sheet, PlayerData player)
        {
            if (sheet == null || player == null) return;

            var snap = sheet.ToStatsSnapshot();
            player.HP           = snap.HP;
            player.MaxHP        = snap.MaxHP;
            player.AC           = snap.AC;
            player.Strength     = snap.Strength;
            player.Dexterity    = snap.Dexterity;
            player.Constitution = snap.Constitution;
            player.Speed        = snap.Speed;
            player.Level        = sheet.TotalLevel;

            if (sheet.MainHand != null)
            {
                var dmg = sheet.MainHand.GetDamage();
                int bonus = dmg.Bonus + sheet.MainHand.MagicBonus;
                player.AttackDice = bonus != 0
                    ? $"{dmg.Count}d{(int)dmg.Die}{(bonus >= 0 ? "+" : "")}{bonus}"
                    : $"{dmg.Count}d{(int)dmg.Die}";
                player.WeaponName = sheet.MainHand.Name;
            }
            else
            {
                player.AttackDice = "1d1+" + snap.AtkDiceBonus;
                player.WeaponName = "Unarmed";
            }

            if (sheet.Armor != null)
                player.ArmorName = sheet.Armor.Name;
        }

        public static string GetClassName(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return "Adventurer";
            return sheet.ClassLevels[0].ClassRef != null ? sheet.ClassLevels[0].ClassRef.Name : "Adventurer";
        }

        public static Ability GetCastingAbility(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return Ability.INT;
            return sheet.ClassLevels[0].ClassRef != null
                ? sheet.ClassLevels[0].ClassRef.SpellcastingAbility
                : Ability.INT;
        }

        public static bool IsProficientConSave(CharacterSheet sheet)
        {
            return sheet != null && sheet.IsProficient("Save:CON");
        }
    }
}
```

**Key changes from original:**
- Removed 8 individual weapon/armor fields (`_longsword`, `_quarterstaff`, etc.)
- Added `_weaponCache` and `_armorCache` dictionaries
- Added public `GetWeapon(string id)` and `GetArmor(string id)` methods
- Renamed `EnsureLoaded()` to `EnsureClassesLoaded()` (only loads class/species now)
- Factory methods use `GetWeapon("longsword")` instead of `_longsword`
- Factory methods use `GetArmor("chain_mail")` instead of `_chainMail`
- Added `using System.Collections.Generic;` for Dictionary
- Note: `sheet.OffHand` for warrior is `GetArmor("shield")` — OffHand accepts object, ArmorData for shields is correct per existing code

- [ ] **Step 2: Commit RPGBridge changes**

```bash
cd "C:/Dev/Forever engin"
git add Assets/Scripts/Demo/RPGBridge.cs
git commit -m "feat: cache-based equipment loading in RPGBridge

Replace 8 hardcoded weapon/armor fields with GetWeapon(id) and
GetArmor(id) cache methods. Any of the 203 generated equipment
assets can now be loaded by ID on demand."
```

---

### Task 4: Verify Compilation and Rebuild Scenes

**Files:** None (verification only)

- [ ] **Step 1: Run Unity batch mode compilation check**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -quit -projectPath "C:/Dev/Forever engin" \
  -logFile - 2>&1 | grep -E "error CS|Exiting" | head -10
```

Expected: "Exiting batchmode successfully now!" with exit code 0 and no `error CS` lines.

If compilation fails, fix errors before proceeding.

- [ ] **Step 2: Rebuild demo scenes**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -quit -projectPath "C:/Dev/Forever engin" \
  -executeMethod ForeverEngine.Editor.DemoSceneBuilder.BuildAll \
  -logFile - 2>&1 | grep -E "scene created|Exiting" | head -10
```

Expected: "MainMenu scene created", "Overworld scene created", "BattleMap scene created", "Exiting batchmode successfully now!"
