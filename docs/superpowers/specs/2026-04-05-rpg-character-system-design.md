# RPG Character System — Integration Design Specification

## Overview

Build a complete D&D 5e-inspired RPG character system in a new `ForeverEngine.RPG` namespace alongside existing demo code. The system is pure C# (no MonoBehaviours, no ECS dependencies) for testability. CharacterSheet is the source of truth; StatsComponent remains the ECS combat snapshot populated via bridge methods.

**Approach:** Build alongside demo systems, migrate later. Demo stays playable throughout.

**Content scope:** Full — all 12 base classes with level 1-20 progression, prestige paths 21-40, 12+ species with subraces, 200+ spells, 14 conditions, 13 damage types.

---

## File Organization

```
Assets/Scripts/RPG/
├── Enums/
│   ├── Ability.cs              — STR, DEX, CON, INT, WIS, CHA
│   ├── DamageType.cs           — 13 types (flagged enum)
│   ├── Condition.cs            — 14 conditions (flagged enum) + composites
│   ├── SpellSchool.cs          — 8 schools
│   ├── WeaponProperty.cs       — Finesse, Heavy, Light, etc. (flagged)
│   ├── ArmorType.cs            — Light, Medium, Heavy, Shield
│   ├── SpellcastingType.cs     — None, Full, Half, Third, Pact
│   ├── Rarity.cs               — Common, Uncommon, Rare, VeryRare, Legendary
│   ├── Tier.cs                 — Adventurer(1-4) through Divine(37-40)
│   ├── DieType.cs              — D4, D6, D8, D10, D12, D20
│   ├── CoverType.cs            — None, Half, ThreeQuarters, Total
│   └── AoEShape.cs             — None, Sphere, Cone, Line, Cube, Cylinder
├── Data/
│   ├── ProficiencyTable.cs     — Level → bonus lookup (2-11, levels 1-40)
│   ├── ExperienceTable.cs      — Level → XP thresholds + GetTierForLevel()
│   ├── SpellSlotTable.cs       — Full/Half/Third caster slot tables (static arrays)
│   ├── AbilityScores.cs        — Struct: 6 ints + GetModifier(Ability) + ApplyBonus()
│   ├── DiceExpression.cs       — Struct: count, sides, bonus + Parse("2d6+3") + Roll()
│   └── ResourcePool.cs         — Struct: current, max, Spend(), Restore(), IsFull
├── Character/
│   ├── CharacterSheet.cs       — Master class: identity, classes, abilities, HP, AC, conditions, spells, equipment
│   ├── ClassData.cs            — ScriptableObject: hit die, proficiencies, progression, spell slots
│   ├── ClassLevelData.cs       — Per-level features struct
│   ├── SpeciesData.cs          — ScriptableObject: ability bonuses, traits, speed, darkvision
│   ├── SpeciesTrait.cs         — Flagged enum: FeyAncestry, Relentless, BraveHalfling, etc.
│   ├── ClassLevel.cs           — Struct: ClassData ref + level (for multiclass tracking)
│   └── CharacterBuilder.cs     — Factory: create character from species + class + ability scores
├── Combat/
│   ├── AttackResolver.cs       — Static: Resolve(AttackContext) → AttackResult
│   ├── AttackContext.cs         — Struct: attacker stats, target AC, weapon, advantage sources, conditions
│   ├── AttackResult.cs         — Struct: hit, critical, natural roll, total, advantage state
│   ├── DamageResolver.cs       — Static: Apply(DamageContext) → DamageResult
│   ├── DamageContext.cs        — Struct: dice, damage type, crit flag, resistances, temp HP
│   ├── DamageResult.cs         — Struct: total dealt, absorbed by temp HP, type, killed flag
│   ├── ConditionManager.cs     — Per-character: active conditions with duration, add/remove/query
│   ├── DeathSaveTracker.cs     — Per-character: 3 successes/failures, nat 1/nat 20 rules
│   └── ConcentrationTracker.cs — Per-character: active spell, CON save on damage, War Caster support
├── Spells/
│   ├── SpellData.cs            — ScriptableObject: name, level, school, range, components, damage, save, AoE, upcast
│   ├── SpellDatabase.cs        — Static registry: lookup by ID/name, filter by school/level/class
│   ├── SpellSlotManager.cs     — Per-character: slot calculation from caster level, multiclass stacking, Pact Magic
│   ├── SpellCastingPipeline.cs — Static: Cast(CastContext) → CastResult (validate→expend→resolve→apply)
│   ├── CastContext.cs          — Struct: caster, target(s), spell, slot level, metamagic
│   ├── CastResult.cs           — Struct: success, damage dealt, conditions applied, slot expended
│   ├── AoEResolver.cs          — Static: GetTargetsInArea(origin, shape, size, grid)
│   └── MetamagicEngine.cs      — Static: ApplyMetamagic(CastContext, type, sorceryPoints) → modified context
├── Items/
│   ├── WeaponData.cs           — ScriptableObject: damage dice, type, properties, range, magic bonus
│   ├── ArmorData.cs            — ScriptableObject: base AC, type, stealth disadvantage, STR req
│   ├── MagicItemData.cs        — ScriptableObject: rarity, attunement, stat modifiers, effects
│   ├── EquipmentResolver.cs    — Static: CalculateAC(CharacterSheet) → int
│   └── AttunementManager.cs    — Per-character: max 3 attuned, attune/unattune
└── Content/
    ├── Editor/
    │   └── ContentGenerator.cs — Editor script: generates all ScriptableObject content from code tables
    ├── Classes/                — 12 ClassData assets (generated)
    ├── Species/                — 12+ SpeciesData assets (generated)
    └── Spells/                 — 200+ SpellData assets (generated)
```

