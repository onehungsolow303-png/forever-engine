# Dungeon Life — Design Spec

**Date:** 2026-04-13
**Scope:** NPC placement in dungeon rooms (friendly + ambient enemies) and hybrid minimap overlay.

---

## 1. Dungeon NPC Data & Placement

### DungeonNPCConfig (ScriptableObject)

New file: `Assets/Scripts/Demo/Dungeon/DungeonNPCConfig.cs`

```
DungeonNPCConfig : ScriptableObject
├── FriendlyNPCRules[]
│   ├── Role: DungeonNPCRole enum (Merchant, Prisoner, QuestGiver)
│   ├── TierFilter: int[] (which room tiers can spawn this)
│   ├── SpawnChance: float (0-1)
│   ├── MaxPerDungeon: int
│   └── ModelKeys: string[] (ModelRegistry keys to pick from)
├── AmbientEnemyRules[]
│   ├── TierFilter: int[]
│   ├── EnemyNames: string[] (names from EncounterData CR table)
│   ├── CountRange: Vector2Int (min, max per room)
│   └── PatrolRadius: float (half-distance for ping-pong waypoints)
└── DirectorOverrides: bool (if true, sends spawn manifest to Director Hub)
```

### Placement Logic

New file: `Assets/Scripts/Demo/Dungeon/DungeonNPCSpawner.cs`

Static method `DungeonNPCSpawner.SpawnNPCs(DADungeonBuilder builder, DungeonNPCConfig config, int seed)` called from `DungeonSceneSetup` after DA builds and before `DungeonExplorer.InitializeWithDA()`.

Iterates non-corridor, non-entrance, non-boss rooms:

- **Tier 1 rooms:** Friendly NPCs (merchant near entrance, prisoner deeper). `SpawnChance` per rule, `MaxPerDungeon` enforced.
- **Tier 2 rooms:** Ambient enemies (1-2 wandering mobs from `AmbientEnemyRules`).
- **Tier 3 rooms:** Both — enemies plus occasional quest NPC.
- **Boss room:** No pre-placed NPCs (boss spawns via encounter zone).
- **Entrance room:** No NPCs (player spawn area).

Each NPC instantiated via `ModelRegistry.Resolve(modelKey)` with capsule fallback. Gets a `DungeonNPC` MonoBehaviour. Parented to the room's `RoomObject` so culling handles visibility.

### Director Hub Override Hook

After procedural spawn, fires `DirectorEvents.Send("dungeon_npcs_placed", spawnManifest)` with room indices and NPC roles. Fire-and-forget — no blocking call during dungeon gen. Director Hub can use this for narrative hooks in future.

---

## 2. NPC Interaction System

### DungeonNPC MonoBehaviour

New file: `Assets/Scripts/Demo/Dungeon/DungeonNPC.cs`

```
DungeonNPC : MonoBehaviour
├── Role: DungeonNPCRole (Merchant, Prisoner, QuestGiver, AmbientEnemy)
├── NPCName: string
├── RoomIndex: int
├── InteractionRadius: float = 2.0f
├── HasInteracted: bool
└── Patrol state (enemies only):
    ├── WaypointA, WaypointB: Vector3
    ├── CurrentTarget: int (0 or 1)
    └── PatrolSpeed: float = 1.5f
```

### Interaction Flow

- `Update()` checks distance to `DungeonExplorer.Instance._playerTransform` every frame
- Within `InteractionRadius`: show floating prompt via `TextMesh` ("[E] Trade", "[E] Talk", "[E] Rescue")
- On `KeyCode.E` press, dispatch by role:
  - **Merchant:** Opens simple trade stub (buy health potions). Full implementation in Batch D with loot system.
  - **Prisoner:** One-shot rescue — scale to zero over 0.5s, grant XP reward (50 XP), set `HasInteracted = true`.
  - **QuestGiver:** Route to Director Hub via `DirectorEvents.SendDialogue()` with context: `locationId`, `roomIndex`, NPC persona from config.
- Prompt hidden when player leaves radius or after interaction.

### Ambient Enemy Behavior

- `Update()` ping-pongs between two waypoints: room center ± `PatrolRadius` on a random axis (X or Z, chosen at spawn)
- Movement: `transform.position = Vector3.MoveTowards(current, target, PatrolSpeed * Time.deltaTime)`
- Faces movement direction: `Quaternion.LookRotation(moveDir)`
- No Rigidbody, no NavMesh — simple transform movement
- When the encounter zone fires (player enters combat trigger), all `DungeonNPC` instances with `Role == AmbientEnemy` in that room index are destroyed (the encounter system spawns its own BattleCombatants)

### DungeonNPCRole Enum

```csharp
public enum DungeonNPCRole { Merchant, Prisoner, QuestGiver, AmbientEnemy }
```

Defined in `DungeonNPC.cs` (same file, namespace scope).

---

## 3. Minimap

### DungeonMinimap MonoBehaviour

New file: `Assets/Scripts/Demo/Dungeon/DungeonMinimap.cs`

