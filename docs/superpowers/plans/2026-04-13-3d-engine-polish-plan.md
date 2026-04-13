# 3D Engine Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the 3D Forever Engine with sprint movement, three battle arena types, tiered dungeon encounters, and a DA visibility graph.

**Architecture:** Four independent changes touching DungeonExplorer (sprint + visibility graph), BattleRenderer3D + BattleSceneTemplate (arena walls), EncounterData + DADungeonBuilder (tiered encounters). All use existing MonoBehaviour patterns — no ECS changes.

**Tech Stack:** Unity 6 C#, Dungeon Architect 1.22 Snap builder, existing Rigidbody movement, primitive geometry.

**Spec:** `docs/superpowers/specs/2026-04-13-3d-engine-polish-design.md`

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | Modify | Sprint multiplier in HandleMovement(), graph-based fog of war |
| `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs` | Modify | Add ArenaType enum and field |
| `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs` | Modify | Arena wall/geometry building per ArenaType |
| `Assets/Scripts/Demo/Battle/BattleGrid.cs` | Modify | Accept non-walkable positions for overworld rocks |
| `Assets/Scripts/Demo/Battle/BattleManager.cs` | Modify | Set ArenaType on template based on encounter source |
| `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs` | Modify | Encode tier in encounter ID, build adjacency graph from SnapModel.connections |
| `Assets/Scripts/Demo/Encounters/EncounterData.cs` | Modify | Parse tier from encounter ID, apply budget/composition/reward scaling |
| `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` | Modify | Store tier from DADungeonBuilder |

---