---

## Enums

### Ability
```
STR, DEX, CON, INT, WIS, CHA
```

### DamageType (flagged)
```
Slashing, Piercing, Bludgeoning, Fire, Cold, Lightning, Thunder, Poison, Acid, Necrotic, Radiant, Psychic, Force
```

### Condition (flagged)
```
Blinded, Charmed, Deafened, Frightened, Grappled, Incapacitated, Invisible,
Paralyzed, Petrified, Poisoned, Prone, Restrained, Stunned, Unconscious
```
Derived composites (calculated, not stored):
- `CantAct` = Incapacitated | Stunned | Paralyzed | Petrified | Unconscious
- `AttacksHaveAdvantage` = target is Blinded | Restrained | Stunned | Paralyzed | Unconscious
- `AttacksHaveDisadvantage` = attacker is Blinded | Frightened | Poisoned | Prone (ranged) | Restrained
- `MeleeAutoCrit` = target is Paralyzed | Unconscious (attacker within 5ft)

### SpellSchool
```
Abjuration, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation
```

### WeaponProperty (flagged)
```
Finesse, Heavy, Light, Thrown, TwoHanded, Versatile, Reach, Loading, Ammunition
```

### SpellcastingType
```
None, Full, Half, Third, Pact
```

### ClassFlag (flagged — for spell class lists)
```
Warrior, Wizard, Rogue, Cleric, Druid, Bard, Ranger, Paladin, Sorcerer, Warlock, Monk, Barbarian
```

---

## Data Structures

### AbilityScores
Struct with 6 int fields. Methods:
- `GetScore(Ability)` → int
- `GetModifier(Ability)` → int (floor((score-10)/2), matching existing DiceRoller.AbilityModifier)
- `SetScore(Ability, int)` → new AbilityScores (immutable pattern)
- `WithBonus(Ability, int)` → new AbilityScores

### DiceExpression
Struct: `int Count, DieType Die, int Bonus`. Methods:
- `static Parse(string "2d6+3")` → DiceExpression (delegates to existing DiceRoller.Parse)
- `Roll(ref uint seed)` → int (delegates to existing DiceRoller.Roll)
- `RollWithAdvantage(ref uint seed)` → int (for d20 rolls: roll twice, take higher)
- `RollWithDisadvantage(ref uint seed)` → int (roll twice, take lower)
- `CriticalDamage(ref uint seed)` → int (double the dice count, keep bonus once)

### ProficiencyTable
Static int array, 40 entries. Level 1-4: +2, 5-8: +3, 9-12: +4, 13-16: +5, 17-20: +6, 21-24: +7, 25-28: +8, 29-32: +9, 33-36: +10, 37-40: +11.

### ExperienceTable
Static int array, 40 entries for XP thresholds per level. `GetTierForLevel(int level)` → Tier enum.

