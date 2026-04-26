> ⚠️ **HISTORICAL — Q-learning superseded 2026-04-16, scope-correction revoked 2026-04-26.** This document references Q-learning (`QLearner.cs`, `QTableStore.cs`, `SelfPlayTrainer.cs`, `CombatBrain.cs`) as part of its design. Those files were deleted on 2026-04-16; combat AI is now deterministic `AIBehavior` (behavior tree). See `~/.claude/projects/C--Dev/game_dev_tracker/golden_standard.md` (Combat section) and pivot `q-learning-scope-correction-revoked` for the canonical state. Treat this file as historical only.

# Seamless Per-Enemy Battle Zones — Design Spec

**Date:** 2026-04-13
**Scope:** Replace scene-based battle arenas with in-world per-enemy battle zones. Each enemy NPC has an 8x8 grid zone that tracks them. Combat is turn-based within zones, free movement outside. Player can escape via perception check.

---

## 1. Core Concept

Every enemy NPC in the world has an invisible 8x8 battle zone centered on them. When the player enters any enemy's zone, that zone activates — grid overlay appears, turn-based combat begins. Multiple zones can be active simultaneously (overlapping fights). Zones re-center on their owner enemy every turn. Player can escape by moving to the zone edge and beating a perception check.

---

## 2. BattleZone

**New file:** `Assets/Scripts/Demo/Battle/BattleZone.cs`

One instance per active enemy. MonoBehaviour on a runtime GameObject.

### Fields

- `BattleCombatant OwnerEnemy` — the enemy combatant this zone tracks
- `Vector3 Origin` — world position, re-centers on owner every turn
- `BattleGrid Grid` — 8x8 walkable array, re-scanned on re-center
- `LineRenderer Boundary` — perimeter glow visual
- `float CellSize = 1f`

### Methods

- `Activate(Vector3 enemyPos)` — create 8x8 grid, scan geometry via Physics.OverlapBox, show boundary LineRenderer
- `ReCenter(Vector3 newPos)` — shift origin to new position, re-scan walkability for all 64 cells, reposition boundary visual
- `Deactivate()` — destroy boundary visual and GameObject instantly
- `GridToWorld(int x, int y)` — returns world position offset by current origin
- `WorldToGrid(Vector3 pos)` — returns (int x, int y) relative to current origin
- `ContainsWorldPos(Vector3 pos)` — true if position falls within the 8x8 world-space area

### Geometry Scanning

On every `ReCenter()`:
- For each of the 64 cells, `Physics.OverlapBox()` at cell center with half-extents (0.45, 0.5, 0.45)
- Filter out colliders tagged "Player" or belonging to combatant GameObjects
- Cell with remaining collider(s) → non-walkable in `Grid.Walkable[]`
- Combatant positions → walkable but occupied (handled by BattleManager movement logic)

### Visual Boundary

- `LineRenderer` with 5 points (closed rectangle around 8x8 perimeter)
- Material: emissive white-blue (0.7, 0.85, 1.0), slight glow
- Width: 0.05 units
- Repositions when zone re-centers
- Each zone has its own boundary (multiple zones = multiple visible rectangles)
- Optional: faint ground-projected grid lines within zone (deferred — boundary is MVP)

---

## 3. BattleManager Rewrite

**Modified file:** `Assets/Scripts/Demo/Battle/BattleManager.cs`

### What Stays

- Turn-based logic: initiative, turn order, round tracking
- `BattleCombatant` data model and all combat resolution (attacks, spells, death saves, conditions)
- AI brains (CombatBrain, CombatIntelligence)
- Input handling (WASD grid move, F attack, Q spells, H potion, Space end turn)
- DamagePopup, HitFlash visual effects

### What Changes

**Multi-zone management:**
- `List<BattleZone> ActiveZones` — one per engaged enemy
- Turn order includes all combatants across all active zones
- `StartBattle()` no longer called from `Start()` on scene load — called by `GameManager.StartSeamlessBattle()`

