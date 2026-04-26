> ⚠️ **HISTORICAL — Q-learning superseded 2026-04-16, scope-correction revoked 2026-04-26.** This document references Q-learning (`QLearner.cs`, `QTableStore.cs`, `SelfPlayTrainer.cs`, `CombatBrain.cs`) as part of its design. Those files were deleted on 2026-04-16; combat AI is now deterministic `AIBehavior` (behavior tree). See `~/.claude/projects/C--Dev/game_dev_tracker/golden_standard.md` (Combat section) and pivot `q-learning-scope-correction-revoked` for the canonical state. Treat this file as historical only.

# 3D Engine Continuation Design

**Date:** 2026-04-12
**Scope:** GLB model wiring, battle AI polish, 3D dungeon interiors
**Status:** Approved

---

## Overview

Three phases of work to advance the Forever Engine 3D transition:

1. **GLB Model Wiring** — Replace capsule tokens with imported 3D character models
2. **Battle AI Polish** — Tune CombatIntelligence, externalize config, diversify encounters
3. **3D Dungeon Interiors** — Make dungeons explorable with prefab room assembly

Phases execute in order. Each phase is independently shippable.

---

## Phase 1: GLB Model Wiring

### Problem

`BattleRenderer3D` already supports loading models via `Resources.Load<GameObject>($"Models/{ModelId}")`, but `EnemyDef.ModelId` is never populated. All 80+ GLB models sit unused while every combatant renders as a colored capsule.

### Design

#### ModelRegistry

New static class: `Assets/Scripts/Demo/Battle/ModelRegistry.cs`

```csharp
public static class ModelRegistry
{
    // Maps enemy name → array of resource paths (for random variety)
    // Optional per-entry scale override defaults to 1.0
    public static (string path, float scale) Resolve(string enemyName);
}
```

Dictionary maps every enemy name used in `EncounterData` biome pools to one or more GLB resource paths under `Resources/Models/`. When multiple paths exist, one is selected at random for visual variety.

Example mappings:

| Enemy Name | GLB Path(s) | Scale |
|---|---|---|
| Wolf | `Monsters/Giant Rat` | 0.8 |
| Dire Wolf | `Monsters/Giant Rat` | 1.2 |
| Bandit | `NPCs/Human Female Bandit`, `NPCs/Human male fighter` | 1.0 |
| Bandit Captain | `NPCs/Human male fighter` | 1.1 |
| Skeleton | `Monsters/Skeleton Fighter` | 1.0 |
| Goblin | `Monsters/Goblin female fighter`, `Monsters/Goblin male fighter` | 0.9 |
| Goblin King | `Monsters/Goblin King` | 1.1 |
| Mutant | `Monsters/Mummy` | 1.0 |
| Plague Rat | `Monsters/Giant Rat` | 0.6 |
| Cultist | `NPCs/Githyanki female fighter` | 1.0 |

Full mapping will be built by auditing all enemy names in `EncounterData` biome pools against available GLBs in `Resources/Models/Monsters/` and `Resources/Models/NPCs/`.

#### Wiring Points

1. **`EncounterData.MakeCREnemyDef()`** — After creating the `EnemyDef`, call `ModelRegistry.Resolve(def.Name)` to populate `def.ModelId` and store scale.
2. **`BattleCombatant.FromEnemy()`** — Already copies `ModelId` from `EnemyDef`. Add `ModelScale` field.
3. **`BattleRenderer3D`** — Already loads via `Resources.Load`. After instantiation, apply `combatant.ModelScale` to the model's transform.
4. **Player model** — Add `ModelId` field to `CharacterSheet`. Populate during character creation based on race/class combo (e.g. `"NPCs/Dwarf male fighter"`). Wire through `BattleCombatant.FromPlayer()`.

#### Scale Normalization

GLB models arrive at varying scales. The `ModelRegistry` scale override is applied as a multiplier on the model's localScale after instantiation. The grid cell size is 1.0 unit, so models should fit within roughly 0.8-1.2 units width.

#### Capsule Fallback

