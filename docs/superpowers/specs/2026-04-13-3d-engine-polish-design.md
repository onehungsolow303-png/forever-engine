# 3D Engine Polish — Design Spec

**Date:** 2026-04-13
**Scope:** Four polish items for the 3D Forever Engine: dungeon sprint, battle arena walls, tiered encounters, DA visibility graph.

---

## 1. Dungeon Sprint

**File:** `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`

Add a `SprintMultiplier = 1.8f` constant alongside existing `MoveSpeed = 6f`. In `HandleMovement()`, check `Input.GetKey(KeyCode.LeftShift)` — if held, multiply the final velocity by the sprint multiplier. No stamina, no UI, no new state. Sprint is a scalar on the existing `moveDir * speed` calculation.

**Sprint speed:** 6.0 × 1.8 = 10.8 m/s.

---

## 2. Battle Arena Walls

**File:** `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs`

### Arena Type Enum

Add `ArenaType { Dungeon, Boss, Overworld }` to distinguish combat contexts. `BattleSceneTemplate` gets an `ArenaType` field. Encounter source sets it: dungeon rooms → `Dungeon`, boss rooms → `Boss`, overworld → `Overworld`.

### Grid Sizes (constants, easy to bump later)

| Arena | Grid | Notes |
|-------|------|-------|
| Dungeon | 8×8 | Current default |
| Boss | 12×12 | Current default |
| Overworld | 16×16 | New |

### Dungeon Arena (enclosed room)

- Floor: existing dark gray plane (0.3, 0.3, 0.35)
- 4 box primitive walls: 3 units tall, 0.3 units thick, length matches floor dimensions
- South wall split into two segments with 2-unit centered gap (door feel)
- All walls parented to `_roomInstance` for cleanup
- No wall colliders needed (grid-based movement)

### Boss Arena (imposing room)

- Floor: darker tinted plane (0.25, 0.2, 0.22)
- 4 box walls: 5 units tall, 0.5 units thick
- 4 pillar boxes at inner grid corners for visual weight
- South wall door gap (2.5 units)
- Same parenting and cleanup

### Overworld Arena (open field)

- Floor: earth-tone plane (0.4, 0.5, 0.3), scaled larger than playable grid for open-space feel
- No walls
- 3-5 randomly placed "rock" cubes (scaled, slightly Y-rotated) as terrain features
- Rock positions marked as non-walkable grid cells
- Rocks add tactical cover variety

### Shared

- Existing directional light unchanged
- Combatant spawn logic unchanged
- All geometry is primitive boxes/planes — no asset pack meshes, no GC risk

---

## 3. Tiered Encounters

**Files:** `Assets/Scripts/Demo/Battle/EncounterData.cs`, `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs`, `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`

### Tier Wiring

`DADungeonBuilder` already classifies rooms as Tier 1/2/3 in `RoomInfo.Tier`. Currently unused by the encounter system. Fix: encode tier in the encounter ID when creating `EncounterZone` — e.g. `"random_dungeon_t2_room3"`. Parse tier back in `EncounterData.GenerateRandom()`.

### Budget Scaling

| Tier | Budget Mult | Min CR Floor | Reward Mult |
|------|-------------|-------------|-------------|
| 1 | 0.8× | None (any CR) | 1.0× |
| 2 | 1.2× | Skip CR 1/4 (XP 25) | 1.3× |
| 3 | 1.6× | Skip CR 1/4 + CR 1/2 (XP 25, 50) | 1.6× |

### Composition Shift

After computing the tier-scaled XP budget, the enemy selection loop applies a `minCR` floor per tier. This naturally produces fewer, stronger enemies at higher tiers — Tier 3 can't fill slots with weak CR 1/4 fodder, so it picks fewer CR 2–5 enemies instead.

### Reward Scaling

Gold reward from the CR lookup table is multiplied by the tier reward multiplier. XP stays tied to enemies defeated (harder enemies inherently give more XP via the CR table).

### Unchanged

- Overworld encounters: biome-based generation, no tier concept
- Boss encounters: own dedicated path, unaffected
- Encounter suppression logic: unchanged

---

## 4. DA Visibility Graph

**Files:** `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs`, `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`

### Graph Construction (OnPostDungeonBuild)

After iterating `SnapQuery.modules`, build an adjacency dictionary: `Dictionary<int, List<int>>` mapping room index → connected room indices.

**Primary method:** Iterate DA Snap module connections (snap points link modules). Each connection = an edge in the graph.

**Fallback:** If DA doesn't expose connection data cleanly, use spatial proximity — two rooms are adjacent if their bounds are within 2 units of each other (snap seams are tight).

Corridors are nodes in the graph (they bridge the rooms on either side).

### Room Activation (replaces 60-unit distance cull)

`UpdateFogOfWar()` switches from distance-based to graph-based:

- BFS from player's current room to depth 2
- **Current room (depth 0):** active, full light intensity
- **1-hop rooms (depth 1):** active, 50% light if visited, lights off if unvisited
- **2-hop rooms (depth 2):** geometry active (silhouette through doorways), lights off
- **Beyond depth 2:** `SetActive(false)`

### Player Room Detection

Already implemented — `UpdateFogOfWar()` checks which room bounds contain the player. Used as BFS origin. No change needed.

### Public API for Future Use

```csharp
// On DADungeonBuilder
public IReadOnlyDictionary<int, List<int>> RoomGraph { get; }
```

Enables future consumers:
- **Minimap:** query graph + visited set for connectivity map
- **Pathfinding:** NPC/quest marker navigation between rooms
- **Quest system:** "room N hops from boss" distance queries

### Fallback Safety

If graph construction produces zero edges (DA API issue), log a warning and fall back to the existing 60-unit distance cull.

---

## Dependencies & Order

1. **Sprint** — independent, no dependencies
2. **Arena walls** — independent, no dependencies
3. **Tiered encounters** — depends on understanding existing EncounterData flow but no code dependency on items 1/2
4. **Visibility graph** — depends on understanding DADungeonBuilder but no code dependency on items 1/2/3

All four can be implemented in parallel or any order.