### SpellSlotTable
Static 2D arrays:
- `FullCasterSlots[20][9]` — full caster slot table (levels 1-20, spell levels 1-9)
- `HalfCasterSlots[20][5]` — half caster (Paladin, Ranger)
- `ThirdCasterSlots[20][4]` — third caster (Eldritch Knight, Arcane Trickster)
- `PactMagicSlots[20][2]` — Warlock slots (count, level)
- `MulticlassSlots(int effectiveCasterLevel)` → int[9] — shared slot table for multiclass

### ResourcePool
Struct: `int Current, int Max`. Methods: `Spend(int)` → bool, `Restore(int)`, `RestoreAll()`, `IsFull` property. Used for rage, ki, sorcery points, lay on hands, etc.

---

## Character System

### CharacterSheet
The master record. Fields:
- `string Name`
- `SpeciesData Species` (reference to ScriptableObject)
- `AbilityScores BaseAbilities` (before racial/item bonuses)
- `AbilityScores EffectiveAbilities` (computed: base + species bonuses + item bonuses + ASIs)
- `List<ClassLevel> ClassLevels` (supports multiclass: [{Warrior,5}, {Wizard,3}])
- `int TotalLevel` → sum of class levels
- `int ProficiencyBonus` → from ProficiencyTable[TotalLevel]
- `Tier CurrentTier` → from ExperienceTable.GetTierForLevel(TotalLevel)
- `int HP, MaxHP, TempHP`
- `int AC` → calculated by EquipmentResolver
- `int XP`
- `HashSet<string> Proficiencies` (skills, tools, weapons, armor, saving throws)
- `Dictionary<string, ResourcePool> Resources` (rage, ki, sorcery points, etc.)
- `List<SpellData> KnownSpells`
- `List<SpellData> PreparedSpells`
- `SpellSlotManager SpellSlots`
- `ConditionManager Conditions`
- `DeathSaveTracker DeathSaves`
- `ConcentrationTracker Concentration`
- `AttunementManager Attunement`
- Equipment slots: MainHand (WeaponData), OffHand (WeaponData|ArmorData for shield), Armor (ArmorData), plus 3 attunement slots
- `Inventory Bag` (reuses existing ForeverEngine.ECS.Data.Inventory for slot/stacking)

Key methods:
- `int GetSaveDC(Ability castingAbility)` → 8 + ProficiencyBonus + EffectiveAbilities.GetModifier(castingAbility)
- `int GetSpellAttackBonus(Ability castingAbility)` → ProficiencyBonus + EffectiveAbilities.GetModifier(castingAbility)
- `bool IsProficient(string proficiency)` → checks proficiency set
- `int GetSkillBonus(string skill)` → ability mod + proficiency (if proficient) + expertise (if applicable)
- `void GainXP(int amount)` → adds XP, triggers LevelUp() if threshold crossed
- `void LevelUp(ClassData classToLevel)` → increments class level, gains HP (hit die + CON mod), gains features
- `void LongRest()` → restore all spell slots, HP to max, reset resources, remove expired conditions
- `void ShortRest()` → restore Warlock pact slots, spend hit dice to heal, reset short-rest resources
- `StatsSnapshot ToStatsSnapshot()` → lightweight struct for ECS bridge / CombatBrain compatibility

### ClassData (ScriptableObject)
- `string Id, string Name`
- `DieType HitDie`
- `Ability[] PrimaryAbilities` (for multiclass prereqs)
- `Ability SpellcastingAbility` (INT for Wizard, WIS for Cleric, CHA for Sorcerer/Bard/Warlock/Paladin)
- `SpellcastingType CastingType`
- `string[] ArmorProficiencies, WeaponProficiencies, ToolProficiencies`
- `Ability[] SaveProficiencies` (2 saves per class)
- `string[] SkillChoices` (pick N from list)
- `int SkillChoiceCount`
- `ClassLevelData[] Progression` (20 entries, one per level)
- `Ability[] MulticlassPrereqs` (minimum 13 in each)

### ClassLevelData
- `int Level`
- `string[] FeaturesGained` (e.g., "Extra Attack", "Evasion", "Subclass")
- `int? ASILevel` (levels 4, 8, 12, 16, 19 typically)
- `string SubclassFeature` (null if no subclass feature at this level)