The existing capsule fallback in `BattleRenderer3D` is preserved. If `Resources.Load` returns null (unmapped enemy, missing file), the capsule renders as before. This ensures no regression.

---

## Phase 2: Battle AI Polish

### Problem

The Q-learning AI works but feels samey. Rewards are hard-coded, hyperparameters are buried in source, `DynamicDifficulty.EnemyDamageMult` is defined but never applied, and every biome spawns homogeneous groups.

### Design

#### A) Tune & Externalize Config

**Move to GameConfig ScriptableObject:**

| Parameter | Current Location | Current Value |
|---|---|---|
| Q-Learning Rate | `CombatBrain` line 19 | 0.15 |
| Q-Discount Factor | `CombatBrain` line 19 | 0.85 |
| Q-Exploration Rate | `CombatBrain` line 20 | 0.25 |
| Advance+Hit Reward | `BattleManager` line ~729 | +0.1 |
| Attack Adjacent Reward | `BattleManager` line ~735 | +0.3 |
| Retreat Low HP Reward | `BattleManager` line ~747 | +0.2 |
| Hold Guard Reward | `BattleManager` line ~756 | +0.1 |
| Hold Non-Guard Penalty | `BattleManager` line ~756 | -0.05 |
| Day XP Budget Mult | `EncounterData` line 65 | 40 |
| Night XP Budget Mult | `EncounterData` line 65 | 60 |
| Max Enemies Per Encounter | `EncounterData` various | 4 |

**Apply EnemyDamageMult:**
In `BattleManager.ResolveAttack()`, multiply final damage by `DynamicDifficulty.Instance.EnemyDamageMult` when the attacker is an enemy. This completes the adaptive difficulty loop (HP scaling already applied in `EncounterManager.ScaleEncounter()`).

**Reward rebalancing targets:**
- Increase kill reward (currently implicit via episode end) — add explicit +0.5 for reducing target to 0 HP
- Add damage-taken penalty: -0.1 per hit received (encourages tactical positioning)
- Scale rewards by difficulty level so harder fights train faster

#### C) Encounter Variety

**Encounter Templates:**
New data structure in `EncounterData` — predefined group compositions:

```csharp
public class EncounterTemplate
{
    public string Name;           // "Goblin Raiding Party"
    public int MinCR, MaxCR;      // CR budget range this template fits
    public EnemySlot[] Slots;     // Specific enemy definitions
}

public class EnemySlot
{
    public string Name;
    public string Behavior;       // "chase", "guard"
    public int CRCost;            // XP cost of this slot
}
```

**Template examples:**

| Template | Composition | Total XP |
|---|---|---|
| Goblin Raiding Party | 1 Goblin King (guard, 100 XP) + 2 Goblins (chase, 50 XP each) | 200 |
| Undead Patrol | 2 Skeletons (guard, 50 XP) + 1 Mummy (chase, 200 XP) | 300 |
| Bandit Ambush | 1 Bandit Captain (guard, 100 XP) + 3 Bandits (chase, 25 XP each) | 175 |
| Wolf Pack | 1 Alpha Wolf (guard, 100 XP) + 3 Wolves (chase, 25 XP each) | 175 |
| Cultist Cell | 2 Cultists (chase, 50 XP each) + 1 Skeleton (guard, 50 XP) | 150 |

**Selection logic:**
`EncounterData.GenerateRandom()` first checks if a template fits the XP budget. If yes, 60% chance to use a template (variety), 40% chance to use the existing random pool (unpredictability). If no template fits, fall back to random pool entirely.

**Biome pool expansion:**
Add entries for available GLB monsters not yet in pools:
- Dungeon biome: Mummy, Skeleton Fighter, Lizard Folk Archer
- Forest additions: Orc male fighter
- New "crypt" biome pool for Eternal Temple encounters

**ModelId wiring:**
Templates include enemy names that `ModelRegistry` already resolves. No separate model field needed on templates.

#### Future Expansion (B) — Documented for Later

