# RPG Content Generation — Design Specification

## Overview

Generate all game content (12 classes, 15 species, 200+ spells) as ScriptableObject assets via editor scripts. Three split generators keep files focused and manageable. All spell names are original (SRD-safe) with D&D 5e-equivalent mechanics.

---

## File Organization

```
Assets/Editor/RPG/
├── ContentGenerator.cs     — Master: calls all three, menu item, batch mode entry
├── ClassGenerator.cs       — Generates 12 ClassData assets
├── SpeciesGenerator.cs     — Generates 15 SpeciesData assets
└── SpellGenerator.cs       — Generates 200+ SpellData assets

Assets/Scripts/RPG/Content/
├── Classes/                — 12 ClassData .asset files (generated)
├── Species/                — 15 SpeciesData .asset files (generated)
└── Spells/                 — 200+ SpellData .asset files (generated)
```

---

## ContentGenerator.cs (Master)

- `[MenuItem("Forever Engine/RPG/Generate All Content")]` menu item
- Static `GenerateAll()` method callable from batch mode
- Calls `ClassGenerator.GenerateAll()`, `SpeciesGenerator.GenerateAll()`, `SpellGenerator.GenerateAll()` in order
- Validation pass: loads all assets from Content folders, logs counts, warns on any null references
- Batch mode entry: `ForeverEngine.Editor.RPG.ContentGenerator.GenerateAll`

---

## ClassGenerator.cs — 12 Classes

Each class creates a ClassData ScriptableObject with complete 20-level progression.

### Class List

| Class | Hit Die | Casting | Ability | Saves | Key Features |
|-------|---------|---------|---------|-------|-------------|
| Warrior | D10 | Third (subclass) | STR/DEX | STR, CON | Fighting Style, Action Surge, Extra Attack (×3), Indomitable |
| Wizard | D6 | Full (INT) | INT | INT, WIS | Arcane Recovery, Arcane Tradition, Spell Mastery |
| Rogue | D8 | Third (subclass) | DEX | DEX, INT | Sneak Attack (1d6→10d6), Cunning Action, Evasion, Uncanny Dodge |
| Cleric | D8 | Full (WIS) | WIS | WIS, CHA | Channel Divinity, Turn Undead, Divine Intervention |
| Druid | D8 | Full (WIS) | WIS | INT, WIS | Wild Shape, Nature's Ward, Timeless Body |
| Bard | D8 | Full (CHA) | CHA | DEX, CHA | Bardic Inspiration (d6→d12), Expertise, Magical Secrets |
| Ranger | D10 | Half (WIS) | DEX/WIS | STR, DEX | Favored Enemy, Natural Explorer, Extra Attack |
| Paladin | D10 | Half (CHA) | STR/CHA | WIS, CHA | Divine Smite, Lay on Hands, Aura of Protection |
| Sorcerer | D6 | Full (CHA) | CHA | CON, CHA | Font of Magic, Metamagic, Sorcery Points |
| Warlock | D8 | Pact (CHA) | CHA | WIS, CHA | Pact Magic, Eldritch Invocations, Pact Boon |
| Monk | D8 | None | DEX/WIS | STR, DEX | Ki Points, Flurry of Blows, Stunning Strike, Unarmored Defense |
| Barbarian | D12 | None | STR | STR, CON | Rage, Reckless Attack, Danger Sense, Brutal Critical, Unarmored Defense |

### Per-Class Data

Each ClassData asset includes:
- `Id`: lowercase class name (e.g., "warrior")
- `Name`: display name
- `HitDie`: DieType enum value
- `PrimaryAbilities`: Ability[] for multiclass prereqs
- `SpellcastingAbility`: Ability (or STR as placeholder for non-casters)
- `CastingType`: SpellcastingType enum
- `ArmorProficiencies`, `WeaponProficiencies`, `ToolProficiencies`: string[]
- `SaveProficiencies`: Ability[2]
- `SkillChoices`: string[] of available skills
- `SkillChoiceCount`: int
- `MulticlassPrereqs`: Ability[] (minimum 13)
- `Progression`: ClassLevelData[20] with per-level features

### Standard Progression Pattern

All classes share ASI levels at 4, 8, 12, 16, 19. Subclass features typically at levels 3, 7, 11, 15, 20 (varies by class). Each level entry lists:
- `Level`: 1-20
- `FeaturesGained`: string[] of feature names
- `HasASI`: bool (true at ASI levels)
- `SubclassFeature`: string or null

---

## SpeciesGenerator.cs — 15 Species

### Species List