### SpeciesData (ScriptableObject)
- `string Id, string Name`
- `Dictionary<Ability, int> AbilityBonuses` (e.g., {STR: +2, CON: +1} for Half-Orc)
- `int Size` (0=Small, 1=Medium)
- `int Speed` (typically 25-35 feet / 5-7 tiles)
- `int DarkvisionRange` (0, 60, or 120 feet)
- `SpeciesTrait Traits` (flagged enum)
- `SpellData[] InnateSpells` (Tiefling gets Hellish Rebuke, etc.)
- `string[] Languages`
- `string[] BonusProficiencies`
- `SpeciesData[] Subraces` (nested — High Elf vs Wood Elf vs Dark Elf)

---

## Combat Resolution

### AttackResolver
Static class. Single method:
```
static AttackResult Resolve(AttackContext ctx, ref uint seed)
```

AttackContext fields:
- `AbilityScores AttackerAbilities`
- `int AttackerProficiency`
- `WeaponData Weapon` (null for unarmed/spell attacks)
- `int TargetAC`
- `Condition AttackerConditions, TargetConditions`
- `bool IsRanged, bool IsMelee`
- `int CritRange` (default 20, Champion Fighter = 19, Improved Champion = 18)
- `int MagicBonus` (weapon/spell focus)
- `List<string> AdvantageReasons, DisadvantageReasons` (for logging)

Resolution logic:
1. Determine ability modifier: STR for melee (or DEX if Finesse and DEX higher), DEX for ranged
2. Evaluate advantage/disadvantage from conditions + explicit sources. Both present = cancel (straight roll)
3. Roll d20 (or 2d20 with advantage/disadvantage)
4. Natural 1 = auto-miss regardless of modifiers
5. Natural 20 (or within CritRange) = auto-hit + critical
6. Total = roll + ability mod + proficiency + magic bonus
7. Compare to target AC

AttackResult fields:
- `bool Hit, bool Critical, int NaturalRoll, int Total`
- `AdvantageState State` (None, Advantage, Disadvantage, Cancelled)

### DamageResolver
Static class:
```
static DamageResult Apply(DamageContext ctx, ref uint seed)
```

DamageContext fields:
- `DiceExpression BaseDamage`
- `DamageType Type`
- `bool Critical` (double dice count)
- `int BonusDamage` (ability mod, magic weapon, features like Rage +2)
- `DamageType Resistances, Vulnerabilities, Immunities` (from target)
- `int TargetTempHP, TargetHP`

Resolution logic:
1. Roll damage dice (if critical, double dice count but not bonus)
2. Add bonus damage (ability mod + magic + features)
3. Check immunity → 0 damage
4. Check resistance → halve (round down)
5. Check vulnerability → double
6. Absorb temp HP first
7. Remainder applied to HP

DamageResult fields:
- `int TotalRolled, int AfterResistance, int AbsorbedByTempHP, int HPDamage`
- `DamageType TypeApplied`
- `bool Killed` (HP reached 0)

### ConditionManager
Per-character instance. Fields:
- `Condition ActiveFlags` (bitfield for fast query)
- `List<ActiveCondition> Conditions` (with source, duration)

Methods:
- `Apply(Condition, int durationTurns, string source)`
- `Remove(Condition)`
- `Has(Condition)` → bool
- `TickDurations()` → removes expired, returns list of removed
- `GetAdvantageModifiers(Condition targetConditions, bool isMelee, bool isRanged)` → advantage/disadvantage sources

### DeathSaveTracker
- `int Successes, Failures` (0-3 each)
- `RollDeathSave(int d20Roll)` → result enum: Success, Failure, Stabilized, Revived, Dead
- Nat 1 = 2 failures. Nat 20 = revived with 1 HP. 10+ = success. <10 = failure.
- `TakeDamageAtZero(bool critical)` → adds 1 failure (2 if critical)
- `Reset()` — called when healed above 0

### ConcentrationTracker
- `SpellData ActiveSpell` (null if none)
- `Begin(SpellData)` → ends previous concentration if any
- `CheckConcentration(int damageTaken, AbilityScores abilities, int proficiency, bool hasWarCaster, ref uint seed)` → bool (pass/fail)
- DC = max(10, damageTaken / 2). CON save. War Caster = advantage.
- `End()` → clears active spell

---

## Spell System