### Task 1: Dungeon Sprint

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs:26` (constant) and `:249-285` (HandleMovement)

- [ ] **Step 1: Add SprintMultiplier constant**

In `DungeonExplorer.cs`, after line 26 (`private const float MoveSpeed = 6f;`), add:

```csharp
private const float SprintMultiplier = 1.8f;
```

- [ ] **Step 2: Apply sprint in HandleMovement()**

In `HandleMovement()`, replace the velocity assignment block. Change lines 273-275 from:

```csharp
Vector3 vel = moveDir * MoveSpeed;
vel.y = _playerRb.linearVelocity.y; // preserve gravity
_playerRb.linearVelocity = vel;
```

to:

```csharp
float speed = Input.GetKey(KeyCode.LeftShift) ? MoveSpeed * SprintMultiplier : MoveSpeed;
Vector3 vel = moveDir * speed;
vel.y = _playerRb.linearVelocity.y; // preserve gravity
_playerRb.linearVelocity = vel;
```

Also update the fallback path (lines 282-284) from:

```csharp
Vector3 vel = new Vector3(inputX, 0, inputZ).normalized * MoveSpeed;
vel.y = _playerRb.linearVelocity.y;
_playerRb.linearVelocity = vel;
```

to:

```csharp
float fallbackSpeed = Input.GetKey(KeyCode.LeftShift) ? MoveSpeed * SprintMultiplier : MoveSpeed;
Vector3 vel = new Vector3(inputX, 0, inputZ).normalized * fallbackSpeed;
vel.y = _playerRb.linearVelocity.y;
_playerRb.linearVelocity = vel;
```

- [ ] **Step 3: Compile check**

Run Roslyn compile check against the modified file using the `Library/Bee/*.rsp` response files:

```bash
cd "C:/Dev/Forever engine" && ls Library/Bee/*.rsp 2>/dev/null | head -1
```

If rsp files exist, run:
```bash
"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe" @Library/Bee/<file>.rsp 2>&1 | head -20
```

Otherwise verify syntax by inspecting the file for obvious errors.

- [ ] **Step 4: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs && git commit -m "feat: add Left Shift sprint to dungeon movement (1.8x speed)"
```

---

### Task 2: ArenaType Enum and BattleSceneTemplate

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs`

- [ ] **Step 1: Add ArenaType enum and field**

In `BattleSceneTemplate.cs`, add the enum before the class definition and a new field inside the class. The full file becomes:

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public enum ArenaType
    {
        Dungeon,
        Boss,
        Overworld
    }

    [CreateAssetMenu(fileName = "BattleTemplate", menuName = "Forever/Battle Scene Template")]
    public class BattleSceneTemplate : ScriptableObject
    {
        [Header("Room")]
        public GameObject RoomPrefab;
        public int GridWidth = 8;
        public int GridHeight = 8;
        public ArenaType Arena = ArenaType.Dungeon;

        [Header("Spawn Zones")]
        public Vector2Int[] PlayerSpawnZone = { new(1, 1), new(1, 2), new(2, 1), new(2, 2) };
        public Vector2Int[] EnemySpawnZone = { new(5, 5), new(5, 6), new(6, 5), new(6, 6) };
        public Vector2Int[] BossSpawnPoints = { new(4, 4) };

        [Header("Biome")]
        public string Biome = "dungeon";
        public bool IsBossArena;

        [Header("Lighting")]
        public Color AmbientColor = new Color(0.3f, 0.3f, 0.4f);
        public float LightIntensity = 1.0f;

        [Header("Variation")]
        [Tooltip("Prop prefabs that BattleVariation can scatter as obstacles.")]
        public GameObject[] ObstacleProps;
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs && git commit -m "feat: add ArenaType enum (Dungeon, Boss, Overworld) to BattleSceneTemplate"
```

---

### Task 3: Battle Arena Geometry

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs:35-53` (fallback floor block)
- Modify: `Assets/Scripts/Demo/Battle/BattleGrid.cs` (accept non-walkable rock positions)

- [ ] **Step 1: Add arena constants and BuildArena method**

In `BattleRenderer3D.cs`, add these constants after the `_ui` field (line 15):

```csharp
// Arena geometry constants (easy to bump later)
private const int DungeonGridSize = 8;
private const int BossGridSize = 12;
private const int OverworldGridSize = 16;
```

- [ ] **Step 2: Extract arena building into a method**

Replace the fallback floor creation block (the `else` branch at lines 35-53, inside `Initialize`) with a call to a new method. The `else` block becomes:

```csharp
else
{
    _roomInstance = new GameObject("BattleRoom");
    BuildArena(template.Arena, grid);
}
```

- [ ] **Step 3: Implement BuildArena method**

Add this method to `BattleRenderer3D` after the `Cleanup()` method:

```csharp
private void BuildArena(ArenaType arenaType, BattleGrid grid)
{
    float gridW = grid.Width * _cellSize;
    float gridH = grid.Height * _cellSize;
    float centerX = gridW / 2f;
    float centerZ = gridH / 2f;

    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

    switch (arenaType)
    {
        case ArenaType.Dungeon:
            BuildFloor(gridW, gridH, centerX, centerZ, new Color(0.3f, 0.3f, 0.35f), shader);
            BuildDungeonWalls(gridW, gridH, centerX, centerZ, 3f, 0.3f, new Color(0.3f, 0.3f, 0.35f), shader);
            break;

        case ArenaType.Boss:
            BuildFloor(gridW, gridH, centerX, centerZ, new Color(0.25f, 0.2f, 0.22f), shader);
            BuildBossWalls(gridW, gridH, centerX, centerZ, shader);
            break;

        case ArenaType.Overworld:
            // Floor larger than grid for open-space feel
            BuildFloor(gridW * 1.5f, gridH * 1.5f, centerX, centerZ, new Color(0.4f, 0.5f, 0.3f), shader);
            BuildOverworldRocks(grid, centerX, centerZ, shader);
            break;
    }
}

private void BuildFloor(float w, float h, float cx, float cz, Color color, Shader shader)
{
    var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
    floor.transform.SetParent(_roomInstance.transform);
    floor.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
    floor.transform.position = new Vector3(cx, 0f, cz);
    var mat = new Material(shader);
    if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", color);
    else
        mat.color = color;
    floor.GetComponent<Renderer>().material = mat;
}

private void BuildDungeonWalls(float gridW, float gridH, float cx, float cz,
    float wallHeight, float wallThickness, Color color, Shader shader)
{
    var mat = new Material(shader);
    if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", color);
    else
        mat.color = color;

    // North wall
    CreateWall("WallNorth", new Vector3(cx, wallHeight / 2f, gridH),
        new Vector3(gridW, wallHeight, wallThickness), mat);

    // East wall
    CreateWall("WallEast", new Vector3(gridW, wallHeight / 2f, cz),
        new Vector3(wallThickness, wallHeight, gridH), mat);

    // West wall
    CreateWall("WallWest", new Vector3(0f, wallHeight / 2f, cz),
        new Vector3(wallThickness, wallHeight, gridH), mat);

    // South wall — split with 2-unit door gap
    float doorGap = 2f;
    float segmentLen = (gridW - doorGap) / 2f;
    CreateWall("WallSouthL", new Vector3(segmentLen / 2f, wallHeight / 2f, 0f),
        new Vector3(segmentLen, wallHeight, wallThickness), mat);
    CreateWall("WallSouthR", new Vector3(gridW - segmentLen / 2f, wallHeight / 2f, 0f),
        new Vector3(segmentLen, wallHeight, wallThickness), mat);
}

private void BuildBossWalls(float gridW, float gridH, float cx, float cz, Shader shader)
{
    float wallHeight = 5f;
    float wallThickness = 0.5f;
    var wallColor = new Color(0.25f, 0.2f, 0.22f);

    var mat = new Material(shader);
    if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", wallColor);
    else
        mat.color = wallColor;

    // North wall
    CreateWall("WallNorth", new Vector3(cx, wallHeight / 2f, gridH),
        new Vector3(gridW, wallHeight, wallThickness), mat);

    // East wall
    CreateWall("WallEast", new Vector3(gridW, wallHeight / 2f, cz),
        new Vector3(wallThickness, wallHeight, gridH), mat);

    // West wall
    CreateWall("WallWest", new Vector3(0f, wallHeight / 2f, cz),
        new Vector3(wallThickness, wallHeight, gridH), mat);

    // South wall — 2.5-unit door gap
    float doorGap = 2.5f;
    float segmentLen = (gridW - doorGap) / 2f;
    CreateWall("WallSouthL", new Vector3(segmentLen / 2f, wallHeight / 2f, 0f),
        new Vector3(segmentLen, wallHeight, wallThickness), mat);
    CreateWall("WallSouthR", new Vector3(gridW - segmentLen / 2f, wallHeight / 2f, 0f),
        new Vector3(segmentLen, wallHeight, wallThickness), mat);

    // 4 corner pillars inside the arena
    float pillarSize = 0.8f;
    float inset = 2f; // inset from walls
    var pillarPositions = new Vector3[]
    {
        new(inset, wallHeight / 2f, inset),
        new(gridW - inset, wallHeight / 2f, inset),
        new(inset, wallHeight / 2f, gridH - inset),
        new(gridW - inset, wallHeight / 2f, gridH - inset),
    };
    for (int i = 0; i < pillarPositions.Length; i++)
        CreateWall($"Pillar_{i}", pillarPositions[i],
            new Vector3(pillarSize, wallHeight, pillarSize), mat);
}

private void BuildOverworldRocks(BattleGrid grid, float cx, float cz, Shader shader)
{
    var mat = new Material(shader);
    var rockColor = new Color(0.45f, 0.42f, 0.38f);
    if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", rockColor);
    else
        mat.color = rockColor;

    var rng = new System.Random(grid.Width * 1000 + grid.Height);
    int rockCount = 3 + rng.Next(3); // 3-5 rocks

    for (int i = 0; i < rockCount; i++)
    {
        // Place in inner 60% of grid to avoid spawn zones
        int rx, ry;
        int attempts = 0;
        do
        {
            rx = rng.Next(grid.Width / 4, grid.Width * 3 / 4);
            ry = rng.Next(grid.Height / 4, grid.Height * 3 / 4);
            attempts++;
        } while (attempts < 20 && !grid.IsWalkable(rx, ry));

        if (attempts >= 20) continue;

        // Mark as non-walkable
        grid.Walkable[ry * grid.Width + rx] = false;

        float scaleX = 0.6f + (float)rng.NextDouble() * 0.6f;
        float scaleY = 0.4f + (float)rng.NextDouble() * 0.4f;
        float scaleZ = 0.6f + (float)rng.NextDouble() * 0.6f;
        float rotY = (float)rng.NextDouble() * 45f;

        var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.name = $"Rock_{i}";
        rock.transform.SetParent(_roomInstance.transform);
        rock.transform.position = new Vector3(
            rx * _cellSize + _cellSize * 0.5f, scaleY / 2f,
            ry * _cellSize + _cellSize * 0.5f);
        rock.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
        rock.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        rock.GetComponent<Renderer>().material = mat;
        var col = rock.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }
}

private void CreateWall(string name, Vector3 position, Vector3 scale, Material mat)
{
    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
    wall.name = name;
    wall.transform.SetParent(_roomInstance.transform);
    wall.transform.position = position;
    wall.transform.localScale = scale;
    wall.GetComponent<Renderer>().material = mat;
    var col = wall.GetComponent<Collider>();
    if (col != null) Object.Destroy(col);
}
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleRenderer3D.cs Assets/Scripts/Demo/Battle/BattleGrid.cs && git commit -m "feat: add arena geometry — dungeon walls, boss pillars, overworld rocks"
```

---

### Task 4: Wire ArenaType in BattleManager

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs:148-149` (FindBattleTemplate call) and `:270-279` (FindBattleTemplate method)

- [ ] **Step 1: Set ArenaType on template after finding it**

In `BattleManager.Start()`, after line 148 (`var template = FindBattleTemplate(_encounterData);`), add ArenaType assignment before the null check:

```csharp
var template = FindBattleTemplate(_encounterData);
if (template != null)
{
    // Determine arena type from encounter context
    string encId = _encounterData.Id ?? "";
    if (encId.Contains("boss"))
        template.Arena = ArenaType.Boss;
    else if (encId.Contains("dungeon") || encId.Contains("Dungeon") || encId.Contains("Crypt"))
        template.Arena = ArenaType.Dungeon;
    else
        template.Arena = ArenaType.Overworld;
```

This replaces the existing `if (template != null)` opening brace at line 149 — the rest of the block stays the same.

- [ ] **Step 2: Update grid dimensions based on ArenaType**

In `BattleManager.Start()`, after setting ArenaType but before creating the grid (line 91), override grid dimensions from the encounter data if it doesn't specify them. Add after the `_encounterData = ...` line (87):

```csharp
// Override grid size based on encounter context for overworld
string rawEncId = _encounterData.Id ?? "";
if (!rawEncId.Contains("dungeon") && !rawEncId.Contains("Dungeon") && !rawEncId.Contains("boss") && !rawEncId.Contains("Crypt"))
{
    if (_encounterData.GridWidth <= 8) _encounterData.GridWidth = 16;
    if (_encounterData.GridHeight <= 8) _encounterData.GridHeight = 16;
}
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleManager.cs && git commit -m "feat: wire ArenaType from encounter context in BattleManager"
```

---

### Task 5: Encode Tier in Encounter IDs

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs:123` (encounter ID assignment)
- Modify: `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` (add Tier field)

- [ ] **Step 1: Add Tier to EncounterZone**

In `EncounterZone.cs`, add a `Tier` field after the existing fields:

```csharp
public string EncounterId;
public int ZoneIndex;
public bool IsBoss;
public int Tier;
```

- [ ] **Step 2: Encode tier in encounter ID in DADungeonBuilder**

In `DADungeonBuilder.cs`, change line 123 from:

```csharp
zone.EncounterId = isBoss ? "boss_dungeon" : $"random_dungeon_room{i}";
```

to:

```csharp
zone.EncounterId = isBoss ? "boss_dungeon" : $"random_dungeon_t{tier}_room{i}";
zone.Tier = tier;
```

Note: if `tier` is 0 (corridor — shouldn't happen since this is inside `if (!isCorridor)`), it will just be `t0` which the parser handles as tier 1 fallback.

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs Assets/Scripts/Demo/Dungeon/EncounterZone.cs && git commit -m "feat: encode room tier in dungeon encounter IDs"
```

---

### Task 6: Tiered Encounter Scaling in EncounterData

**Files:**
- Modify: `Assets/Scripts/Demo/Encounters/EncounterData.cs:44-98` (GenerateRandom method)

- [ ] **Step 1: Parse tier from encounter ID and apply budget scaling**

In `EncounterData.GenerateRandom()`, after the `xpBudget` calculation (line 73) and before the RNG seed (line 77), add tier parsing and scaling:

```csharp
// Parse tier from dungeon encounter ID (e.g. "random_dungeon_t2_room3")
int tier = 1; // default
if (id.Contains("_t1_")) tier = 1;
else if (id.Contains("_t2_")) tier = 2;
else if (id.Contains("_t3_")) tier = 3;

// Tier scaling: budget multiplier and minimum XP floor per enemy
float tierBudgetMult = tier switch { 1 => 0.8f, 2 => 1.2f, 3 => 1.6f, _ => 1f };
int tierMinXP = tier switch { 1 => 0, 2 => 50, 3 => 100, _ => 0 }; // skip weak enemies
float tierRewardMult = tier switch { 1 => 1.0f, 2 => 1.3f, 3 => 1.6f, _ => 1f };

// Only apply tier scaling to dungeon encounters
if (id.Contains("dungeon"))
    xpBudget = (int)(xpBudget * tierBudgetMult);
```

- [ ] **Step 2: Apply min XP floor in dungeon biome enemy selection**

In the `Dungeon/Crypt` branch (lines 146-201), the enemy selection uses `MakeCREnemyDef` with fixed XP values. Apply the floor by filtering: wrap the skeleton garrison branch. Replace the skeleton garrison count calculation (line 153):

```csharp
int effectiveXP = System.Math.Max(50, tierMinXP > 0 ? tierMinXP : 50);
int count = System.Math.Min(xpBudget / effectiveXP, maxEnemies);
```

And for the lizardfolk patrol (line 191):

```csharp
int effectiveXP2 = System.Math.Max(50, tierMinXP > 0 ? tierMinXP : 50);
int count = System.Math.Min(xpBudget / effectiveXP2, maxEnemies);
```

For the mummy branch (lines 166-186): the mummy itself is 200 XP which is always above any tier floor, and minions are 25 XP. Apply the floor to minion selection — if `tierMinXP > 25`, use stronger minions:

After the existing minion loop (line 178), replace the minion XP from 25 to `System.Math.Max(25, tierMinXP)`:

```csharp
int minionXP = System.Math.Max(25, tierMinXP);
int minions = System.Math.Min(remainBudget / minionXP, maxEnemies - enc.Enemies.Count);
```

And change the minion `MakeCREnemyDef` call to use `minionXP` instead of hardcoded 25:

```csharp
var def = MakeCREnemyDef("Skeleton", minionXP, "guard", "Ruins", DamageType.Slashing);
```

- [ ] **Step 3: Apply reward multiplier**

At the end of each dungeon encounter branch, before `return enc;`, multiply rewards. Since there are multiple return paths in the dungeon/crypt section, the cleanest approach is to add a post-processing step. After all the biome branches (before the final `return enc;` at line 297), add:

```csharp
// Apply tier reward scaling for dungeon encounters
if (id.Contains("dungeon") && tierRewardMult != 1f)
{
    enc.GoldReward = (int)(enc.GoldReward * tierRewardMult);
    enc.XPReward = (int)(enc.XPReward * tierRewardMult);
}
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Encounters/EncounterData.cs && git commit -m "feat: tier-based encounter scaling — budget, composition, rewards"
```

---

### Task 7: DA Visibility Graph — Build Adjacency

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs`

- [ ] **Step 1: Add adjacency graph fields**

In `DADungeonBuilder`, after the existing properties (line 30), add:

```csharp
private Dictionary<int, List<int>> _roomGraph = new();
public IReadOnlyDictionary<int, List<int>> RoomGraph => _roomGraph;
```

Add the required using at the top of the file:

```csharp
using System.Collections.Generic;
```

- [ ] **Step 2: Build adjacency graph from SnapModel connections**

In `OnPostDungeonBuild`, after the room initialization loop (after line 125, before the "If no explicit boss found" block at line 127), add the graph construction:

```csharp
// Build room adjacency graph from DA Snap connections
BuildRoomGraph(dungeon);
```

Then add the method after `GetRoomAtPosition`:

```csharp
private void BuildRoomGraph(DA.Dungeon dungeon)
{
    _roomGraph.Clear();

    // Initialize empty adjacency lists for all rooms
    for (int i = 0; i < Rooms.Length; i++)
        _roomGraph[i] = new List<int>();

    // Build InstanceID → room index mapping
    var idToIndex = new Dictionary<string, int>();
    if (Query != null && Query.modules != null)
    {
        for (int i = 0; i < Query.modules.Length; i++)
        {
            var instanceId = Query.modules[i].instanceInfo.InstanceID;
            if (!string.IsNullOrEmpty(instanceId))
                idToIndex[instanceId] = i;
        }
    }

    // Read connections from SnapModel
    var snapModel = dungeon.GetComponent<DungeonArchitect.Builders.Snap.SnapModel>();
    if (snapModel != null && snapModel.connections != null)
    {
        foreach (var conn in snapModel.connections)
        {
            if (idToIndex.TryGetValue(conn.ModuleAInstanceID, out int idxA) &&
                idToIndex.TryGetValue(conn.ModuleBInstanceID, out int idxB))
            {
                if (!_roomGraph[idxA].Contains(idxB))
                    _roomGraph[idxA].Add(idxB);
                if (!_roomGraph[idxB].Contains(idxA))
                    _roomGraph[idxB].Add(idxA);
            }
        }
    }

    // Fallback: if no connections found, use spatial proximity
    int totalEdges = 0;
    foreach (var edges in _roomGraph.Values) totalEdges += edges.Count;

    if (totalEdges == 0)
    {
        Debug.LogWarning("[DADungeonBuilder] No SnapModel connections found — using spatial proximity fallback");
        for (int i = 0; i < Rooms.Length; i++)
        {
            for (int j = i + 1; j < Rooms.Length; j++)
            {
                float dist = Vector3.Distance(Rooms[i].WorldBounds.center, Rooms[j].WorldBounds.center);
                float threshold = Mathf.Max(
                    Rooms[i].WorldBounds.size.magnitude,
                    Rooms[j].WorldBounds.size.magnitude) * 0.75f;

                if (dist < threshold)
                {
                    _roomGraph[i].Add(j);
                    _roomGraph[j].Add(i);
                }
            }
        }

        // Recount
        totalEdges = 0;
        foreach (var edges in _roomGraph.Values) totalEdges += edges.Count;
    }

    Debug.Log($"[DADungeonBuilder] Room graph: {Rooms.Length} nodes, {totalEdges / 2} edges");
}
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DADungeonBuilder.cs && git commit -m "feat: build room adjacency graph from DA SnapModel connections"
```

---

### Task 8: Graph-Based Fog of War

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs:288-326` (UpdateFogOfWar method)

- [ ] **Step 1: Add BFS helper**

In `DungeonExplorer`, add a BFS helper method before `SaveDungeonState`:

```csharp
/// <summary>
/// BFS from a start room, returning the depth of each reachable room up to maxDepth.
/// </summary>
private static Dictionary<int, int> BFSRoomDepths(IReadOnlyDictionary<int, System.Collections.Generic.List<int>> graph, int startRoom, int maxDepth)
{
    var depths = new Dictionary<int, int> { [startRoom] = 0 };
    var queue = new Queue<int>();
    queue.Enqueue(startRoom);

    while (queue.Count > 0)
    {
        int room = queue.Dequeue();
        int depth = depths[room];
        if (depth >= maxDepth) continue;

        if (graph.TryGetValue(room, out var neighbors))
        {
            foreach (int neighbor in neighbors)
            {
                if (!depths.ContainsKey(neighbor))
                {
                    depths[neighbor] = depth + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }
    return depths;
}
```

Add at the top of the file:

```csharp
using System.Collections.Generic;
```

- [ ] **Step 2: Replace distance-based fog with graph-based**

Replace the entire `UpdateFogOfWar` method (lines 290-326) with:

```csharp
private void UpdateFogOfWar()
{
    if (_daBuilder == null || _daBuilder.Rooms == null || _playerTransform == null) return;

    Vector3 playerPos = _playerTransform.position;
    var state = GameManager.Instance?.PendingDungeonState;

    int currentRoom = _daBuilder.GetRoomAtPosition(playerPos);

    // Use graph-based activation if the adjacency graph is available
    var graph = _daBuilder.RoomGraph;
    bool useGraph = graph != null && graph.Count > 0;

    Dictionary<int, int> roomDepths = null;
    if (useGraph && currentRoom >= 0)
        roomDepths = BFSRoomDepths(graph, currentRoom, 2);

    foreach (var room in _daBuilder.Rooms)
    {
        bool shouldBeActive;
        int depth = -1;

        if (useGraph && roomDepths != null)
        {
            // Graph-based: activate rooms within 2 hops
            roomDepths.TryGetValue(room.Index, out depth);
            shouldBeActive = depth >= 0 && depth <= 2;
        }
        else
        {
            // Fallback: distance-based (original behavior)
            float dist = Vector3.Distance(playerPos, room.WorldBounds.center);
            shouldBeActive = dist < RoomCullDistance;
        }

        if (room.RoomObject != null)
            room.RoomObject.SetActive(shouldBeActive);

        if (room.FogLight == null) continue;

        if (room.Index == currentRoom)
        {
            // Current room: full light
            room.FogLight.enabled = true;
            room.FogLight.intensity = room.OriginalLightIntensity;
            state?.VisitRoom(room.Index);
        }
        else if (shouldBeActive && depth == 1 && state != null && state.HasVisited(room.Index))
        {
            // 1-hop, previously visited: 50% light
            room.FogLight.enabled = true;
            room.FogLight.intensity = room.OriginalLightIntensity * 0.5f;
        }
        else if (shouldBeActive && depth <= 1 && !useGraph && state != null && state.HasVisited(room.Index))
        {
            // Fallback path: same as original behavior for nearby visited rooms
            room.FogLight.enabled = true;
            room.FogLight.intensity = room.OriginalLightIntensity * 0.5f;
        }
        else
        {
            // 2-hop rooms: geometry visible but dark (silhouette through doorways)
            // Beyond 2 hops or unvisited 1-hop: dark
            room.FogLight.enabled = false;
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs && git commit -m "feat: graph-based fog of war using DA room adjacency (replaces distance cull)"
```

---

### Task 9: Final Compile Check and Integration Verification

- [ ] **Step 1: Verify all modified files compile**

Check all modified files for syntax issues:

```bash
cd "C:/Dev/Forever engine" && rsp=$(ls Library/Bee/*.rsp 2>/dev/null | head -1) && if [ -n "$rsp" ]; then "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe" "@$rsp" 2>&1 | tail -30; else echo "No rsp files — check files manually"; fi
```

- [ ] **Step 2: Review all changes as a coherent diff**

```bash
cd "C:/Dev/Forever engine" && git log --oneline -10
```

Verify all 8 commits from this plan are present and the changes are coherent.

- [ ] **Step 3: Final commit (if any fixups needed)**

If any compile errors were found and fixed:

```bash
cd "C:/Dev/Forever engine" && git add -u && git commit -m "fix: resolve compile errors from 3D engine polish"
```
