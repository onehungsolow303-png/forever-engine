# RPG Demo Integration — Design Specification

## Overview

Wire the full RPG character system into the Shattered Kingdom demo. Replace simplified PlayerData/BattleCombatant plumbing with CharacterSheet, AttackResolver, DamageResolver, spell casting, conditions, death saves, and CR-based encounters. Player selects from 4 premade characters at the main menu.

---

## 1. Character Creation (MainMenu)

### DemoMainMenu Changes
- Add 4 premade character buttons: Human Warrior, Elf Wizard, Dwarf Cleric, Halfling Rogue
- Each button calls `CharacterBuilder.Create()` with the appropriate ClassData + SpeciesData loaded from `Assets/Scripts/RPG/Content/`
- Ability scores use standard array: 15, 14, 13, 12, 10, 8 — assigned to match class priorities (Warrior: STR 15, CON 14, DEX 13; Wizard: INT 15, DEX 14, CON 13; etc.)
- Created `CharacterSheet` stored on `GameManager.Character` (new field)
- `GameManager.Player` (PlayerData) populated from CharacterSheet as bridge for any remaining demo code that reads PlayerData

### GameManager Changes
- Add `public CharacterSheet Character { get; set; }` field
- Add `SyncPlayerFromCharacter()` — copies CharacterSheet stats to PlayerData for backward compat:
  - HP, MaxHP, AC from CharacterSheet
  - STR/DEX/CON from EffectiveAbilities
  - Speed from species
  - AttackDice from equipped weapon
  - Level from TotalLevel
- Call `SyncPlayerFromCharacter()` after character creation and after any stat change (level up, equip, rest)

### Premade Characters

| Name | Species | Class | Key Stats | Starting Spell |
|------|---------|-------|-----------|---------------|
| Human Warrior | Human | Warrior L1 | STR 16, CON 15, DEX 14 | None |
| Elf Wizard | High Elf | Wizard L1 | INT 16, DEX 16, CON 13 | Flame Dart, Arcane Bolt, Frost Ray + 6 L1 spells |
| Dwarf Cleric | Hill Dwarf | Cleric L1 | WIS 16, CON 16, STR 13 | Holy Spark, Glow + Mending Touch, Sacred Shield, Guiding Light |
| Halfling Rogue | Lightfoot Halfling | Rogue L1 | DEX 17, CON 13, INT 14 | None |

Each character starts with appropriate starting equipment:
- Warrior: Longsword (1d8 slashing), Chain Mail (AC 16), Shield (+2)
- Wizard: Quarterstaff (1d6 bludgeoning), no armor
- Cleric: Mace (1d6 bludgeoning), Scale Mail (AC 14+DEX max 2), Shield (+2)
- Rogue: Shortsword (1d6 piercing), Leather Armor (AC 11+DEX)

---

## 2. Combat Overhaul (BattleManager)