### SpellData (ScriptableObject)
- `string Id, string Name`
- `int Level` (0 = cantrip, 1-9)
- `SpellSchool School`
- `string CastingTime` ("action", "bonus_action", "reaction", "1_minute", "10_minutes")
- `int Range` (feet; 0 = self, 5 = touch, then 30/60/90/120/150)
- `bool Verbal, Somatic, Material`
- `string MaterialDescription` (if Material)
- `string Duration` ("instantaneous", "1_round", "1_minute", "10_minutes", "1_hour", "concentration_1_minute", etc.)
- `bool Concentration, Ritual`
- `DiceExpression Damage` (null if no damage)
- `DamageType DamageType`
- `Ability SaveType` (DEX, CON, WIS, etc. — None if attack roll)
- `bool SpellAttack` (true if uses attack roll instead of save)
- `AoEShape AreaShape`
- `int AreaSize` (radius/length in feet)
- `DiceExpression UpcastDamagePerLevel` (extra per slot level above base)
- `Condition AppliesCondition` (if any)
- `int ConditionDuration` (turns)
- `string HealingDice` (for healing spells)
- `ClassFlag Classes` (flagged: which classes can learn this)

### SpellDatabase
Static class:
- `Dictionary<string, SpellData> ById`
- `GetByName(string)`, `GetBySchool(SpellSchool)`, `GetByLevel(int)`, `GetByClass(ClassFlag)`
- Populated at startup from `Resources.LoadAll<SpellData>("RPG/Spells")`

### SpellSlotManager
Per-character instance:
- `int[] AvailableSlots` (9 entries, levels 1-9)
- `int[] MaxSlots` (calculated from class levels)
- `int PactSlotCount, PactSlotLevel` (Warlock)
- `RecalculateSlots(List<ClassLevel> classLevels)` — sums effective caster level across multiclass, looks up shared table
- `CanCast(SpellData spell, int slotLevel)` → bool
- `ExpendSlot(int level)` → bool
- `RestoreAll()` — long rest
- `RestorePactSlots()` — short rest

Multiclass caster level calculation:
- Full caster class levels count at 1×
- Half caster class levels count at 0.5× (round down)
- Third caster class levels count at 0.33× (round down)
- Sum = effective caster level → look up MulticlassSlots table

### SpellCastingPipeline
Static class:
```
static CastResult Cast(CastContext ctx, ref uint seed)
```

CastContext:
- `CharacterSheet Caster`
- `CharacterSheet[] Targets` (or positions for AoE)
- `SpellData Spell`
- `int SlotLevel` (must be >= spell level)
- `MetamagicType Metamagic` (None, or Twinned/Quickened/etc.)

Steps:
1. Validate: caster knows spell, has slot at required level (or cantrip = no slot)
2. Apply metamagic (if any) — modify context, spend sorcery points
3. Expend spell slot (unless cantrip or ritual)
4. If spell attack: call AttackResolver with spell attack bonus
5. If save-based: target rolls save vs caster's spell DC
6. Roll damage/healing with upcast scaling
7. Apply damage via DamageResolver (or apply healing)
8. Apply conditions if save failed
9. Set concentration if spell requires it (ends previous)

CastResult:
- `bool Success`
- `int DamageDealt, int HealingDone`
- `Condition ConditionsApplied`
- `int SlotExpended`
- `bool ConcentrationStarted`

### MetamagicEngine
Static class:
- `ApplyMetamagic(CastContext, MetamagicType, ref ResourcePool sorceryPoints)` → modified CastContext
- Types: Twinned (cost = spell level, duplicate target), Quickened (cost 2, bonus action), Subtle (cost 1, no V/S), Empowered (cost 1, reroll damage dice), Heightened (cost 3, target has disadvantage on save), Careful (cost 1, chosen allies auto-save), Distant (cost 1, double range)

---

## Items & Equipment

### WeaponData (ScriptableObject)
- `string Id, Name`
- `DiceExpression Damage`
- `DamageType Type` (Slashing, Piercing, Bludgeoning)
- `WeaponProperty Properties` (flagged)
- `string ProficiencyGroup` ("Simple" or "Martial")
- `int NormalRange, LongRange` (0 if melee-only)
- `DiceExpression VersatileDamage` (if Versatile property)
- `int MagicBonus` (0-3)
- `Rarity Rarity`