**New entry point:**
```
public void StartSeamlessBattle(List<BattleZone> zones, List<BattleCombatant> enemies, BattleCombatant player)
```
- Receives pre-created zones and combatant data
- Spawns enemy models at zone grid positions via ModelRegistry (capsule fallback)
- Player model already exists in the world — just adds to combatant list
- Starts initiative and turn order

**Per-turn zone tracking:**
- On each enemy's turn, before they act: `zone.ReCenter(enemyModel.transform.position)`
- After enemy moves: `zone.ReCenter(newPosition)` again

**Combatant positioning:**
- Models move in world space via the zone's `GridToWorld()`
- Each combatant knows which zone it belongs to
- Player can be in multiple zones simultaneously

**Battle end:**
- When all enemies in a zone are dead: `zone.Deactivate()`, remove from `ActiveZones`
- When `ActiveZones` is empty: battle fully over, call `GameManager.OnBattleComplete()`

### What's Removed

- `Start()` auto-initialization from scene load
- `FindBattleTemplate()`, `BattleSceneTemplate` dependency
- `BattleRenderer3D` instantiation
- `SceneManager.LoadScene("BattleMap")` references
- `RequestEnemySprites()` coroutine (sprites were for 2D, models are already in world)

---

## 4. Escape Mechanic

### Flow

1. Player uses movement turn to step toward the edge of a specific enemy's zone
2. After player moves, BattleManager checks: did player exit any zone?
3. For each zone the player exited:
   - Enemy rolls D20 perception check
   - DC = 10 + player's DEX modifier (DEX from CharacterSheet or PlayerData)
   - **Enemy rolls >= DC (success):** Player is pulled back to the last valid cell inside the zone. Turn consumed. Log: "[Enemy] spots you trying to flee!"
   - **Enemy rolls < DC (fail):** Zone deactivates. Enemy drops from turn order. Enemy resumes patrol/chase AI in free movement. Log: "You slip away from [Enemy]!"
4. If player is outside ALL active zones: combat fully ends. `GameManager.OnBattleComplete()`.

### Perception Roll

```
int roll = Random.Range(1, 21); // D20
int dc = 10 + playerDexModifier;
bool spotted = roll >= dc;
```

Player DEX modifier: `(CharacterSheet.Dex - 10) / 2` or `(PlayerData.Dex - 10) / 2`.

---

## 5. Dynamic Enemy Joining

### World Keeps Running

During combat, `Update()` continues for all non-combatant entities:
- Ambient enemies patrol
- Day/night cycles
- Other NPCs move

### Join Mechanic

- Each frame during combat, `BattleManager` checks all non-combatant `DungeonNPC` with `Role == AmbientEnemy`
- If any enemy's natural 8x8 zone would contain the player's position AND that enemy is not already in combat:
  - Create new `BattleZone` for that enemy
  - Add enemy to turn order (rolls initiative, inserts at correct position)
  - Log: "[Enemy] joins the fight!"
- Check runs once per round (not per frame) to avoid spam — at the start of each new round

---

## 6. Camera

### Combat Start

- `PerspectiveCameraController` changes follow target to the center of all active zones' bounding box
- Zoom out to show the full extent of active zones + 2 cell margin
- Orbit angle snaps to ~60 degrees for tactical overhead view

### During Combat

- Camera follows active turn combatant (existing behavior)
- When zones move (re-center), camera adjusts framing if needed

### Combat End

- Camera restores previous follow target (player) and zoom distance
- Snap immediately (matching the instant grid dissolve)

---

## 7. Loot

**New file:** `Assets/Scripts/Demo/Battle/WorldLoot.cs`

MonoBehaviour spawned at each defeated enemy's last world position when ALL combat ends.

### WorldLoot

- `int GoldAmount`
- `int XPAmount`
- Spawns a small floating cube (gold color) at enemy death position + 0.5 units up
- Bobs up and down with sine wave (visual indicator)
- Player walks within 1.5 units → auto-collect: add gold/XP to player, destroy loot object
- Auto-despawn after 60 seconds if not collected
- Collected loot shows floating text popup (reuse DamagePopup pattern with gold color)

---

## 8. Encounter Trigger Rewiring