| Species | Ability Bonuses | Speed | Darkvision | Traits |
|---------|----------------|-------|------------|--------|
| Human | +1 all | 30 | 0 | ExtraSkill, ExtraFeat |
| High Elf | +2 DEX, +1 INT | 30 | 60 | FeyAncestry, Trance, ElfWeaponTraining, BonusCantrip |
| Wood Elf | +2 DEX, +1 WIS | 35 | 60 | FeyAncestry, Trance, ElfWeaponTraining, MaskOfTheWild |
| Dark Elf | +2 DEX, +1 CHA | 30 | 120 | FeyAncestry, Trance, SuperiorDarkvision, SunlightSensitivity, DrowMagic |
| Mountain Dwarf | +2 STR, +2 CON | 25 | 60 | DwarvenResilience, Stonecunning, DwarvenArmorTraining |
| Hill Dwarf | +2 CON, +1 WIS | 25 | 60 | DwarvenResilience, Stonecunning, DwarvenToughness |
| Lightfoot Halfling | +2 DEX, +1 CHA | 25 | 0 | Lucky, BraveHalfling, NaturallyStealthy |
| Stout Halfling | +2 DEX, +1 CON | 25 | 0 | Lucky, BraveHalfling, StoutResilience |
| Dragonborn | +2 STR, +1 CHA | 30 | 0 | BreathWeapon, DamageResistance |
| Tiefling | +1 INT, +2 CHA | 30 | 60 | HellishResistance, InfernalLegacy |
| Half-Orc | +2 STR, +1 CON | 30 | 60 | RelentlessEndurance, SavageAttacks |
| Forest Gnome | +2 INT, +1 DEX | 25 | 60 | GnomeCunning, NaturalIllusionist |
| Rock Gnome | +2 INT, +1 CON | 25 | 60 | GnomeCunning, Tinker |
| Half-Elf | +2 CHA, +1 two | 30 | 60 | FeyAncestry, ExtraSkill |
| Changeling | +2 CHA, +1 any | 30 | 0 | Shapechanger |

### Per-Species Data

Each SpeciesData asset includes:
- `Id`: lowercase (e.g., "high_elf")
- `Name`: display name
- `AbilityBonuses`: serialized array of (Ability, int) pairs
- `Size`: 0=Small, 1=Medium
- `Speed`: int (feet)
- `DarkvisionRange`: int (0, 60, or 120)
- `Traits`: SpeciesTrait flagged enum
- `Languages`: string[] (e.g., "Common", "Elvish")
- `BonusProficiencies`: string[]
- `InnateSpells`: SpellData[] (null for most; Tiefling/Drow get innate spells — these reference SpellData assets, so spells must be generated first)
- `Subraces`: SpeciesData[] (null for species without subraces)

---

## SpellGenerator.cs — 200+ Spells (Original Names)

All spells use **original names** with D&D 5e-equivalent mechanics to avoid IP concerns.

### Naming Convention

| D&D 5e Name | Original Name | Same Mechanics |
|-------------|---------------|----------------|
| Magic Missile | Arcane Bolt | 3×1d4+1 force, auto-hit |
| Fireball | Flame Burst | 8d6 fire, 20ft sphere, DEX save |
| Shield | Arcane Ward | +5 AC reaction |
| Cure Wounds | Mending Touch | 1d8+mod healing |
| Healing Word | Healing Whisper | 1d4+mod healing, bonus action |
| Lightning Bolt | Thunder Lance | 8d6 lightning, 100ft line, DEX save |
| Counterspell | Spell Break | Reaction, counter casting |
| Eldritch Blast | Eldritch Ray | 1d10 force, cantrip |
| etc. | etc. | etc. |

### Spell Count by Level

| Cantrips | L1 | L2 | L3 | L4 | L5 | L6 | L7 | L8 | L9 | Total |
|----------|----|----|----|----|----|----|----|----|-----|-------|
| 30 | 35 | 30 | 25 | 20 | 20 | 15 | 12 | 10 | 8 | 205 |

### Per-Spell Data

Each SpellData asset includes:
- `Id`: snake_case (e.g., "flame_burst")
- `Name`: display name (e.g., "Flame Burst")
- `Level`: 0-9 (0 = cantrip)
- `School`: SpellSchool enum
- `CastingTime`: string ("action", "bonus_action", "reaction", "1_minute", "10_minutes")
- `Range`: int (feet; 0=self, 5=touch)
- `Verbal`, `Somatic`, `Material`: bool
- `MaterialDescription`: string (empty if no material)
- `Duration`: string ("instantaneous", "1_round", "1_minute", "concentration_1_minute", etc.)
- `Concentration`: bool
- `Ritual`: bool
- `Damage`: DiceExpression (or default if no damage)
- `DamageType`: DamageType enum
- `SaveType`: Ability enum (STR if no save — check SpellAttack flag)
- `SpellAttack`: bool
- `AreaShape`: AoEShape enum
- `AreaSize`: int (feet)
- `UpcastDamagePerLevel`: DiceExpression (extra per slot above base)
- `AppliesCondition`: Condition enum
- `ConditionDuration`: int (turns)
- `HealingDice`: string (e.g., "1d8" — parsed at runtime)
- `Classes`: ClassFlag (which classes can learn)