### ArmorData (ScriptableObject)
- `string Id, Name`
- `int BaseAC`
- `ArmorType Type`
- `bool StealthDisadvantage`
- `int StrengthRequirement`
- `int MagicBonus`
- `Rarity Rarity`

### MagicItemData (ScriptableObject)
- `string Id, Name`
- `Rarity Rarity`
- `bool RequiresAttunement`
- `Dictionary<Ability, int> AbilityBonuses`
- `int ACBonus, SaveBonus`
- `string[] EffectTags` (interpreted at runtime)

### EquipmentResolver
Static:
- `CalculateAC(CharacterSheet sheet)` → int
- Logic: if no armor → 10 + DEX mod (or Unarmored Defense variant). Light → base + DEX. Medium → base + DEX (max 2). Heavy → base flat. Add shield +2. Add magic bonuses.
- Monk Unarmored Defense: 10 + DEX + WIS
- Barbarian Unarmored Defense: 10 + DEX + CON

### AttunementManager
- `MagicItemData[] Slots` (3 max)
- `bool Attune(MagicItemData item)` → false if full
- `void Unattune(int slot)`
- `bool IsAttuned(MagicItemData item)`

---

## Content: Full Class Definitions

All 12 base classes with complete progression:

| Class | Hit Die | Casting | Primary | Saves | Key Features |
|-------|---------|---------|---------|-------|--------------|
| Warrior (Fighter) | d10 | Third (EK subclass) | STR/DEX | STR, CON | Fighting Style, Extra Attack (×3), Action Surge, Indomitable |
| Wizard | d6 | Full (INT) | INT | INT, WIS | Arcane Recovery, Spell Mastery, Signature Spells |
| Rogue | d8 | Third (AT subclass) | DEX | DEX, INT | Sneak Attack (1d6-10d6), Cunning Action, Evasion, Uncanny Dodge |
| Cleric | d8 | Full (WIS) | WIS | WIS, CHA | Channel Divinity, Divine Intervention, Turn Undead |
| Druid | d8 | Full (WIS) | WIS | INT, WIS | Wild Shape, Nature's Ward, Timeless Body |
| Bard | d8 | Full (CHA) | CHA | DEX, CHA | Bardic Inspiration (d6-d12), Expertise, Magical Secrets |
| Ranger | d10 | Half (WIS) | DEX/WIS | STR, DEX | Favored Enemy, Natural Explorer, Extra Attack |
| Paladin | d10 | Half (CHA) | STR/CHA | WIS, CHA | Divine Smite (2d8+), Lay on Hands, Aura of Protection |
| Sorcerer | d6 | Full (CHA) | CHA | CON, CHA | Metamagic, Sorcery Points, Font of Magic |
| Warlock | d8 | Pact (CHA) | CHA | WIS, CHA | Pact Magic (short rest slots), Eldritch Invocations, Pact Boon |
| Monk | d8 | None | DEX/WIS | STR, DEX | Ki Points, Flurry of Blows, Stunning Strike, Unarmored Defense |
| Barbarian | d12 | None | STR | STR, CON | Rage (+2-4 damage), Reckless Attack, Danger Sense, Unarmored Defense |

Each class includes 20-level progression table with features at every level.

Prestige paths (levels 21-40): Arch-mage, Battle Master Supreme, Shadow Lord, High Priest, Archdruid, Loremaster, Beast Lord, Holy Avenger, Wild Mage, Eldritch Master, Grand Master, Titan.

---

## Content: Species

