# Combat balance audit (2026-04-07)

Investigation of the "I died instantly with no turn" complaint, follow-up to commit `c6fc22d` (full heal on respawn + player first turn). The first-turn fix is necessary but not sufficient — the underlying encounter math is hostile at low levels because player HP doesn't scale with the encounter scaling.

## The math

**Player at level 1** (default `PlayerData`):
| Stat | Value |
|---|---|
| HP / MaxHP | 20 / 20 |
| AC | 12 |
| Strength | 14 (+2 mod) |
| Dex | 12 (+1 mod) |
| AttackDice | `1d8+2` (avg 6.5, max 10) |

**Enemy stat blocks** (from `EncounterData.MakeCREnemyDef`):

| CR | XP | HP | AC | AtkDice | Avg dmg | Max dmg |
|---|---|---|---|---|---|---|
| 1/4 | 25 | 10 | 11 | `1d6+1` | 4.5 | 7 |
| 1/2 | 50 | 15 | 12 | `1d8+1` | 5.5 | 9 |
| 1   | 100 | 25 | 13 | `1d10+2` | 7.5 | 12 |
| 2   | 200 | 40 | 14 | `2d6+3` | 10 | 15 |
| 3   | 450 | 55 | 15 | `2d8+3` | 12 | 19 |
| 5   | 900 | 80 | 16 | `2d10+4` | 15 | 24 |

**Encounter XP budget** (`EncounterData.GenerateRandom`):
- Day: `50 × playerLevel`
- Night: `75 × playerLevel`
- Then divided by per-enemy XP, capped at 4-5 enemies depending on biome

## The death scenario

**Level 1, Plains, day**: budget = 50 XP, bandit XP = 50 → **1 bandit**. Survivable.

**Level 1, Plains, night**: budget = 75 XP, bandit XP = 50 → still 1 bandit (integer floor). Survivable.

**Level 5, Plains, night**: budget = 375 XP, bandit XP = 50 → cap at **4 bandits**.

But here's the catch: **`PlayerData.MaxHP` is hard-coded to 20** with no per-level scaling:

```csharp
public int HP = 20, MaxHP = 20, AC = 12;
```

So a level-5 player walks into a 4-bandit encounter with the same 20 HP a level-1 player has. The math:

```
Round 1 (player goes first thanks to c6fc22d's promote-to-front):
  Player attack: 1d8+2 vs AC 12. Player attack roll = 1d20+2 vs 12 → ~55% hit.
  Expected damage to ONE bandit per round: 0.55 × 6.5 = ~3.6
  Bandit HP: 15. Survives the player's first hit easily.

  4 bandits attack player: 1d20+1 vs AC 12 → 50% hit each.
  Expected damage to player per round: 4 × 0.5 × 5.5 = 11

  Player HP after round 1: 20 - 11 = 9.

Round 2:
  Player still hasn't killed any bandit (~7 HP dealt cumulative).
  Bandits hit again: another 11 expected → player at -2 HP. **Dead.**
```

**Player goes first but still dies in round 2** because the 4-vs-1 numerical disadvantage swamps the first-turn benefit. Worst case (max rolls), the bandits combined max 36 damage in one round vs 20 player HP — instant kill regardless of turn order.

## Root cause

`PlayerData.MaxHP` is constant. `EncounterData` budget scales with `playerLevel`. The scaling assumes the player ALSO scales with level, which they don't (unless they go through `CharacterSheet`, which has its own HP track).

This is a contract violation between the encounter generator and the player data model. The encounter generator says "this player is level 5, throw level-5 challenges at them" but the player is still level-1 fragile.

## Three fixes (pick one)

### Option A — Scale player HP with level
Smallest disruption to game design. Add to `PlayerData`:
```csharp
public int MaxHP => 20 + (Level - 1) * 8;
```
Make `HP` track this on level-up. A level-5 player has 52 HP → can survive 4-bandit night encounters comfortably.

### Option B — Tame encounter scaling
Reduce encounter aggression. Change `EncounterData.GenerateRandom`:
```csharp
int xpBudget = (int)((night ? 75 : 50) * playerLevel * pacingMult);
// becomes:
int xpBudget = (int)((night ? 50 : 30) * playerLevel * pacingMult);
```
Plus drop the cap from 5 to 3 enemies. Means the player faces fewer/easier encounters at every level.

### Option C — Both A and B (lighter versions)
- Player MaxHP = `20 + (Level - 1) * 5` (slightly less generous than A)
- Encounter budget = `(night ? 60 : 40) * playerLevel` (slightly less aggressive than B)
- Cap at 4 enemies

## Recommendation

**Option C** because it addresses both the player-fragility side and the encounter-spam side without overcorrecting either. Option A alone leaves the option for absurd encounter density at high levels; Option B alone leaves the player feeling stuck at 20 HP forever. Option C is the balanced fix.

All three are 2-4 line changes. Tell me which (or none) and I'll apply it. Until then, the player-first-in-round-1 fix from `c6fc22d` is the only mitigation in place — sufficient for level 1-2 day encounters, NOT sufficient for level 3+ night encounters.

## Aside: the actual "no turn" path

Pre-`c6fc22d`, the loss-without-a-turn path was:
1. Player rolls low initiative (d20+1 with Dex 12)
2. All enemies act in initiative order before the player
3. Combined damage exceeds player HP
4. `BattleManager.CheckBattleEnd` flips `BattleOver=true`
5. `Update` skips `StartTurn` for any subsequent combatant including the player
6. Player never gets `CurrentTurn` set to themselves

The promote-to-front fix in `c6fc22d` makes the player the FIRST entry in the `Combatants` list every battle, so `_turnIndex=0` always points at the player. They get exactly one turn before any enemy acts. That's enough to address the literal "no turn" complaint but not enough to actually win unbalanced fights.