Created by `DungeonExplorer.InitializeWithDA()` after setup, passing `DADungeonBuilder` reference.

### Data Sources

- `DADungeonBuilder.Rooms[]` — positions, bounds, types, tiers
- `DADungeonBuilder.RoomGraph` — adjacency edges for corridors/connections
- `DungeonState.VisitedRooms` — explored vs unknown
- `DungeonExplorer.Instance._playerTransform` — player position
- `FindObjectsByType<DungeonNPC>()` — NPC/enemy marker positions (cached, refreshed on room change)

### Coordinate Mapping

On init, compute axis-aligned bounding box of all room `WorldBounds.center` XZ coordinates. `WorldToMinimap(Vector3 worldPos, Rect minimapRect)` does linear remap from world AABB to minimap pixel rect. Computed once (rooms don't move).

### Corner Minimap (Always-On)

- Rendered via IMGUI `OnGUI()` — consistent with OverworldHUD/BattleHUD pattern
- Position: top-right corner, 200x200 px
- Background: semi-transparent black (alpha 0.4)
- **Explored rooms:** Filled rectangles proportional to actual `WorldBounds` size, color-coded:
  - Tier 1: green tint
  - Tier 2: yellow tint
  - Tier 3: red tint
  - Corridor: gray, thinner
- **Unexplored adjacent rooms** (1-hop from any visited room via RoomGraph): small gray circle with "?"
- **Boss room:** Red outline always shown (even if unexplored) as goal indicator
- **Entrance:** Green border
- **Edges:** Thin lines between connected room centers (from RoomGraph)
- **Player:** Yellow dot, updates every frame
- **Friendly NPCs:** Blue dots (only in explored rooms)
- **Ambient enemies:** Red dots (only in explored rooms)

### Full Overlay (Tab Toggle)

- Same rendering logic, scaled to 60% of screen, centered
- Dim background: full-screen black rect (alpha 0.6) behind the map
- Larger room rectangles with room name labels and tier numbers
- Toggle: `Input.GetKeyDown(KeyCode.Tab)` in `DungeonExplorer.Update()`
- While overlay is open, movement input suppressed (guard in `HandleMovement`: `if (_minimapFullOpen) return;`)
- `DungeonMinimap.IsFullOpen` property read by `DungeonExplorer`

### Suppression

- Minimap hidden during battle transition (DungeonExplorer destroys or the scene unloads)
- Corner minimap suppressed when DialoguePanel is open (check `DialoguePanel.Instance?.IsOpen`)

---

## 4. Integration Points

### DungeonSceneSetup Changes

Current flow:
1. Instantiate DA prefab → `dungeon.Build()` → `OnPostDungeonBuild` fires
2. Create DungeonExplorer → `InitializeWithDA()`

New flow:
1. Instantiate DA prefab → `dungeon.Build()` → `OnPostDungeonBuild` fires
2. **`DungeonNPCSpawner.SpawnNPCs(builder, config, seed)`** — place NPCs in rooms
3. Create DungeonExplorer → `InitializeWithDA()`
4. **DungeonExplorer creates `DungeonMinimap`** and passes builder reference

### EncounterZone Changes

When encounter fires, before loading battle scene:
- Find all `DungeonNPC` in the triggering room with `Role == AmbientEnemy`
- Destroy them (combat system creates its own combatants from EncounterData)

### DungeonState Changes

No changes needed — `VisitedRooms` HashSet already tracks exploration. Minimap reads it directly.

---

## 5. New Files Summary

| File | Type | Purpose |
|------|------|---------|
| `Assets/Scripts/Demo/Dungeon/DungeonNPCConfig.cs` | ScriptableObject | NPC spawn rules data |
| `Assets/Scripts/Demo/Dungeon/DungeonNPCSpawner.cs` | Static class | Placement logic |
| `Assets/Scripts/Demo/Dungeon/DungeonNPC.cs` | MonoBehaviour | NPC runtime behavior + DungeonNPCRole enum |
| `Assets/Scripts/Demo/Dungeon/DungeonMinimap.cs` | MonoBehaviour | IMGUI minimap rendering |

## 6. Modified Files Summary

| File | Change |
|------|--------|
| `Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs` | Call NPC spawner after DA build |
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | Create minimap, Tab toggle, movement suppression when overlay open |
| `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` | Destroy ambient enemies in room when encounter fires |

## 7. Dependencies & Order

1. **DungeonNPCRole enum + DungeonNPC** — defines the types, must exist first
2. **DungeonNPCConfig** — depends on enum
3. **DungeonNPCSpawner** — depends on config + DungeonNPC
4. **DungeonSceneSetup integration** — depends on spawner
5. **DungeonMinimap** — independent of NPCs but needs DungeonExplorer changes
6. **EncounterZone cleanup** — depends on DungeonNPC existing

Tasks 1-4 (NPC system) and Task 5 (minimap) are independent and can run in parallel. Task 6 ties them together.