| Species | Ability Bonuses | Speed | Darkvision | Key Traits |
|---------|----------------|-------|------------|------------|
| Human | +1 to all | 30ft | None | Extra skill, extra feat |
| High Elf | +2 DEX, +1 INT | 30ft | 60ft | Fey Ancestry, Trance, Cantrip |
| Wood Elf | +2 DEX, +1 WIS | 35ft | 60ft | Fey Ancestry, Mask of the Wild |
| Dark Elf (Drow) | +2 DEX, +1 CHA | 30ft | 120ft | Superior Darkvision, Sunlight Sensitivity, Drow Magic |
| Mountain Dwarf | +2 STR, +2 CON | 25ft | 60ft | Dwarven Resilience, Stonecunning, Armor Proficiency |
| Hill Dwarf | +2 CON, +1 WIS | 25ft | 60ft | Dwarven Resilience, Dwarven Toughness (+1 HP/level) |
| Lightfoot Halfling | +2 DEX, +1 CHA | 25ft | None | Lucky (reroll nat 1), Brave, Naturally Stealthy |
| Stout Halfling | +2 DEX, +1 CON | 25ft | None | Lucky, Brave, Stout Resilience (poison resistance) |
| Dragonborn | +2 STR, +1 CHA | 30ft | None | Breath Weapon (damage type by color), Damage Resistance |
| Tiefling | +1 INT, +2 CHA | 30ft | 60ft | Hellish Resistance (fire), Infernal Legacy (innate spells) |
| Half-Orc | +2 STR, +1 CON | 30ft | 60ft | Relentless Endurance (1/rest avoid KO), Savage Attacks (extra crit die) |
| Forest Gnome | +2 INT, +1 DEX | 25ft | 60ft | Gnome Cunning (advantage on INT/WIS/CHA saves vs magic), Minor Illusion |
| Rock Gnome | +2 INT, +1 CON | 25ft | 60ft | Gnome Cunning, Tinker |
| Half-Elf | +2 CHA, +1 to two others | 30ft | 60ft | Fey Ancestry, 2 extra skills |
| Changeling | +2 CHA, +1 any | 30ft | None | Shapechanger (alter appearance at will) |

---

## Content: Spell Count by Level

| Cantrips | L1 | L2 | L3 | L4 | L5 | L6 | L7 | L8 | L9 | Total |
|----------|----|----|----|----|----|----|----|----|-----|-------|
| ~30 | ~35 | ~30 | ~25 | ~20 | ~20 | ~15 | ~12 | ~10 | ~8 | ~205 |

Spells cover all 8 schools across all casting classes. Includes all core D&D 5e spells plus original additions for prestige path abilities.

---

## ECS Bridge

`CharacterSheet.ToStatsSnapshot()` produces a `StatsSnapshot` struct compatible with the existing `StatsComponent`:
- Maps EffectiveAbilities → STR/DEX/CON/INT/WIS/CHA
- Maps equipped weapon → AtkDiceCount/AtkDiceSides/AtkDiceBonus
- Maps CalculateAC → AC
- Maps HP/MaxHP/Speed directly

This allows the demo's `BattleCombatant.FromCharacterSheet(CharacterSheet)` bridge to work alongside the existing `FromPlayer(PlayerData)` path.

---

## Content Generation Strategy

All 200+ spells, 12 classes, and 15 species are defined in a single editor script (`ContentGenerator.cs`) that programmatically creates ScriptableObject assets. This is more maintainable than hand-editing hundreds of asset files:

```
ContentGenerator.GenerateAll()
  → Creates Assets/Scripts/RPG/Content/Classes/Warrior.asset, Wizard.asset, ...
  → Creates Assets/Scripts/RPG/Content/Species/Human.asset, HighElf.asset, ...
  → Creates Assets/Scripts/RPG/Content/Spells/Fireball.asset, MagicMissile.asset, ...
```

Each ScriptableObject is created with `ScriptableObject.CreateInstance<T>()` and saved via `AssetDatabase.CreateAsset()`. The editor script contains all the data tables (class progressions, spell definitions, species stats) as inline C# — single source of truth.

---

## Testing Strategy

All RPG code is pure C# with no Unity dependencies in the logic layer. Testing via Unity Test Framework (already in project):
- AbilityScores: modifier calculation for all values 1-30
- DiceExpression: parsing, rolling, advantage/disadvantage, critical damage
- AttackResolver: nat 1, nat 20, normal hit/miss, advantage/disadvantage cancellation, expanded crit range
- DamageResolver: resistance, vulnerability, immunity, temp HP absorption, critical damage
- ConditionManager: apply/remove, duration tick, composite derivation
- DeathSaveTracker: all edge cases (nat 1, nat 20, 3 successes, 3 failures)
- SpellSlotManager: full caster, half caster, multiclass, Pact Magic
- SpellCastingPipeline: attack spell, save spell, healing, upcast, metamagic
- EquipmentResolver: each armor type, Unarmored Defense variants, shield, magic bonuses
- CharacterSheet: level up, multiclass, long rest, short rest, XP progression
- ContentGenerator: verify all 12 classes, 15 species, 200+ spells create without errors
