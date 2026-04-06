# Z-Level Transitions Design

**Date:** 2026-04-05
**Status:** Approved
**Scope:** 2-floor dungeon support with stairs_up/stairs_down transitions

## Problem

Dungeons are single-floor (z=0 only). MapImporter.CreateTransition() is stubbed. No TransitionComponent exists in ECS. MapDataStore overwrites walkability on each z-level load.

## Solution: Swap-in-Place

Store all z-levels' walkability data in MapDataStore. When player steps on stairs, swap the active NativeArray to the target z-level. All existing systems keep reading `MapDataStore.Walkability` — no changes needed to FogOfWarSystem, AIExecutionSystem, or PathfindJob.

## Architecture

```
Generate floor 0 + floor -1 (PipelineCoordinator x2)
→ MapSerializer writes 2 z_levels + transitions
→ MapImporter loads both, stores per-z walkability in MapDataStore
→ Player moves onto stairs tile
→ PlayerMovement detects TransitionComponent at position
→ MapDataStore.SwapToLevel(-1) copies stored data to active arrays
→ TileRenderer.RenderLevel(-1) rerenders tiles
→ FogRenderer reinitializes for new level
→ Player position Z updated
```

## Components

### 1. New: TransitionComponent.cs
`Assets/Scripts/ECS/Components/TransitionComponent.cs`

```csharp
public struct TransitionComponent : IComponentData
{
    public int FromZ;
    public int ToZ;
    public int TransitionType; // 0=stairs_down, 1=stairs_up, 2=ladder, 3=trapdoor, 4=portal
}
```

### 2. Modify: MapDataStore.cs
Add `Dictionary<int, int[]> _rawWalkByZ` to store all z-levels' raw data during import.

- `LoadWalkability(int z, int[] flatData)` — store in dictionary AND set active if z matches CurrentZ
- `SwapToLevel(int z)` — copy stored walkability to active NativeArray, reset FogGrid to unexplored, update CurrentZ

### 3. Fix: MapImporter.CreateTransition()
Add TransitionComponent with FromZ, ToZ, TransitionType parsed from JSON type string.

### 4. Modify: PlayerMovement.cs
After successful move, query all TransitionComponent entities. If player position matches a transition's position AND player Z matches FromZ, trigger level change:
- Call MapDataStore.SwapToLevel(toZ)
- Update player PositionComponent.Z
- Update MapDataSingleton.CurrentZ
- Call TileRenderer.RenderLevel(toZ) and FogRenderer.Initialize()

### 5. Modify: MapSerializer.cs
New overload: `Serialize(GenerationResult floor0, GenerationResult floorMinus1, string outputDir)`
- Write 2 terrain PNGs (terrain_z0.png, terrain_z-1.png)
- Write 2 z_levels in JSON
- Place stairs_down in floor 0's last room, stairs_up at matching position on floor -1
- Spawns from both floors

### 6. Modify: LocationInteriorManager.cs
For dungeon/castle types, call PipelineCoordinator.Generate() twice with offset seeds. Pass both results to the new MapSerializer overload.

## Files Changed

| File | Action |
|---|---|
| `Assets/Scripts/ECS/Components/TransitionComponent.cs` | **New** |
| `Assets/Scripts/ECS/Data/MapDataStore.cs` | **Modify** — per-z storage + SwapToLevel |
| `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs` | **Modify** — implement CreateTransition |
| `Assets/Scripts/MonoBehaviour/Input/PlayerMovement.cs` | **Modify** — transition detection |
| `Assets/Scripts/Generation/MapSerializer.cs` | **Modify** — 2-floor serialization |
| `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs` | **Modify** — generate 2 floors |