### Spell Categories (all 205)

**Cantrips (30):** Damage cantrips (Eldritch Ray, Firebolt→Flame Dart, Sacred Flame→Holy Spark, etc.), utility cantrips (Light→Glow, Mage Hand→Spectral Hand, Prestidigitation→Minor Trick, etc.)

**Level 1 (35):** Combat (Arcane Bolt, Burning Grasp→Searing Touch, Thunderwave→Shockwave), healing (Mending Touch, Healing Whisper), utility (Arcane Ward, Detect Magic→Sense Magic, Identify→Arcane Insight), control (Sleep→Slumber, Entangle→Grasping Vines)

**Level 2 (30):** Combat (Scorching Ray→Searing Rays, Shatter→Sonic Blast), healing (Lesser Restoration→Purify), utility (Invisibility→Vanish, Knock→Open Lock, Misty Step→Blink Step), control (Hold Person→Paralyze, Web→Spider Silk)

**Level 3 (25):** Combat (Flame Burst, Thunder Lance, Spirit Guardians→Radiant Sentinels), healing (Revivify→Resurgence), utility (Counterspell→Spell Break, Dispel Magic→Nullify, Fly→Soar, Haste→Quicken), control (Hypnotic Pattern→Mesmerize, Slow→Lethargy)

**Level 4 (20):** Combat (Ice Storm→Frozen Tempest, Blight→Wither), healing (Greater Restoration→Greater Purify partially), utility (Banishment→Exile, Dimension Door→Portal Step, Polymorph→Shapeshift), control (Confusion→Bewilderment, Wall of Fire→Flame Wall)

**Level 5 (20):** Combat (Cone of Cold→Frost Cone, Flame Strike→Heaven's Fire), healing (Mass Cure→Mass Mending, Raise Dead→Resurrection Call), utility (Teleportation Circle→Warp Circle, Scrying→Far Sight), control (Hold Monster→Greater Paralyze, Wall of Force→Force Barrier)

**Level 6 (15):** Combat (Chain Lightning→Arc Storm, Disintegrate→Annihilate, Harm→Bane Touch), healing (Heal→Restoration, Heroes' Feast→Champion's Feast), utility (Globe of Invulnerability→Spell Aegis, True Seeing→Truesight), control (Mass Suggestion→Mass Command, Otto's Dance→Compelled Dance)

**Level 7 (12):** Combat (Finger of Death→Death Grasp, Fire Storm→Inferno), healing (Regenerate→Regrowth, Resurrection→Greater Resurrection Call), utility (Teleport→Greater Warp, Plane Shift→Realm Shift), control (Forcecage→Prison of Force, Reverse Gravity→Gravity Reversal)

**Level 8 (10):** Combat (Sunburst→Solar Flare, Earthquake→Cataclysm), healing (none at L8), utility (Demiplane→Pocket Realm, Maze→Labyrinth, Mind Blank→Thought Shield), control (Dominate Monster→Dominate Creature, Power Word Stun→Word of Stunning, Feeblemind→Mind Shatter)

**Level 9 (8):** Combat (Meteor Swarm→Cataclysmic Barrage, Power Word Kill→Word of Death), healing (Mass Heal→Mass Restoration, True Resurrection→True Rebirth), utility (Wish→Miracle, Gate→Planar Gate, Foresight→Prescience, Time Stop→Temporal Halt)

---

## Generation Order

1. **Spells first** — So species with innate spells can reference them
2. **Classes second** — Independent of other content
3. **Species third** — References spell assets for innate spells (Tiefling, Drow)

---

## Execution

### From Unity Menu
- `Forever Engine > RPG > Generate All Content`
- `Forever Engine > RPG > Generate Classes`
- `Forever Engine > RPG > Generate Species`
- `Forever Engine > RPG > Generate Spells`

### From Batch Mode
```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod ForeverEngine.Editor.RPG.ContentGenerator.GenerateAll -logFile tests/content-gen.log -quit
```

### Validation Output
```
[ContentGenerator] Generated 12 ClassData assets
[ContentGenerator] Generated 15 SpeciesData assets
[ContentGenerator] Generated 205 SpellData assets
[ContentGenerator] Validation: 232 total assets, 0 errors
```

---

## Testing

- Verify all 12 class assets load and have 20 progression entries each
- Verify all 15 species assets load with correct ability bonuses
- Verify all 205 spell assets load with non-default damage/school/level
- Verify species innate spell references are not null
- Verify class casting types match expected (Wizard=Full, Warrior=Third, Barbarian=None, etc.)
- Verify spell class flags (e.g., Flame Burst accessible by Wizard|Sorcerer, not by Cleric)
