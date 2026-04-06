# Dungeon Interior Pipeline Design

**Date:** 2026-04-05
**Status:** Approved
**Scope:** Wire C# PipelineCoordinator output through MapImporter for dungeon interiors

## Problem

`LocationInteriorManager` currently calls an external Python process (`AssetGeneratorBridge`) to generate dungeon interiors. The Python bridge is incomplete and adds cross-process complexity. Meanwhile, a fully functional C# generation pipeline (`PipelineCoordinator`) already exists in-project but has no serialization step to produce the v1.1.0 JSON that `MapImporter` consumes.

## Solution

Add a `MapSerializer` that converts `PipelineCoordinator.GenerationResult` to schema-compliant v1.1.0 JSON + terrain PNG, then rewire `LocationInteriorManager` to use the C# pipeline instead of the Python bridge.

## Architecture

```
LocationInteriorManager.EnterLocation(loc)
  -> check cache -> miss ->
  PipelineCoordinator.Generate(request)     // existing C# generation
  -> MapSerializer.Serialize(result, dir)   // NEW: write JSON + PNG
  -> GameManager.PendingMapDataPath = path
  -> SceneManager.LoadScene("Game")
  -> GameBootstrap -> MapImporter.Import()  // existing, unchanged
```

## Components

### 1. New: MapSerializer.cs

**Location:** `Assets/Scripts/Generation/MapSerializer.cs`
**Namespace:** `ForeverEngine.Generation`

Converts `PipelineCoordinator.GenerationResult` to disk files matching map_schema v1.1.0.

**Public API:**
```csharp
public static class MapSerializer
{
    /// Returns path to written MapData.json
    public static string Serialize(PipelineCoordinator.GenerationResult result, string outputDir)
}
```

**Serialization mapping:**

| Source (GenerationResult) | Target (map_schema v1.1.0) |
|---|---|
| `Request.Width/Height/MapType/Biome/Seed` | `config` object |
| `Terrain.Walkability` (bool[]) | `z_levels[0].walkability` (int[] 0/1) |
| `Terrain.TerrainColor` (byte[] RGB) | `z_levels[0].terrain_png` -> PNG file on disk |
| `Layout.Nodes` (RoomNode list) | `z_levels[0].room_graph.rooms` |
| `Layout.Edges` (RoomEdge list) | `z_levels[0].room_graph.connections` |
| `Population.PlayerSpawn` | `spawns[]` entry with token_type "player" |
| `Population.Encounters` | `spawns[]` entries with token_type "enemy", stats from GameTables |
| `Population.Traps` | `z_levels[0].entities[]` with type "trap" |
| `Population.Loot` | `z_levels[0].entities[]` with type "loot" |
| `Population.Dressing` | `z_levels[0].entities[]` with type "dressing" |

**Terrain PNG generation:**
- Create `Texture2D` from `TerrainResult.TerrainColor` (RGB byte array)
- Encode to PNG via `ImageConversion.EncodeToPNG()` (or reflection fallback matching MapImporter pattern)
- Write to `outputDir/terrain_z0.png`

**Creature stat mapping:**
- Use `GameTables.GetCreatureStats(variant, partyLevel)` for HP, AC, ability scores, atk_dice
- If GameTables lacks a creature entry, use scaled defaults: HP = partyLevel * 4 + 4, AC = 10 + partyLevel/2, all abilities 10, speed 6, atk_dice = "1d6+{partyLevel/2}"

**Single z-level:** For the vertical slice, all generation is z=0. Schema supports multiple z-levels for future expansion.

### 2. Modify: LocationInteriorManager.cs

**Changes:**
- Remove `AssetGeneratorBridge` dependency (no more Python bridge, `_bridge` field, reflection setup)
- Remove `async Task GenerateAndShowAsync()` (generation is now synchronous C#)
- Replace with synchronous generation flow:

```csharp
private void GenerateInterior(LocationData loc)
{
    var (mapType, biome) = GetLocationProfile(loc);
    int partyLevel = GameManager.Instance?.Player?.Level ?? 3;

    var request = new MapGenerationRequest
    {
        MapType = mapType,
        Biome = biome,
        Width = 128,          // Interior-appropriate size
        Height = 128,
        Seed = GameManager.Instance?.CurrentSeed ?? 42,
        PartyLevel = partyLevel,
        PartySize = 1
    };

    var result = PipelineCoordinator.Generate(request);
    if (!result.Success) { ShowPopup(loc.Name, result.Error); return; }

    string outputDir = Path.GetDirectoryName(GetCachePath(loc));
    string mapPath = MapSerializer.Serialize(result, outputDir);

    // Validate and transition
    string mapJson = File.ReadAllText(mapPath);
    if (!SchemaValidator.ValidateMapData(mapJson))
        Debug.LogWarning("[LocationInterior] Generated map failed validation - loading anyway.");

    GameManager.Instance.PendingMapDataPath = mapPath;
    GameManager.Instance.PendingLocationId = loc.Id;
    SceneManager.LoadScene("Game");
}
```

- Keep cache check in `EnterLocation()` — if cached JSON exists and is valid, skip generation
- Keep `GetLocationProfile()`, `GetCachePath()`, `ApplyLocationEffects()` unchanged
- Keep IMGUI popup for error display only (success path goes straight to scene load)
- Remove `pythonPath`, `generatorScriptPath`, `timeoutSeconds` inspector fields

### 3. No changes: GameBootstrap.cs, MapImporter.cs

Both already handle the `PendingMapDataPath` -> `Import()` flow correctly. The JSON format is the contract — as long as MapSerializer produces valid v1.1.0 JSON, the downstream is unchanged.

## Map Size for Interiors

Interior maps use 128x128 (not the default 512x512) because:
- Dungeon interiors are confined spaces, not open worlds
- Faster generation and smaller file sizes
- Appropriate room count (6-12 rooms at 128x128 vs 50+ at 512x512)

## Follow-Up Tasks (Same Session)

### Task 3: ANTHROPIC_API_KEY Setup

`ClaudeAPIClient.cs` reads `Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")` at Awake. Set the Windows environment variable so Claude NPC dialogue works at runtime.

### Task 4: Auto-Launch Verification

`AutoLaunchDemo.cs` checks for `tests/launch-demo.flag`. Create the flag file, then open Unity — it should auto-open MainMenu and enter Play mode.

### Task 1: Play-Test Full Loop

End-to-end verification: MainMenu -> character creation -> overworld -> enter dungeon location -> Game scene loads with generated map -> combat encounter -> return to overworld.

## Files Changed

| File | Action |
|---|---|
| `Assets/Scripts/Generation/MapSerializer.cs` | **New** |
| `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs` | **Modify** — replace Python bridge with C# pipeline |

## Files Unchanged

| File | Reason |
|---|---|
| `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs` | Consumes JSON as before |
| `Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs` | Reads PendingMapDataPath as before |
| `Assets/Scripts/Generation/PipelineCoordinator.cs` | Already returns GenerationResult |
| `Assets/Scripts/Demo/GameManager.cs` | Already has PendingMapDataPath |