### GameManager Changes

**New methods:**
```
StartSeamlessBattle(Vector3 position, string encounterId)
OnBattleComplete(bool playerWon)
```

**New property:**
```
bool IsInCombat
```

`StartSeamlessBattle`:
1. Resolve `EncounterData.Get(encounterId)`
2. Create one `BattleZone` per enemy in encounter data, centered on `position` (offset each slightly so they don't stack)
3. Create or get `BattleManager` component
4. Call `BattleManager.StartSeamlessBattle(zones, enemies, player)`
5. Set `IsInCombat = true`

`OnBattleComplete`:
1. Spawn `WorldLoot` at each defeated enemy position
2. Set `IsInCombat = false`
3. In dungeon: check boss defeat via `DungeonExplorer.OnBattleWon()`
4. In overworld: increment `EncountersSinceRest`

**Removed:** `EnterBattle(string encounterId)` with `SceneManager.LoadScene`

### DungeonExplorer Changes

- `EnterBattle()`: no scene load, no state save. Calls `GameManager.StartSeamlessBattle(position, encounterId)`
- `HandleMovement()`: early return if `GameManager.Instance?.IsInCombat == true`
- `OnBattleWon()`: still handles boss defeat → `CompleteDungeon()`, unchanged logic

### OverworldManager Changes

- Encounter trigger: `GameManager.StartSeamlessBattle(playerPosition, encounterId)` instead of `GameManager.EnterBattle()`
- Movement suppressed via `IsInCombat` check

### EncounterZone Changes

- `OnTriggerEnter`: calls `GameManager.StartSeamlessBattle(transform.position, EncounterId)` instead of `explorer.EnterBattle()`

---

## 9. Removed/Gutted Files

| File | Action | Reason |
|------|--------|--------|
| `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs` | Gut | Arena geometry (walls, pillars, rocks) no longer needed. Keep DamagePopup and HitFlash classes (move to own files or keep in gutted file). |
| `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs` | Remove | No arena templates needed. ArenaType enum removed. |
| `Assets/Scenes/BattleMap.unity` | Unused | Scene no longer loaded. Can delete or leave. |

**Keep from BattleRenderer3D:** `DamagePopup` and `HitFlash` classes — move to `Assets/Scripts/Demo/Battle/BattleEffects.cs`.

---

## 10. New Files Summary

| File | Type | Purpose |
|------|------|---------|
| `Assets/Scripts/Demo/Battle/BattleZone.cs` | MonoBehaviour | Per-enemy grid zone, geometry scan, boundary visual |
| `Assets/Scripts/Demo/Battle/WorldLoot.cs` | MonoBehaviour | Loot pickup at enemy death positions |
| `Assets/Scripts/Demo/Battle/BattleEffects.cs` | MonoBehaviours | DamagePopup + HitFlash extracted from BattleRenderer3D |

## 11. Modified Files Summary

| File | Change |
|------|--------|
| `Assets/Scripts/Demo/Battle/BattleManager.cs` | Major rewrite — multi-zone, seamless entry, escape mechanic |
| `Assets/Scripts/Demo/GameManager.cs` | `StartSeamlessBattle()`, `OnBattleComplete()`, `IsInCombat` |
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | No scene load in EnterBattle, IsInCombat movement guard |
| `Assets/Scripts/Demo/Overworld/OverworldManager.cs` | Seamless battle trigger |
| `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` | Call StartSeamlessBattle instead of EnterBattle |

## 12. Dependencies & Order

1. **BattleEffects.cs** — extract DamagePopup + HitFlash (must exist before gutting BattleRenderer3D)
2. **BattleZone.cs** — new zone system (independent)
3. **WorldLoot.cs** — loot pickup (independent)
4. **BattleManager.cs rewrite** — depends on BattleZone, BattleEffects
5. **GameManager rewiring** — depends on BattleManager new API
6. **DungeonExplorer + EncounterZone rewiring** — depends on GameManager
7. **OverworldManager rewiring** — depends on GameManager
8. **Gut BattleRenderer3D + remove BattleSceneTemplate** — after everything else is wired