State space expansion candidates (not implemented now):
- `has_ranged_attack` flag (2 bins) → 324 states
- `ally_hp_critical` flag (2 bins) → 648 states
- `terrain_advantage` (adjacent to wall/obstacle, 2 bins) → 1296 states
- New actions: `UseAbility`, `ProtectAlly`, `FocusFire`
- Outcome-weighted rewards: +1.0 for kill, proportional damage penalty

This expansion should coincide with neural model training (Sentis ONNX) where the larger state space adds real value.

---

## Phase 3: 3D Dungeon Interiors

### Problem

`LocationInteriorManager` bypasses dungeon exploration entirely, falling back to a battle encounter. Three 3D dungeon asset packs are purchased but unused. The camera and rendering infrastructure is production-ready.

### Design

#### RoomCatalog

New ScriptableObject: `Assets/ScriptableObjects/RoomCatalog.asset`

```csharp
[CreateAssetMenu(menuName = "Forever Engine/Room Catalog")]
public class RoomCatalog : ScriptableObject
{
    public RoomEntry[] Rooms;
}

public enum RoomTag { Entrance, Corridor, Chamber, DeadEnd, Boss, Treasure }

[Serializable]
public class RoomEntry
{
    public string Id;
    public RoomTag Tag;
    public GameObject Prefab;          // Reference to asset pack prefab
    public Vector2Int Dimensions;      // Size in tile units
    public DoorPosition[] Doors;       // N/S/E/W connection points
    public string LightingPreset;      // "torch", "dark", "boss_glow"
    public string Pack;                // Source pack for attribution
}

[Serializable]
public class DoorPosition
{
    public Direction Side;             // N, S, E, W
    public int Offset;                 // Position along that wall
}
```

**Initial catalog:** 10-15 rooms hand-picked from:
- **Multistory Dungeons 2** — modular rooms designed for runtime assembly
- **Lordenfel** — `Prefabs/Architecture/CompleteRooms/FirstPersonDungeon/` rooms
- **Eternal Temple** — decorative chambers for boss encounters

#### DungeonAssembler

New class: `Assets/Scripts/Demo/Dungeon/DungeonAssembler.cs`

**Input:** Room graph from `PipelineCoordinator` — list of rooms with types and connections.

**Process:**
1. For each room node, select a prefab from `RoomCatalog` matching the tag
2. Instantiate at world position based on graph layout (grid-aligned)
3. Align door positions between connected rooms
4. Spawn short corridor segments for connections that don't align directly
5. Apply lighting preset per room (directional light color/intensity, point lights)
6. Place `EncounterZone` trigger colliders in chamber/boss rooms
7. Place loot interactables in treasure rooms

**Output:** A populated scene root `GameObject` with all rooms as children.

#### DungeonExplorer

New MonoBehaviour: `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`

Replaces the battle fallback in `LocationInteriorManager.EnterLocation()`.

**Responsibilities:**
- Load `DungeonExploration` scene
- Call `DungeonAssembler.Build()` with layout from `PipelineCoordinator`
- Spawn player model at entrance room
- Attach `PerspectiveCameraController` to follow player
- WASD movement (reuse movement code from `OverworldManager`, lines 124-146)
- Track visited rooms for fog of war state
- Handle scene transitions to/from battle

#### Fog of War

Shader-based darkness with reveal radius:
- All rooms start dark (black ambient, no lights active)
- When player enters a room, activate that room's lighting preset
- Previously visited rooms stay lit at 50% intensity
- Unvisited rooms remain dark
- Implementation: per-room `Light` components toggled by room visit state

This avoids complex shader work — just room-level light toggling.

#### EncounterZone

New MonoBehaviour: `Assets/Scripts/Demo/Dungeon/EncounterZone.cs`

```csharp
public class EncounterZone : MonoBehaviour
{
    public string EncounterId;         // Links to EncounterData
    public bool Triggered;             // One-shot

    void OnTriggerEnter(Collider other)
    {
        if (Triggered || !other.CompareTag("Player")) return;
        Triggered = true;
        DungeonExplorer.Instance.EnterBattle(EncounterId, transform.position);
    }
}
```

