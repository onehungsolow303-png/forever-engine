# Dungeon Architect Snap Builder Integration

**Date:** 2026-04-13
**Scope:** Replace custom DungeonAssembler with Dungeon Architect's Snap builder using Lordenfel room prefabs
**Status:** Approved

---

## Problem

The current `DungeonAssembler` generates simple snake-path layouts with primitive cube fallback rooms. Dungeon Architect 1.22.0 is now installed alongside Lordenfel's pre-configured Snap builder preset (`FPD_DungeonSnap_01.prefab`) with 32 room modules (corridors, stairs, Tier 1-3 rooms, boss room). We should use DA's proven procedural generation instead of our custom layout.

## Design

### DADungeonBuilder

New `DungeonEventListener` subclass: `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs`

**Responsibilities:**
1. Instantiate `FPD_DungeonSnap_01` prefab from Resources at runtime
2. Set seed from GameManager
3. Call `dungeon.Build()`
4. In `OnPostDungeonBuild()`, iterate `SnapQuery.modules` to:
   - Identify room types by spawned GameObject name (Corridor/Stair → skip, Room_Tier* → encounter, Boss → boss encounter)
   - Place `EncounterZone` triggers in non-corridor rooms using `moduleInfo.instanceInfo.WorldBounds`
   - Add per-room Point Lights for fog of war
   - Identify entrance (first module) and boss room
5. Expose room data for DungeonExplorer (entrance position, room list, lights)

**Room type detection** from spawned module name:
- Contains `"Corridor"` or `"Stair"` → passage, no encounter
- Contains `"Boss"` → boss encounter zone
- Contains `"Room"` → regular encounter zone (Tier1/2/3 determines enemy CR)

### DungeonExplorer Changes

- Remove `DungeonAssembler` dependency, use `DADungeonBuilder` instead
- Use `SnapQuery.GetModuleInfo(playerPos, out info)` for fog of war room tracking
- Player spawns at entrance module center position
- Fog of war: toggle per-room lights based on which module player is in

### DungeonSceneSetup Changes

- Instead of creating a `DungeonAssembler`, load and instantiate `FPD_DungeonSnap_01` prefab
- Add `DADungeonBuilder` component to the dungeon GameObject
- Add `SnapQuery` component (if not already on prefab)
- Call `dungeon.Build()` after setup

### Resource Setup

The `FPD_DungeonSnap_01.prefab` must be accessible at runtime. Copy or reference it from `Resources/` so `Resources.Load` can find it. Alternatively, assign it as a serialized field on `DungeonSceneSetup`.

## Files

### New
| File | Purpose |
|------|---------|
| `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs` | DungeonEventListener that sets up encounters + fog after DA build |

### Modified
| File | Change |
|------|--------|
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | Use DADungeonBuilder + SnapQuery instead of DungeonAssembler |
| `Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs` | Instantiate DA prefab instead of DungeonAssembler |

### Removed
| File | Reason |
|------|--------|
| `Assets/Scripts/Demo/Dungeon/DungeonAssembler.cs` | Replaced by DA Snap builder |
| `Assets/Scripts/Demo/Dungeon/RoomCatalog.cs` | DA manages its own module catalog via SnapConfig |

## Unchanged
- `DungeonState.cs` — still tracks visited rooms, triggered encounters, boss state
- `EncounterZone.cs` — still used for battle triggers, placed by DADungeonBuilder
- `GameManager.cs` — EnterDungeon/ReturnToOverworld flow unchanged
- `LocationInteriorManager.cs` — routing unchanged