### BattleCombatant Changes
- Add `public CharacterSheet Sheet { get; set; }` (nullable — player has one, enemies don't)
- Add `public ConditionManager Conditions { get; set; }` (initialized for all combatants)
- Add `public DeathSaveTracker DeathSaves { get; set; }` (initialized for player only)
- Add `public ConcentrationTracker Concentration { get; set; }` (initialized for casters)
- Add `public DamageType Resistances, Vulnerabilities, Immunities` fields
- New factory: `FromCharacterSheet(CharacterSheet sheet)` — creates BattleCombatant from full RPG data
- Existing `FromEnemy(EnemyDef)` stays but initializes ConditionManager

### ResolveAttack Replacement
Current simple logic replaced with full pipeline:

```
1. Build AttackContext from attacker stats + target AC + conditions
2. Call AttackResolver.Resolve(ctx, ref seed) → AttackResult
3. If hit: build DamageContext with weapon dice + type + crit flag + resistances
4. Call DamageResolver.Apply(dmgCtx, ref seed) → DamageResult
5. Apply DamageResult to target HP/TempHP
6. If target at 0 HP: activate DeathSaveTracker (player) or kill (enemy)
7. If damage dealt to concentrating caster: ConcentrationTracker.CheckConcentration()
8. Fire existing AI integration hooks (OnPlayerAttacked, OnEnemyKilled, etc.)
9. Fire CombatBrain reward hooks
```

### Spell Casting in Battle
- Player presses Q to open spell menu (list of prepared spells with slot costs)
- Player selects spell + target(s)
- `SpellCastingPipeline.Cast()` resolves the spell:
  - Attack spells: AttackResolver for spell attack
  - Save spells: target rolls save vs caster DC
  - Damage applied via DamageResolver
  - Conditions applied via target's ConditionManager
  - Concentration tracked
  - Slot expended
- Enemies don't cast spells (Q-learning handles their tactics) — future enhancement

### Death Saves
- When player HP reaches 0, enter death save mode instead of instant "You have fallen"
- Each turn at 0 HP: player rolls death save (d20)
- Nat 1 = 2 failures, Nat 20 = revive with 1 HP
- 3 successes = stabilized (unconscious but alive, combat continues without player)
- 3 failures = dead (game over)
- Taking damage at 0 HP = auto-failure (crit = 2 failures)
- Any healing at 0 HP = revive with healed amount, reset death saves
- BattleHUD shows death save status (successes/failures pips)

### Conditions in Combat
- ConditionManager tracks active conditions per combatant
- Conditions affect combat:
  - Blinded: disadvantage on attacks, attacks against have advantage
  - Frightened: disadvantage on ability checks and attacks while source visible
  - Paralyzed: auto-fail STR/DEX saves, attacks have advantage, melee hits are auto-crits
  - Poisoned: disadvantage on attacks and ability checks
  - Prone: melee attacks against have advantage, ranged have disadvantage
  - Restrained: disadvantage on attacks, attacks against have advantage, disadvantage on DEX saves
  - Stunned: auto-fail STR/DEX saves, attacks have advantage
- Conditions tick at start of affected creature's turn
- Spells that apply conditions (Hold Person → Paralyze, etc.) use ConditionManager

### Turn Flow Update
```
StartTurn:
  1. Tick condition durations (remove expired)
  2. If player at 0 HP: roll death save, skip normal turn
  3. If player's turn: allow Move, Attack, Cast Spell, End Turn
  4. If enemy turn: CombatBrain decides (same as before, but attacks resolve through AttackResolver)
```

---

## 3. Encounter Upgrade (EncounterManager)

### CR-Based Encounter Generation
- Replace hardcoded EnemyDef templates with CR-scaled generation
- `EncounterData.GenerateRandom()` now calculates XP budget from player level:
  - Easy: 25 × level, Medium: 50 × level, Hard: 75 × level, Deadly: 100 × level
  - AI Director pacing multiplier adjusts the budget (0.5× to 1.5×)
- Enemy stat blocks derived from CR tables:
  - CR 1/4: ~10 HP, AC 11, +3 attack, 1d6+1 damage
  - CR 1/2: ~15 HP, AC 12, +3 attack, 1d8+1 damage
  - CR 1: ~25 HP, AC 13, +4 attack, 1d10+2 damage
  - CR 2: ~40 HP, AC 14, +5 attack, 2d6+3 damage
  - CR 3: ~55 HP, AC 15, +5 attack, 2d8+3 damage
  - CR 5: ~80 HP, AC 16, +7 attack, 2d10+4 damage
  - Higher CRs for boss encounters
- Enemy names/types still themed by biome (Forest: wolves/bandits, Ruins: skeletons/mutants)
- Enemies can have resistances/vulnerabilities (Skeleton: vulnerable to bludgeoning, resistant to piercing)

### EnemyDef Enhancement
Add to EnemyDef:
- `DamageType Resistances, Vulnerabilities, Immunities`
- `int CR` (challenge rating for XP calculation)
- `DamageType AttackDamageType` (Slashing, Piercing, etc.)

---

## 4. HUD Updates

### OverworldHUD Changes
- Show: `[Species] [Class] Lv[N]` (e.g., "High Elf Wizard Lv1")
- Show: `HP: X/Y | AC: Z | Spell Slots: 2/2`
- Existing survival stats (Hunger, Thirst) stay
- Existing AI status panel stays

### BattleHUD Changes
- Show player class/level
- Show active conditions on player and current target
- Show available spell slots by level (e.g., "L1: 2/2 | L2: 0/0")
- Show spell list when Q pressed (spell name, slot level, damage/effect summary)
- Show death save pips when player at 0 HP: ○○○ successes / ○○○ failures (fill as rolled)

### BattleLog Enhancement
- Attack messages show advantage/disadvantage: "Wanderer attacks Wolf with advantage (d20=15+4=19 vs AC 11) — Hit!"
- Crit messages: "CRITICAL HIT! Wanderer deals 14 slashing damage (doubled dice)"
- Resistance messages: "Wolf resists 4 piercing damage (halved to 2)"
- Spell messages: "Wanderer casts Flame Burst (L3, DEX save DC 14) — Wolf fails! 28 fire damage"
- Death save messages: "Death Save: d20=15 — Success (2/3)"

---

## Files Changed

| Action | File | What Changes |
|--------|------|-------------|
| Modify | `Assets/Scripts/Demo/UI/DemoMainMenu.cs` | Add 4 character selection buttons |
| Modify | `Assets/Scripts/Demo/GameManager.cs` | Add CharacterSheet field, SyncPlayerFromCharacter() |
| Modify | `Assets/Scripts/Demo/Battle/BattleCombatant.cs` | Add Sheet, Conditions, DeathSaves, Concentration, resistances, FromCharacterSheet() |
| Modify | `Assets/Scripts/Demo/Battle/BattleManager.cs` | Replace ResolveAttack, add spell casting, death saves, condition tracking |
| Modify | `Assets/Scripts/Demo/Encounters/EncounterData.cs` | CR-based generation, enhanced EnemyDef |
| Modify | `Assets/Scripts/Demo/Encounters/EncounterManager.cs` | XP budget calculation, CR scaling |
| Modify | `Assets/Scripts/Demo/UI/OverworldHUD.cs` | Show class/level/spell slots |
| Modify | `Assets/Scripts/Demo/UI/BattleHUD.cs` | Show conditions, spells, death saves |
| Create | `Assets/Scripts/Demo/RPGBridge.cs` | Premade character definitions, CharacterSheet↔PlayerData sync |
| Modify | `Assets/Editor/DemoSceneBuilder.cs` | Rebuild scenes with updated components |

---

## Testing

- Character creation: each of the 4 premades creates valid CharacterSheet with correct stats
- Combat: AttackResolver produces hits/misses/crits, DamageResolver applies resistance correctly
- Spell casting: Wizard can cast Flame Burst, Cleric can cast Mending Touch, slots decrement
- Death saves: player at 0 HP rolls saves, nat 20 revives, 3 failures = game over
- Conditions: spell that applies Paralyzed gives melee attacks auto-crit
- Encounters: CR-based generation produces appropriately difficult enemies for player level
- AI integration: all existing AI hooks (Director, Profiler, DDA) still fire correctly
- Q-learning: CombatBrain still functions with new resolver pipeline