#### Battle → Dungeon Return Flow

When a battle ends in the dungeon context:
1. `BattleManager` fires victory/defeat event
2. `GameManager` checks if battle was dungeon-sourced (stored context)
3. On victory: reload `DungeonExploration` scene, restore player position + room visit state
4. On defeat: return to overworld (same as current behavior)

State persistence across battle transitions:
- `DungeonState` data class holds: visited rooms, triggered encounters, player grid position
- Serialized to `GameManager` static field before battle scene load
- Restored on return to dungeon scene

#### Boss Room & Dungeon Completion

- Boss room has a special `EncounterZone` with `isBoss = true`
- After boss battle victory, dungeon is marked complete
- Return to overworld with loot/XP summary
- Location marked as cleared in `LocationData` (prevents re-entry or enables different content)

#### Scene Flow

```
Overworld3D
  → Player enters dungeon location (Press Return)
  → LocationInteriorManager.EnterLocation()
  → if location.IsDungeon:
      → Load DungeonExploration scene
      → PipelineCoordinator generates layout
      → DungeonAssembler builds rooms from RoomCatalog
      → DungeonExplorer enables player movement
      → Player explores (WASD + camera orbit)
      → EncounterZone triggered:
          → Save DungeonState
          → Load BattleMap scene (existing flow)
          → Battle plays out
          → Victory: reload DungeonExploration, restore state
          → Defeat: return to Overworld3D
      → Boss defeated:
          → Return to Overworld3D
          → Location marked cleared
```

---

## File Summary

### New Files

| File | Phase | Purpose |
|---|---|---|
| `Scripts/Demo/Battle/ModelRegistry.cs` | 1 | Enemy name → GLB path lookup |
| `Scripts/Demo/Dungeon/DungeonAssembler.cs` | 3 | Runtime room prefab assembly |
| `Scripts/Demo/Dungeon/DungeonExplorer.cs` | 3 | Dungeon exploration controller |
| `Scripts/Demo/Dungeon/EncounterZone.cs` | 3 | Battle trigger colliders |
| `Scripts/Demo/Dungeon/DungeonState.cs` | 3 | Serializable dungeon progress |
| `Scripts/Demo/Dungeon/RoomCatalog.cs` | 3 | SO for tagged room prefab entries |
| `Scenes/DungeonExploration.unity` | 3 | Dungeon exploration scene |

### Modified Files

| File | Phase | Change |
|---|---|---|
| `Scripts/Demo/Encounters/EncounterData.cs` | 1, 2 | Populate ModelId; add EncounterTemplate selection |
| `Scripts/Demo/Battle/BattleCombatant.cs` | 1 | Add ModelScale field |
| `Scripts/Demo/Battle/BattleRenderer3D.cs` | 1 | Apply ModelScale after instantiation |
| `Scripts/Demo/Battle/BattleManager.cs` | 2 | Externalize reward values; apply EnemyDamageMult |
| `Scripts/Demo/Battle/CombatBrain.cs` | 2 | Read hyperparameters from GameConfig |
| `Scripts/ScriptableObjects/GameConfig.cs` | 2 | Add AI tuning + encounter config fields |
| `Scripts/Demo/Locations/LocationInteriorManager.cs` | 3 | Route dungeons to DungeonExplorer |
| `Scripts/Demo/GameManager.cs` | 3 | DungeonState persistence across scene loads |
| `CharacterSheet` (wherever defined) | 1 | Add ModelId field for player model |

---

## Dependencies & Risks

- **GLB import quality** — Some models may need material fixes for URP. Test each model in-scene after wiring.
- **Asset pack prefab compatibility** — Room prefabs from different packs may use different material/lighting setups. Standardize on URP/Lit materials.
- **PipelineCoordinator reliability** — The map generator hasn't been exercised since the dungeon bypass. May need fixes.
- **Scene load times** — Instantiating many room prefabs at once could spike. Consider async loading or a loading screen.
- **Collider setup** — Room prefabs may not have trigger colliders; `EncounterZone` placement may need manual door/chamber detection.
