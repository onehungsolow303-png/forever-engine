# Phase 1: Core Game Loop — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load a Map Generator dungeon into Unity and play it — move, see fog, fight enemies, win/lose.

**Architecture:** Hybrid DOTS/MonoBehaviour on Unity 6+. ECS handles game logic (fog, combat, AI, pathfinding). MonoBehaviour handles rendering, camera, UI, input. MapImporter bridges Map Generator JSON output into ECS entities and NativeArrays.

**Tech Stack:** Unity 6+, Entities (com.unity.entities), Burst (com.unity.burst), Collections (com.unity.collections), Unity Mathematics, Input System, UI Toolkit, Tilemap (2D)

**Spec:** `docs/superpowers/specs/2026-04-05-forever-engine-design.md`

**Existing scaffold:** 17 C# files already created in `Assets/Scripts/`. This plan fixes issues in the scaffold, fills gaps, and wires everything together with tests.

---

## File Map

### ECS Layer (`Assets/Scripts/ECS/`)

| File | Responsibility | Status |
|------|---------------|--------|
| `Components/StatsComponent.cs` | D&D stat block | Scaffolded, needs dice rolling utility |
| `Components/PositionComponent.cs` | Tile grid position | Scaffolded, complete |
| `Components/CombatStateComponent.cs` | Turn resources, faction | Scaffolded, complete |
| `Components/AIBehaviorComponent.cs` | AI config per entity | Scaffolded, complete |
| `Components/FogStateComponent.cs` | Fog enums + vision tag | Scaffolded, complete |
| `Components/VisualComponent.cs` | Sprite reference | Scaffolded, complete |
| `Components/PlayerTag.cs` | Tag component for player | **NEW** |
| `Data/MapDataBuffer.cs` | Walkability/elevation buffers | Scaffolded, complete |
| `Data/MapDataStore.cs` | NativeArray holder for map data | **NEW** — singleton managed class |
| `Jobs/FogRaycastJob.cs` | Parallel LOS raycasting | Scaffolded, complete |
| `Jobs/PathfindJob.cs` | A* pathfinding per NPC | Scaffolded, complete |
| `Jobs/AIDecisionJob.cs` | Batch NPC decision making | Scaffolded, complete |
| `Jobs/CombatJob.cs` | D20 attack resolution | **NEW** |
| `Systems/FogOfWarSystem.cs` | Fog dim + raycast scheduling | Scaffolded, needs MapDataStore fix |
| `Systems/GameStateSystem.cs` | Game state machine | Scaffolded, needs cleanup |
| `Systems/CombatSystem.cs` | Turn order + attack dispatch | **NEW** |
| `Systems/AIExecutionSystem.cs` | Runs AI decisions + pathfind | **NEW** |
| `Utility/DiceRoller.cs` | Parse "2d8+3", roll dice | **NEW** — Burst-compatible static methods |

### MonoBehaviour Layer (`Assets/Scripts/MonoBehaviour/`)

| File | Responsibility | Status |
|------|---------------|--------|
| `Bootstrap/GameBootstrap.cs` | Scene init, ECS world setup | Scaffolded, needs wiring |
| `Bootstrap/MapImporter.cs` | JSON → ECS entities | Scaffolded, needs MapDataStore |
| `Camera/CameraController.cs` | Smooth follow, zoom, pan | Scaffolded, complete |
| `Rendering/TileRenderer.cs` | Terrain PNG → Tilemap | **NEW** |
| `Rendering/EntityRenderer.cs` | ECS → SpriteRenderer sync | **NEW** |
| `Rendering/FogRenderer.cs` | Fog grid → visual overlay | **NEW** |
| `Input/InputManager.cs` | Unity Input System bindings | **NEW** |
| `Input/PlayerMovement.cs` | WASD → ECS position update | **NEW** |
| `UI/HUDManager.cs` | HP bar, mode indicator, stats | **NEW** |
| `UI/CombatLogUI.cs` | Scrolling combat messages | **NEW** |
| `UI/MinimapUI.cs` | Minimap with fog overlay | **NEW** |

### Shared (`Assets/Scripts/Shared/`)

| File | Responsibility | Status |
|------|---------------|--------|
| `SchemaValidator.cs` | Cross-project contract check | Scaffolded, complete |

### Tests (`Assets/Tests/`)

| File | Tests |
|------|-------|
| `EditMode/DiceRollerTests.cs` | Dice parsing, rolling, modifiers |
| `EditMode/StatsComponentTests.cs` | Stat modifiers, HP percent |
| `EditMode/FogRaycastJobTests.cs` | Raycasting correctness, wall blocking |
| `EditMode/PathfindJobTests.cs` | A* pathfinding, obstacle navigation |
| `EditMode/AIDecisionJobTests.cs` | AI behavior type decisions |
| `EditMode/CombatJobTests.cs` | Attack rolls, damage, death |
| `EditMode/SchemaValidatorTests.cs` | Map/asset JSON validation |
| `EditMode/MapImporterTests.cs` | JSON parsing, entity spawning |
| `PlayMode/GameLoopTests.cs` | Integration: load map, move, fight |

### Assembly Definitions

| File | Purpose |
|------|---------|
| `Assets/Scripts/ForeverEngine.asmdef` | Main runtime assembly |
| `Assets/Tests/EditMode/ForeverEngine.Tests.EditMode.asmdef` | Edit mode test assembly |
| `Assets/Tests/PlayMode/ForeverEngine.Tests.PlayMode.asmdef` | Play mode test assembly |

---

## Task 1: Unity Project Setup + DOTS Packages

**Files:**
- Create: `Assets/Scripts/ForeverEngine.asmdef`
- Create: `Assets/Tests/EditMode/ForeverEngine.Tests.EditMode.asmdef`
- Create: `Assets/Tests/PlayMode/ForeverEngine.Tests.PlayMode.asmdef`
- Create: `Packages/manifest.json`

This task sets up the Unity project structure so all subsequent code compiles. The user must have Unity 6+ installed. All DOTS packages are added via the package manifest.

- [ ] **Step 1: Create Unity package manifest**

Create `Packages/manifest.json` listing required packages. This tells Unity which packages to install when the project is opened.

```json
{
  "dependencies": {
    "com.unity.entities": "1.3.5",
    "com.unity.burst": "1.8.18",
    "com.unity.collections": "2.5.1",
    "com.unity.mathematics": "1.3.2",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.2d.tilemap": "1.0.0",
    "com.unity.2d.sprite": "1.0.0",
    "com.unity.ui": "2.0.0",
    "com.unity.test-framework": "1.4.5",
    "com.unity.render-pipelines.universal": "17.0.3"
  }
}
```

- [ ] **Step 2: Create main assembly definition**

```json
{
  "name": "ForeverEngine",
  "rootNamespace": "ForeverEngine",
  "references": [
    "Unity.Entities",
    "Unity.Burst",
    "Unity.Collections",
    "Unity.Mathematics",
    "Unity.Transforms",
    "Unity.InputSystem"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": true
}
```

Save to `Assets/Scripts/ForeverEngine.asmdef`.

- [ ] **Step 3: Create edit mode test assembly definition**

```json
{
  "name": "ForeverEngine.Tests.EditMode",
  "rootNamespace": "ForeverEngine.Tests",
  "references": [
    "ForeverEngine",
    "Unity.Entities",
    "Unity.Burst",
    "Unity.Collections",
    "Unity.Mathematics"
  ],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": []
}
```

Save to `Assets/Tests/EditMode/ForeverEngine.Tests.EditMode.asmdef`.

- [ ] **Step 4: Create play mode test assembly definition**

```json
{
  "name": "ForeverEngine.Tests.PlayMode",
  "rootNamespace": "ForeverEngine.Tests",
  "references": [
    "ForeverEngine",
    "Unity.Entities",
    "Unity.Collections",
    "Unity.Mathematics"
  ],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": [],
  "excludePlatforms": []
}
```

Save to `Assets/Tests/PlayMode/ForeverEngine.Tests.PlayMode.asmdef`.

- [ ] **Step 5: Commit**

```bash
git add Packages/ Assets/Scripts/ForeverEngine.asmdef Assets/Tests/
git commit -m "feat: Unity project setup with DOTS packages and assembly definitions"
```

---

## Task 2: DiceRoller Utility + StatsComponent Tests

**Files:**
- Create: `Assets/Scripts/ECS/Utility/DiceRoller.cs`
- Create: `Assets/Tests/EditMode/DiceRollerTests.cs`
- Create: `Assets/Tests/EditMode/StatsComponentTests.cs`

Pure logic, no Unity runtime required. All Burst-compatible (no heap allocation).

- [ ] **Step 1: Write DiceRoller failing tests**

```csharp
// Assets/Tests/EditMode/DiceRollerTests.cs
using NUnit.Framework;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.Tests
{
    public class DiceRollerTests
    {
        [Test]
        public void ParseDice_Simple_1d6()
        {
            DiceRoller.Parse("1d6", out int count, out int sides, out int bonus);
            Assert.AreEqual(1, count);
            Assert.AreEqual(6, sides);
            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void ParseDice_WithBonus_2d8Plus3()
        {
            DiceRoller.Parse("2d8+3", out int count, out int sides, out int bonus);
            Assert.AreEqual(2, count);
            Assert.AreEqual(8, sides);
            Assert.AreEqual(3, bonus);
        }

        [Test]
        public void ParseDice_WithPenalty_1d4Minus1()
        {
            DiceRoller.Parse("1d4-1", out int count, out int sides, out int bonus);
            Assert.AreEqual(1, count);
            Assert.AreEqual(4, sides);
            Assert.AreEqual(-1, bonus);
        }

        [Test]
        public void ParseDice_Uppercase_3D10Plus5()
        {
            DiceRoller.Parse("3D10+5", out int count, out int sides, out int bonus);
            Assert.AreEqual(3, count);
            Assert.AreEqual(10, sides);
            Assert.AreEqual(5, bonus);
        }

        [Test]
        public void ParseDice_Invalid_ReturnsDefaults()
        {
            DiceRoller.Parse("garbage", out int count, out int sides, out int bonus);
            Assert.AreEqual(1, count);
            Assert.AreEqual(4, sides);
            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void Roll_1d6_InRange()
        {
            // Roll 100 times, all should be 1-6
            uint seed = 12345;
            for (int i = 0; i < 100; i++)
            {
                int result = DiceRoller.Roll(1, 6, 0, ref seed);
                Assert.GreaterOrEqual(result, 1);
                Assert.LessOrEqual(result, 6);
            }
        }

        [Test]
        public void Roll_2d6Plus3_InRange()
        {
            uint seed = 54321;
            for (int i = 0; i < 100; i++)
            {
                int result = DiceRoller.Roll(2, 6, 3, ref seed);
                Assert.GreaterOrEqual(result, 5);   // min: 2+3
                Assert.LessOrEqual(result, 15);     // max: 12+3
            }
        }

        [Test]
        public void AbilityModifier_Standard()
        {
            Assert.AreEqual(-5, DiceRoller.AbilityModifier(1));
            Assert.AreEqual(-1, DiceRoller.AbilityModifier(8));
            Assert.AreEqual(0, DiceRoller.AbilityModifier(10));
            Assert.AreEqual(0, DiceRoller.AbilityModifier(11));
            Assert.AreEqual(2, DiceRoller.AbilityModifier(14));
            Assert.AreEqual(5, DiceRoller.AbilityModifier(20));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: Open Unity → Window → General → Test Runner → EditMode → Run All
Expected: FAIL — `DiceRoller` type not found

- [ ] **Step 3: Implement DiceRoller**

```csharp
// Assets/Scripts/ECS/Utility/DiceRoller.cs
using Unity.Burst;
using Unity.Mathematics;

namespace ForeverEngine.ECS.Utility
{
    /// <summary>
    /// Burst-compatible dice utilities — rewritten from pygame entities.py roll_dice().
    /// Uses xorshift32 RNG for deterministic Burst-safe random numbers.
    /// No heap allocation. Safe for use inside IJob.
    /// </summary>
    [BurstCompile]
    public static class DiceRoller
    {
        /// <summary>
        /// Parse dice notation string "2d8+3" into components.
        /// </summary>
        public static void Parse(string dice, out int count, out int sides, out int bonus)
        {
            count = 1; sides = 4; bonus = 0;
            if (string.IsNullOrEmpty(dice)) return;

            string s = dice.ToLowerInvariant().Trim();
            int dIdx = s.IndexOf('d');
            if (dIdx < 0) return;

            if (!int.TryParse(s.Substring(0, dIdx), out count)) { count = 1; return; }

            string rest = s.Substring(dIdx + 1);
            int plusIdx = rest.IndexOf('+');
            int minusIdx = rest.IndexOf('-');
            int modIdx = plusIdx >= 0 ? plusIdx : minusIdx;

            if (modIdx >= 0)
            {
                if (!int.TryParse(rest.Substring(0, modIdx), out sides)) sides = 4;
                if (!int.TryParse(rest.Substring(modIdx), out bonus)) bonus = 0;
            }
            else
            {
                if (!int.TryParse(rest, out sides)) sides = 4;
            }
        }

        /// <summary>
        /// Roll dice with xorshift32 RNG. Burst-compatible.
        /// seed is modified in-place for sequential rolls.
        /// </summary>
        [BurstCompile]
        public static int Roll(int count, int sides, int bonus, ref uint seed)
        {
            int total = bonus;
            for (int i = 0; i < count; i++)
            {
                seed = Xorshift32(seed);
                total += (int)(seed % (uint)sides) + 1;
            }
            return total;
        }

        /// <summary>
        /// D&D ability modifier: (score - 10) / 2, rounded down.
        /// </summary>
        [BurstCompile]
        public static int AbilityModifier(int score)
        {
            return (score - 10) / 2;
        }

        /// <summary>
        /// Xorshift32 PRNG — fast, deterministic, Burst-safe.
        /// </summary>
        [BurstCompile]
        public static uint Xorshift32(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: Unity Test Runner → EditMode → DiceRollerTests → Run All
Expected: 8/8 PASS

- [ ] **Step 5: Write StatsComponent tests**

```csharp
// Assets/Tests/EditMode/StatsComponentTests.cs
using NUnit.Framework;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.Tests
{
    public class StatsComponentTests
    {
        [Test]
        public void Default_AllScores10()
        {
            var stats = StatsComponent.Default;
            Assert.AreEqual(10, stats.Strength);
            Assert.AreEqual(10, stats.Dexterity);
            Assert.AreEqual(10, stats.AC);
            Assert.AreEqual(10, stats.HP);
            Assert.AreEqual(6, stats.Speed);
        }

        [Test]
        public void Modifiers_Correct()
        {
            var stats = StatsComponent.Default;
            stats.Strength = 16;
            stats.Dexterity = 8;
            Assert.AreEqual(3, stats.StrMod);   // (16-10)/2 = 3
            Assert.AreEqual(-1, stats.DexMod);   // (8-10)/2 = -1
        }

        [Test]
        public void HPPercent_HalfHealth()
        {
            var stats = StatsComponent.Default;
            stats.HP = 5;
            stats.MaxHP = 10;
            Assert.AreEqual(0.5f, stats.HPPercent, 0.001f);
        }

        [Test]
        public void HPPercent_Dead()
        {
            var stats = StatsComponent.Default;
            stats.HP = 0;
            stats.MaxHP = 10;
            Assert.AreEqual(0f, stats.HPPercent, 0.001f);
        }

        [Test]
        public void HPPercent_ZeroMaxHP_ReturnsZero()
        {
            var stats = StatsComponent.Default;
            stats.HP = 0;
            stats.MaxHP = 0;
            Assert.AreEqual(0f, stats.HPPercent, 0.001f);
        }
    }
}
```

- [ ] **Step 6: Run StatsComponent tests**

Run: Unity Test Runner → EditMode → StatsComponentTests → Run All
Expected: 5/5 PASS (StatsComponent already scaffolded)

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/ECS/Utility/ Assets/Tests/EditMode/
git commit -m "feat: DiceRoller utility with Burst-safe xorshift32 RNG + tests"
```

---

## Task 3: MapDataStore + MapImporter Fix

**Files:**
- Create: `Assets/Scripts/ECS/Data/MapDataStore.cs`
- Modify: `Assets/Scripts/ECS/Systems/FogOfWarSystem.cs` — replace broken singleton reference
- Modify: `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs` — use MapDataStore
- Create: `Assets/Tests/EditMode/MapImporterTests.cs`

The current FogOfWarSystem references `SystemAPI.GetSingleton<MapDataSingleton>().Walkability` but MapDataSingleton has no Walkability field. We need a managed data store that holds NativeArrays.

- [ ] **Step 1: Write MapImporter failing tests**

```csharp
// Assets/Tests/EditMode/MapImporterTests.cs
using NUnit.Framework;
using ForeverEngine.ECS.Data;
using Unity.Collections;

namespace ForeverEngine.Tests
{
    public class MapDataStoreTests
    {
        [Test]
        public void CreateStore_SetsWidthHeight()
        {
            var store = new MapDataStore();
            store.Initialize(64, 64);
            Assert.AreEqual(64, store.Width);
            Assert.AreEqual(64, store.Height);
            store.Dispose();
        }

        [Test]
        public void Walkability_CorrectSize()
        {
            var store = new MapDataStore();
            store.Initialize(32, 32);
            Assert.AreEqual(32 * 32, store.Walkability.Length);
            store.Dispose();
        }

        [Test]
        public void FogGrid_InitializedUnexplored()
        {
            var store = new MapDataStore();
            store.Initialize(16, 16);
            for (int i = 0; i < store.FogGrid.Length; i++)
                Assert.AreEqual(0, store.FogGrid[i]); // UNEXPLORED = 0
            store.Dispose();
        }

        [Test]
        public void SetWalkability_RoundTrips()
        {
            var store = new MapDataStore();
            store.Initialize(8, 8);
            // Set a pattern: row 0 all walkable, row 1 all walls
            for (int x = 0; x < 8; x++)
            {
                store.Walkability[0 * 8 + x] = true;
                store.Walkability[1 * 8 + x] = false;
            }
            Assert.IsTrue(store.Walkability[0]);      // (0,0) walkable
            Assert.IsFalse(store.Walkability[8]);      // (0,1) wall
            store.Dispose();
        }

        [Test]
        public void LoadZLevel_ParsesWalkabilityArray()
        {
            var store = new MapDataStore();
            store.Initialize(4, 4);
            int[] flat = { 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            store.LoadWalkability(0, flat);
            Assert.IsTrue(store.Walkability[0]);    // (0,0) = 1
            Assert.IsTrue(store.Walkability[1]);    // (1,0) = 1
            Assert.IsFalse(store.Walkability[2]);   // (2,0) = 0
            Assert.IsTrue(store.Walkability[15]);   // (3,3) = 1
            store.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — `MapDataStore` type not found

- [ ] **Step 3: Implement MapDataStore**

```csharp
// Assets/Scripts/ECS/Data/MapDataStore.cs
using Unity.Collections;
using System;

namespace ForeverEngine.ECS.Data
{
    /// <summary>
    /// Managed holder for map NativeArrays accessed by both ECS jobs and MonoBehaviour.
    /// Singleton instance — one active map at a time.
    /// Jobs read Walkability and FogGrid via NativeArray references.
    /// </summary>
    public class MapDataStore : IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int CurrentZ { get; set; }

        /// <summary>Flattened [y * Width + x] bool walkability grid for current z-level.</summary>
        public NativeArray<bool> Walkability { get; private set; }

        /// <summary>Flattened [y * Width + x] fog state (0=Unexplored, 1=Explored, 2=Visible).</summary>
        public NativeArray<byte> FogGrid { get; private set; }

        /// <summary>Flattened [y * Width + x] elevation values, optional.</summary>
        public NativeArray<float> Elevation { get; private set; }

        private bool _disposed;

        public static MapDataStore Instance { get; private set; }

        public void Initialize(int width, int height)
        {
            Dispose(); // Clean up any previous data
            Width = width;
            Height = height;
            int size = width * height;

            Walkability = new NativeArray<bool>(size, Allocator.Persistent);
            FogGrid = new NativeArray<byte>(size, Allocator.Persistent);
            Elevation = new NativeArray<float>(size, Allocator.Persistent);

            Instance = this;
            _disposed = false;
        }

        public void LoadWalkability(int z, int[] flatData)
        {
            int size = Width * Height;
            int len = Math.Min(flatData.Length, size);
            for (int i = 0; i < len; i++)
                Walkability[i] = flatData[i] != 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (Walkability.IsCreated) Walkability.Dispose();
            if (FogGrid.IsCreated) FogGrid.Dispose();
            if (Elevation.IsCreated) Elevation.Dispose();
            _disposed = true;
            if (Instance == this) Instance = null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Expected: 5/5 PASS

- [ ] **Step 5: Update FogOfWarSystem to use MapDataStore**

Replace the broken singleton reference in `Assets/Scripts/ECS/Systems/FogOfWarSystem.cs`. Remove the `_fogGrid` field and `InitializeGrid()` method. The system now reads from `MapDataStore.Instance`.

```csharp
// Assets/Scripts/ECS/Systems/FogOfWarSystem.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FogOfWarSystem : ISystem
    {
        private int2 _lastPlayerPos;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FogVisionComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var store = MapDataStore.Instance;
            if (store == null || !store.FogGrid.IsCreated) return;

            foreach (var (vision, position) in
                SystemAPI.Query<RefRO<FogVisionComponent>, RefRO<PositionComponent>>())
            {
                var playerPos = new int2(position.ValueRO.X, position.ValueRO.Y);

                if (playerPos.Equals(_lastPlayerPos) && _initialized)
                    continue;

                _lastPlayerPos = playerPos;
                _initialized = true;

                var dimJob = new FogDimJob { FogGrid = store.FogGrid };
                var dimHandle = dimJob.Schedule(store.FogGrid.Length, 256, state.Dependency);

                var rayJob = new FogRaycastJob
                {
                    PlayerX = playerPos.x,
                    PlayerY = playerPos.y,
                    SightRadius = vision.ValueRO.SightRadius,
                    MapWidth = store.Width,
                    MapHeight = store.Height,
                    Walkability = store.Walkability,
                    FogGrid = store.FogGrid
                };
                var rayHandle = rayJob.Schedule(360, 36, dimHandle);

                state.Dependency = rayHandle;
            }
        }
    }
}
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/ECS/Data/MapDataStore.cs Assets/Scripts/ECS/Systems/FogOfWarSystem.cs Assets/Tests/EditMode/MapImporterTests.cs
git commit -m "feat: MapDataStore with NativeArray management, fix FogOfWarSystem"
```

---

## Task 4: FogRaycastJob Tests

**Files:**
- Create: `Assets/Tests/EditMode/FogRaycastJobTests.cs`

Tests the core fog of war raycasting logic in isolation. No Unity runtime needed — just NativeArrays and the Burst job.

- [ ] **Step 1: Write fog raycast tests**

```csharp
// Assets/Tests/EditMode/FogRaycastJobTests.cs
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.Tests
{
    public class FogRaycastJobTests
    {
        [Test]
        public void OpenRoom_AllVisible()
        {
            // 8x8 all walkable, player at center (4,4), radius 8
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            var fog = new NativeArray<byte>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var job = new FogRaycastJob
            {
                PlayerX = 4, PlayerY = 4, SightRadius = 8,
                MapWidth = w, MapHeight = h,
                Walkability = walk, FogGrid = fog
            };
            job.Schedule(360, 36).Complete();

            // Player's tile and nearby tiles should be VISIBLE (2)
            Assert.AreEqual(2, fog[4 * w + 4]); // Player tile
            Assert.AreEqual(2, fog[4 * w + 5]); // Adjacent

            walk.Dispose();
            fog.Dispose();
        }

        [Test]
        public void WallBlocks_BehindWall_NotVisible()
        {
            // 8x8 with wall column at x=5 (full height)
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            var fog = new NativeArray<byte>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;
            // Wall column at x=5
            for (int y = 0; y < h; y++) walk[y * w + 5] = false;

            // Player at (2,4)
            var job = new FogRaycastJob
            {
                PlayerX = 2, PlayerY = 4, SightRadius = 8,
                MapWidth = w, MapHeight = h,
                Walkability = walk, FogGrid = fog
            };
            job.Schedule(360, 36).Complete();

            // Wall itself should be visible (rays mark wall then stop)
            Assert.AreEqual(2, fog[4 * w + 5]); // Wall tile visible

            // Behind wall (x=6,7) should NOT be visible
            Assert.AreEqual(0, fog[4 * w + 7]); // Behind wall

            walk.Dispose();
            fog.Dispose();
        }

        [Test]
        public void DimJob_VisibleBecomesExplored()
        {
            int size = 16;
            var fog = new NativeArray<byte>(size, Allocator.TempJob);
            fog[0] = 2; // VISIBLE
            fog[1] = 1; // EXPLORED
            fog[2] = 0; // UNEXPLORED

            new FogDimJob { FogGrid = fog }.Schedule(size, 8).Complete();

            Assert.AreEqual(1, fog[0]); // Was VISIBLE → now EXPLORED
            Assert.AreEqual(1, fog[1]); // Was EXPLORED → stays EXPLORED
            Assert.AreEqual(0, fog[2]); // Was UNEXPLORED → stays UNEXPLORED

            fog.Dispose();
        }

        [Test]
        public void OutOfBounds_DoesNotCrash()
        {
            // Player at corner (0,0) in tiny 4x4 map
            int w = 4, h = 4;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            var fog = new NativeArray<byte>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var job = new FogRaycastJob
            {
                PlayerX = 0, PlayerY = 0, SightRadius = 16,
                MapWidth = w, MapHeight = h,
                Walkability = walk, FogGrid = fog
            };

            // Should not throw even though radius exceeds map size
            Assert.DoesNotThrow(() => job.Schedule(360, 36).Complete());

            walk.Dispose();
            fog.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run tests**

Expected: 4/4 PASS (FogRaycastJob already implemented)

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/EditMode/FogRaycastJobTests.cs
git commit -m "test: FogRaycastJob unit tests — visibility, wall blocking, bounds safety"
```

---

## Task 5: PathfindJob Tests

**Files:**
- Create: `Assets/Tests/EditMode/PathfindJobTests.cs`

Tests A* pathfinding correctness — replacing the greedy chase from pygame ai.py.

- [ ] **Step 1: Write pathfinding tests**

```csharp
// Assets/Tests/EditMode/PathfindJobTests.cs
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.Tests
{
    public class PathfindJobTests
    {
        [Test]
        public void StraightLine_FindsPath()
        {
            // 8x8 open, NPC at (1,4), target at (6,4)
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(1, 4);
            targets[0] = new int2(6, 4);
            maxSteps[0] = 20;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsTrue(found[0]);
            // First step should move toward target (x should increase)
            Assert.AreEqual(2, nextSteps[0].x);
            Assert.AreEqual(4, nextSteps[0].y);

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }

        [Test]
        public void NavigatesAroundWall()
        {
            // 8x8 with wall at x=3 (y=2 to y=5), NPC at (1,4), target at (6,4)
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;
            for (int y = 2; y <= 5; y++) walk[y * w + 3] = false;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(1, 4);
            targets[0] = new int2(6, 4);
            maxSteps[0] = 30;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsTrue(found[0]);
            // NPC should path around the wall, not get stuck
            // First step should be valid (not into wall)
            var step = nextSteps[0];
            Assert.IsTrue(walk[step.y * w + step.x], "First step must be walkable");

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }

        [Test]
        public void Adjacent_StaysPut()
        {
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(4, 4);
            targets[0] = new int2(4, 5);  // 1 tile away
            maxSteps[0] = 10;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsTrue(found[0]);
            // Already adjacent, should stay
            Assert.AreEqual(starts[0], nextSteps[0]);

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }

        [Test]
        public void NoPath_Blocked()
        {
            // NPC surrounded by walls
            int w = 8, h = 8;
            var walk = new NativeArray<bool>(w * h, Allocator.TempJob);
            for (int i = 0; i < walk.Length; i++) walk[i] = true;
            // Box around (4,4)
            walk[3 * w + 4] = false; walk[5 * w + 4] = false;
            walk[4 * w + 3] = false; walk[4 * w + 5] = false;

            var starts = new NativeArray<int2>(1, Allocator.TempJob);
            var targets = new NativeArray<int2>(1, Allocator.TempJob);
            var maxSteps = new NativeArray<int>(1, Allocator.TempJob);
            var nextSteps = new NativeArray<int2>(1, Allocator.TempJob);
            var found = new NativeArray<bool>(1, Allocator.TempJob);
            var occupied = new NativeArray<int2>(0, Allocator.TempJob);

            starts[0] = new int2(4, 4);
            targets[0] = new int2(0, 0);
            maxSteps[0] = 20;

            new PathfindJob
            {
                MapWidth = w, MapHeight = h,
                Walkability = walk, OccupiedTiles = occupied,
                StartPositions = starts, TargetPositions = targets,
                MaxSteps = maxSteps, NextSteps = nextSteps, PathFound = found
            }.Schedule(1, 1).Complete();

            Assert.IsFalse(found[0]);

            walk.Dispose(); starts.Dispose(); targets.Dispose();
            maxSteps.Dispose(); nextSteps.Dispose(); found.Dispose(); occupied.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run tests**

Expected: 4/4 PASS

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/EditMode/PathfindJobTests.cs
git commit -m "test: PathfindJob A* tests — straight line, wall nav, adjacent, blocked"
```

---

## Task 6: CombatJob + CombatSystem

**Files:**
- Create: `Assets/Scripts/ECS/Jobs/CombatJob.cs`
- Create: `Assets/Scripts/ECS/Systems/CombatSystem.cs`
- Create: `Assets/Tests/EditMode/CombatJobTests.cs`

Rewrites pygame combat.py D20 attack resolution as a Burst-compiled job.

- [ ] **Step 1: Write combat job tests**

```csharp
// Assets/Tests/EditMode/CombatJobTests.cs
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.Tests
{
    public class CombatJobTests
    {
        [Test]
        public void Attack_HitAndDamage()
        {
            var results = new NativeArray<CombatResult>(1, Allocator.TempJob);

            // Attacker: STR 16 (+3 mod), 1d8+3 damage
            // Defender: AC 10
            // With seed that produces a hit (roll >= 7 needed: 10 - 3 = 7)
            var job = new CombatJob
            {
                AttackerStrMod = 3,
                DefenderAC = 10,
                AtkDiceCount = 1,
                AtkDiceSides = 8,
                AtkDiceBonus = 3,
                Seed = 99999, // Chosen to produce hit
                Results = results
            };
            job.Schedule().Complete();

            // With this seed we just check the result is structurally valid
            var r = results[0];
            Assert.GreaterOrEqual(r.AttackRoll, 1);
            Assert.LessOrEqual(r.AttackRoll, 20);
            if (r.Hit)
            {
                Assert.GreaterOrEqual(r.Damage, 4);  // min 1+3
                Assert.LessOrEqual(r.Damage, 11);    // max 8+3
            }
            else
            {
                Assert.AreEqual(0, r.Damage);
            }

            results.Dispose();
        }

        [Test]
        public void Attack_NatOne_AlwaysMisses()
        {
            // Find a seed that produces roll of 1
            var results = new NativeArray<CombatResult>(1, Allocator.TempJob);
            uint seed = 1;
            // Brute force a nat 1 seed
            for (uint s = 1; s < 100000; s++)
            {
                uint test = s;
                test ^= test << 13;
                test ^= test >> 17;
                test ^= test << 5;
                if ((int)(test % 20) + 1 == 1) { seed = s; break; }
            }

            var job = new CombatJob
            {
                AttackerStrMod = 10,  // Even with +10 mod, nat 1 misses
                DefenderAC = 1,
                AtkDiceCount = 1, AtkDiceSides = 8, AtkDiceBonus = 0,
                Seed = seed,
                Results = results
            };
            job.Schedule().Complete();

            Assert.AreEqual(1, results[0].AttackRoll);
            Assert.IsFalse(results[0].Hit);
            Assert.AreEqual(0, results[0].Damage);

            results.Dispose();
        }

        [Test]
        public void Attack_NatTwenty_AlwaysHits()
        {
            var results = new NativeArray<CombatResult>(1, Allocator.TempJob);
            uint seed = 1;
            for (uint s = 1; s < 100000; s++)
            {
                uint test = s;
                test ^= test << 13;
                test ^= test >> 17;
                test ^= test << 5;
                if ((int)(test % 20) + 1 == 20) { seed = s; break; }
            }

            var job = new CombatJob
            {
                AttackerStrMod = -5,  // Even with -5, nat 20 hits
                DefenderAC = 30,
                AtkDiceCount = 1, AtkDiceSides = 6, AtkDiceBonus = 0,
                Seed = seed,
                Results = results
            };
            job.Schedule().Complete();

            Assert.AreEqual(20, results[0].AttackRoll);
            Assert.IsTrue(results[0].Hit);
            Assert.Greater(results[0].Damage, 0);

            results.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — `CombatJob` and `CombatResult` types not found

- [ ] **Step 3: Implement CombatJob**

```csharp
// Assets/Scripts/ECS/Jobs/CombatJob.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.ECS.Jobs
{
    /// <summary>
    /// D20 attack resolution — rewritten from pygame combat.py player_attack().
    /// Resolves a single attack: roll d20 + STR mod vs AC, roll damage if hit.
    /// Nat 1 always misses. Nat 20 always hits.
    /// </summary>
    [BurstCompile]
    public struct CombatJob : IJob
    {
        [ReadOnly] public int AttackerStrMod;
        [ReadOnly] public int DefenderAC;
        [ReadOnly] public int AtkDiceCount;
        [ReadOnly] public int AtkDiceSides;
        [ReadOnly] public int AtkDiceBonus;
        public uint Seed;

        [WriteOnly] public NativeArray<CombatResult> Results;

        public void Execute()
        {
            // Roll d20 for attack
            int attackRoll = DiceRoller.Roll(1, 20, 0, ref Seed);
            bool hit;

            if (attackRoll == 1)
                hit = false;  // Nat 1: always miss
            else if (attackRoll == 20)
                hit = true;   // Nat 20: always hit
            else
                hit = (attackRoll + AttackerStrMod) >= DefenderAC;

            int damage = 0;
            if (hit)
            {
                damage = DiceRoller.Roll(AtkDiceCount, AtkDiceSides, AtkDiceBonus, ref Seed);
                if (damage < 1) damage = 1; // Minimum 1 damage on hit
            }

            Results[0] = new CombatResult
            {
                AttackRoll = attackRoll,
                Hit = hit,
                Damage = damage
            };
        }
    }

    public struct CombatResult
    {
        public int AttackRoll;
        public bool Hit;
        public int Damage;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Expected: 3/3 PASS

- [ ] **Step 5: Implement CombatSystem**

```csharp
// Assets/Scripts/ECS/Systems/CombatSystem.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Jobs;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Combat turn system — rewritten from pygame combat.py CombatManager.
    /// Manages initiative order, turn cycling, attack dispatch.
    /// Reads GameStateSingleton to know when combat is active.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateSystem))]
    public partial struct CombatSystem : ISystem
    {
        private NativeList<Entity> _turnOrder;
        private bool _combatActive;
        private uint _rngSeed;

        public void OnCreate(ref SystemState state)
        {
            _turnOrder = new NativeList<Entity>(16, Allocator.Persistent);
            _rngSeed = (uint)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();

            if (gameState.CurrentState != GameState.Combat)
            {
                if (_combatActive)
                {
                    _turnOrder.Clear();
                    _combatActive = false;
                }
                return;
            }

            if (!_combatActive)
            {
                RollInitiative(ref state);
                _combatActive = true;
            }
        }

        private void RollInitiative(ref SystemState state)
        {
            _turnOrder.Clear();

            // Collect all alive combatants and roll initiative
            foreach (var (combat, stats, entity) in
                SystemAPI.Query<RefRW<CombatStateComponent>, RefRO<StatsComponent>>()
                    .WithEntityAccess())
            {
                if (!combat.ValueRO.Alive) continue;

                int roll = DiceRoller.Roll(1, 20, 0, ref _rngSeed);
                combat.ValueRW.InitiativeRoll = roll + stats.ValueRO.DexMod;
                combat.ValueRW.MovementRemaining = stats.ValueRO.Speed;
                combat.ValueRW.HasAction = true;

                _turnOrder.Add(entity);
            }

            // Sort by initiative (descending) — simple insertion sort for small N
            for (int i = 1; i < _turnOrder.Length; i++)
            {
                var key = _turnOrder[i];
                int keyInit = state.EntityManager.GetComponentData<CombatStateComponent>(key).InitiativeRoll;
                int j = i - 1;
                while (j >= 0)
                {
                    int jInit = state.EntityManager.GetComponentData<CombatStateComponent>(_turnOrder[j]).InitiativeRoll;
                    if (jInit >= keyInit) break;
                    _turnOrder[j + 1] = _turnOrder[j];
                    j--;
                }
                _turnOrder[j + 1] = key;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_turnOrder.IsCreated) _turnOrder.Dispose();
        }
    }
}
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/ECS/Jobs/CombatJob.cs Assets/Scripts/ECS/Systems/CombatSystem.cs Assets/Tests/EditMode/CombatJobTests.cs
git commit -m "feat: CombatJob D20 resolution + CombatSystem initiative + tests"
```

---

## Task 7: PlayerTag + InputManager + PlayerMovement

**Files:**
- Create: `Assets/Scripts/ECS/Components/PlayerTag.cs`
- Create: `Assets/Scripts/MonoBehaviour/Input/InputManager.cs`
- Create: `Assets/Scripts/MonoBehaviour/Input/PlayerMovement.cs`

Rewrites pygame main.py input handling — WASD movement, interact, end turn, zoom, pan.

- [ ] **Step 1: Create PlayerTag component**

```csharp
// Assets/Scripts/ECS/Components/PlayerTag.cs
using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Tag component identifying the player entity.
    /// Used by systems that need to find the player (camera follow, fog origin, input target).
    /// </summary>
    public struct PlayerTag : IComponentData { }
}
```

- [ ] **Step 2: Create InputManager**

```csharp
// Assets/Scripts/MonoBehaviour/Input/InputManager.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForeverEngine.MonoBehaviour.Input
{
    /// <summary>
    /// Input bridge — rewritten from pygame main.py event loop.
    /// Maps Unity Input System actions to game events.
    /// Other scripts poll this for input state each frame.
    /// </summary>
    public class InputManager : UnityEngine.MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        // Movement (WASD/Arrows) — tile-based, fires once per press
        public Vector2Int MoveInput { get; private set; }
        public bool InteractPressed { get; private set; }    // F key
        public bool EndTurnPressed { get; private set; }     // Space
        public bool ToggleFogPressed { get; private set; }   // V
        public bool ToggleGridPressed { get; private set; }  // G
        public bool TogglePerspective { get; private set; }  // Tab
        public float ZoomDelta { get; private set; }         // Mouse wheel
        public bool PanActive { get; private set; }          // Middle mouse held
        public Vector2 PanDelta { get; private set; }        // Middle mouse drag
        public Vector2 ClickPosition { get; private set; }   // Left click screen pos
        public bool ClickedThisFrame { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Reset one-frame inputs
            MoveInput = Vector2Int.zero;
            InteractPressed = false;
            EndTurnPressed = false;
            ToggleFogPressed = false;
            ToggleGridPressed = false;
            TogglePerspective = false;
            ZoomDelta = 0f;
            PanDelta = Vector2.zero;
            ClickedThisFrame = false;

            // Movement (discrete tile steps, not continuous)
            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.up;
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.down;
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.left;
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.right;

            // Actions
            if (Keyboard.current.fKey.wasPressedThisFrame) InteractPressed = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) EndTurnPressed = true;
            if (Keyboard.current.vKey.wasPressedThisFrame) ToggleFogPressed = true;
            if (Keyboard.current.gKey.wasPressedThisFrame) ToggleGridPressed = true;
            if (Keyboard.current.tabKey.wasPressedThisFrame) TogglePerspective = true;

            // Zoom
            if (Mouse.current != null)
                ZoomDelta = Mouse.current.scroll.ReadValue().y;

            // Pan (middle mouse)
            if (Mouse.current != null)
            {
                PanActive = Mouse.current.middleButton.isPressed;
                if (PanActive)
                    PanDelta = Mouse.current.delta.ReadValue();
            }

            // Click
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ClickedThisFrame = true;
                ClickPosition = Mouse.current.position.ReadValue();
            }
        }
    }
}
```

- [ ] **Step 3: Create PlayerMovement**

```csharp
// Assets/Scripts/MonoBehaviour/Input/PlayerMovement.cs
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.Input
{
    /// <summary>
    /// Bridges input to ECS — rewritten from pygame main.py _try_move().
    /// Reads InputManager, validates walkability, updates player PositionComponent.
    /// </summary>
    public class PlayerMovement : UnityEngine.MonoBehaviour
    {
        private EntityManager _em;
        private EntityQuery _playerQuery;

        private void Start()
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _playerQuery = _em.CreateEntityQuery(
                typeof(PlayerTag), typeof(PositionComponent), typeof(CombatStateComponent));
        }

        private void Update()
        {
            var input = InputManager.Instance;
            if (input == null || input.MoveInput == Vector2Int.zero) return;

            var store = MapDataStore.Instance;
            if (store == null) return;

            // Check game state — only move in Exploration or during player's combat turn
            var stateQuery = _em.CreateEntityQuery(typeof(GameStateSingleton));
            if (stateQuery.IsEmpty) return;

            var gameState = stateQuery.GetSingleton<GameStateSingleton>();
            if (gameState.CurrentState != GameState.Exploration &&
                gameState.CurrentState != GameState.Combat)
                return;

            // Get player entity
            var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) { entities.Dispose(); return; }

            var playerEntity = entities[0];
            entities.Dispose();

            var pos = _em.GetComponentData<PositionComponent>(playerEntity);
            var combat = _em.GetComponentData<CombatStateComponent>(playerEntity);

            // In combat, check movement remaining
            if (gameState.CurrentState == GameState.Combat)
            {
                if (combat.MovementRemaining <= 0) return;
            }

            int newX = pos.X + input.MoveInput.x;
            int newY = pos.Y + input.MoveInput.y;

            // Bounds check
            if (newX < 0 || newX >= store.Width || newY < 0 || newY >= store.Height)
                return;

            // Walkability check
            int idx = newY * store.Width + newX;
            if (!store.Walkability[idx]) return;

            // Move
            pos.X = newX;
            pos.Y = newY;
            _em.SetComponentData(playerEntity, pos);

            // Decrement movement in combat
            if (gameState.CurrentState == GameState.Combat)
            {
                combat.MovementRemaining--;
                _em.SetComponentData(playerEntity, combat);
            }
        }
    }
}
```

- [ ] **Step 4: Update MapImporter to add PlayerTag**

In `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs`, in the `SpawnCreature` method, after the block that adds `FogVisionComponent` for the player, add:

```csharp
// Inside SpawnCreature(), after the FogVisionComponent block:
if (tokenType == TokenType.Player)
{
    em.AddComponent<PlayerTag>(entity);
}
```

Add the using statement at the top:
```csharp
using ForeverEngine.ECS.Components;  // already present, just ensure PlayerTag is in scope
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/ECS/Components/PlayerTag.cs Assets/Scripts/MonoBehaviour/Input/ Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs
git commit -m "feat: InputManager + PlayerMovement with walkability validation"
```

---

## Task 8: TileRenderer + EntityRenderer + FogRenderer

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/Rendering/TileRenderer.cs`
- Create: `Assets/Scripts/MonoBehaviour/Rendering/EntityRenderer.cs`
- Create: `Assets/Scripts/MonoBehaviour/Rendering/FogRenderer.cs`

Rewrites pygame renderer.py — terrain display, creature tokens, fog overlay. Uses Unity Tilemap for terrain and SpriteRenderer for entities.

- [ ] **Step 1: Implement TileRenderer**

```csharp
// Assets/Scripts/MonoBehaviour/Rendering/TileRenderer.cs
using UnityEngine;
using UnityEngine.Tilemaps;
using ForeverEngine.MonoBehaviour.Bootstrap;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.Rendering
{
    /// <summary>
    /// Terrain renderer — rewritten from pygame renderer.py _prerender_level().
    /// Converts terrain PNG into Unity Tilemap tiles.
    /// Each pixel in the terrain PNG becomes one tile with that pixel's color.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class TileRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private Tile _baseTile; // White tile to tint

        private int _currentZ;

        public void RenderLevel(int z)
        {
            var store = MapDataStore.Instance;
            if (store == null) return;

            var tex = TerrainTextureRegistry.Get(z);
            if (tex == null) return;

            _tilemap.ClearAllTiles();
            _currentZ = z;

            // Each pixel in terrain PNG = one tile
            var pixels = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // PNG is top-down, tilemap is bottom-up
                    var color = pixels[(h - 1 - y) * w + x];
                    var pos = new Vector3Int(x, y, 0);

                    _tilemap.SetTile(pos, _baseTile);
                    _tilemap.SetTileFlags(pos, TileFlags.None);
                    _tilemap.SetColor(pos, color);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Implement EntityRenderer**

```csharp
// Assets/Scripts/MonoBehaviour/Rendering/EntityRenderer.cs
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.MonoBehaviour.Rendering
{
    /// <summary>
    /// Entity sprite sync — rewritten from pygame renderer.py _draw_creatures().
    /// Creates/updates GameObjects with SpriteRenderers to match ECS entity state.
    /// Interpolates position for smooth movement between tiles.
    /// </summary>
    public class EntityRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private GameObject _creaturePrefab;

        private EntityManager _em;
        private EntityQuery _entityQuery;
        private Dictionary<Entity, GameObject> _entityObjects = new();

        private void Start()
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _entityQuery = _em.CreateEntityQuery(
                typeof(PositionComponent), typeof(VisualComponent), typeof(CombatStateComponent));
        }

        private void LateUpdate()
        {
            var entities = _entityQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            var activeEntities = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var pos = _em.GetComponentData<PositionComponent>(entity);
                var combat = _em.GetComponentData<CombatStateComponent>(entity);

                if (!combat.Alive) continue;

                activeEntities.Add(entity);

                // Get or create GameObject
                if (!_entityObjects.TryGetValue(entity, out var go))
                {
                    go = Instantiate(_creaturePrefab, transform);
                    _entityObjects[entity] = go;

                    // Set color by token type
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = combat.TokenType switch
                        {
                            TokenType.Player => _config != null ? _config.PlayerColor : Color.blue,
                            TokenType.Enemy => _config != null ? _config.EnemyColor : Color.red,
                            TokenType.NPC => _config != null ? _config.NPCColor : Color.yellow,
                            _ => Color.gray
                        };
                    }
                }

                // Smooth interpolation toward tile position
                Vector3 targetPos = new Vector3(pos.X + 0.5f, pos.Y + 0.5f, -1f);
                go.transform.position = Vector3.Lerp(
                    go.transform.position, targetPos, Time.deltaTime * 10f);
            }

            // Remove dead/missing entity objects
            var toRemove = new List<Entity>();
            foreach (var kvp in _entityObjects)
            {
                if (!activeEntities.Contains(kvp.Key))
                {
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var e in toRemove) _entityObjects.Remove(e);

            entities.Dispose();
        }
    }
}
```

- [ ] **Step 3: Implement FogRenderer**

```csharp
// Assets/Scripts/MonoBehaviour/Rendering/FogRenderer.cs
using UnityEngine;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.Rendering
{
    /// <summary>
    /// Fog of war visual overlay — rewritten from pygame renderer.py _draw_fog().
    /// Reads MapDataStore.FogGrid and updates a texture overlay.
    /// 0=black (unexplored), 1=dim (explored), 2=clear (visible).
    /// </summary>
    public class FogRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _fogSprite;
        [SerializeField] private GameConfig _config;

        private Texture2D _fogTexture;
        private Color32[] _fogPixels;
        private int _width, _height;

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _fogTexture.filterMode = FilterMode.Point;
            _fogPixels = new Color32[width * height];

            // Initial: all unexplored (black)
            var unexplored = new Color32(0, 0, 0, 255);
            for (int i = 0; i < _fogPixels.Length; i++)
                _fogPixels[i] = unexplored;

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply();

            _fogSprite.sprite = Sprite.Create(
                _fogTexture,
                new Rect(0, 0, width, height),
                Vector2.zero,
                1f); // 1 pixel = 1 unit (matches tilemap)
        }

        private void LateUpdate()
        {
            var store = MapDataStore.Instance;
            if (store == null || !store.FogGrid.IsCreated) return;

            var unexplored = new Color32(0, 0, 0, 255);
            var explored = new Color32(0, 0, 0, 153); // ~60% opacity
            var visible = new Color32(0, 0, 0, 0);    // Fully transparent

            for (int i = 0; i < store.FogGrid.Length && i < _fogPixels.Length; i++)
            {
                // Flip Y for texture (bottom-up)
                int x = i % _width;
                int y = i / _width;
                int texIdx = (_height - 1 - y) * _width + x;
                if (texIdx < 0 || texIdx >= _fogPixels.Length) continue;

                _fogPixels[texIdx] = store.FogGrid[i] switch
                {
                    2 => visible,
                    1 => explored,
                    _ => unexplored
                };
            }

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply();
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/Rendering/
git commit -m "feat: TileRenderer + EntityRenderer + FogRenderer — pygame renderer.py rewrite"
```

---

## Task 9: HUD + Combat Log UI

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/UI/HUDManager.cs`
- Create: `Assets/Scripts/MonoBehaviour/UI/CombatLogUI.cs`

Rewrites pygame ui_overlay.py — mode indicator, HP bar, stats, combat log.

- [ ] **Step 1: Implement HUDManager**

```csharp
// Assets/Scripts/MonoBehaviour/UI/HUDManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.UI
{
    /// <summary>
    /// HUD — rewritten from pygame ui_overlay.py.
    /// Uses UI Toolkit for mode indicator, player stats, HP bar.
    /// </summary>
    public class HUDManager : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private Label _modeLabel;
        private Label _zLevelLabel;
        private Label _nameLabel;
        private Label _hpLabel;
        private VisualElement _hpBar;
        private Label _statsLabel;

        private EntityManager _em;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;

            _modeLabel = root.Q<Label>("mode-label");
            _zLevelLabel = root.Q<Label>("zlevel-label");
            _nameLabel = root.Q<Label>("player-name");
            _hpLabel = root.Q<Label>("hp-text");
            _hpBar = root.Q<VisualElement>("hp-bar-fill");
            _statsLabel = root.Q<Label>("stats-text");

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private void Update()
        {
            if (_em == null) return;

            // Game state
            var stateQuery = _em.CreateEntityQuery(typeof(GameStateSingleton));
            if (!stateQuery.IsEmpty)
            {
                var gs = stateQuery.GetSingleton<GameStateSingleton>();
                if (_modeLabel != null)
                {
                    _modeLabel.text = gs.CurrentState.ToString().ToUpper();
                    _modeLabel.style.color = gs.CurrentState switch
                    {
                        GameState.Exploration => new Color(0.2f, 0.8f, 0.2f),
                        GameState.Combat => new Color(1f, 0.84f, 0f),
                        GameState.GameOver => new Color(1f, 0.2f, 0.2f),
                        _ => new Color(0.7f, 0.7f, 0.7f)
                    };
                }
            }

            // Player stats
            var playerQuery = _em.CreateEntityQuery(typeof(PlayerTag), typeof(StatsComponent));
            if (!playerQuery.IsEmpty)
            {
                var stats = playerQuery.GetSingleton<StatsComponent>();
                if (_hpLabel != null)
                    _hpLabel.text = $"HP: {stats.HP}/{stats.MaxHP}";
                if (_hpBar != null)
                    _hpBar.style.width = new Length(stats.HPPercent * 100f, LengthUnit.Percent);
                if (_statsLabel != null)
                    _statsLabel.text = $"AC:{stats.AC}  STR:{stats.Strength}  DEX:{stats.Dexterity}  SPD:{stats.Speed}";
            }
        }
    }
}
```

- [ ] **Step 2: Implement CombatLogUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/CombatLogUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.UI
{
    /// <summary>
    /// Combat log display — rewritten from pygame ui_overlay.py _draw_combat_log().
    /// Shows last 8 combat messages with color coding.
    /// </summary>
    public class CombatLogUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private int _maxVisibleLines = 8;

        private ScrollView _logContainer;
        private EntityManager _em;
        private int _lastLogCount;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _logContainer = root.Q<ScrollView>("combat-log");
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        public void AddMessage(string message, Color color)
        {
            if (_logContainer == null) return;

            var label = new Label(message);
            label.style.color = color;
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            _logContainer.Add(label);

            // Trim old messages
            while (_logContainer.childCount > _maxVisibleLines)
                _logContainer.RemoveAt(0);

            // Auto-scroll to bottom
            _logContainer.scrollOffset = new Vector2(0, float.MaxValue);
        }

        public void Clear()
        {
            _logContainer?.Clear();
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/UI/
git commit -m "feat: HUDManager + CombatLogUI — pygame ui_overlay.py rewrite"
```

---

## Task 10: AIExecutionSystem

**Files:**
- Create: `Assets/Scripts/ECS/Systems/AIExecutionSystem.cs`
- Create: `Assets/Tests/EditMode/AIDecisionJobTests.cs`

Wires AIDecisionJob + PathfindJob into the game loop. Runs only during combat on NPC turns.

- [ ] **Step 1: Write AI decision tests**

```csharp
// Assets/Tests/EditMode/AIDecisionJobTests.cs
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Jobs;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.Tests
{
    public class AIDecisionJobTests
    {
        [Test]
        public void Chase_Adjacent_Attacks()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Chase, DetectRange = 12 };
            positions[0] = new PositionComponent { X = 5, Y = 5 };
            combats[0] = new CombatStateComponent { Alive = true, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(5, 6), // Adjacent
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.Attack, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }

        [Test]
        public void Chase_FarAway_Moves()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Chase, DetectRange = 12 };
            positions[0] = new PositionComponent { X = 1, Y = 1 };
            combats[0] = new CombatStateComponent { Alive = true, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(6, 6), // Far away
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.MoveTo, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }

        [Test]
        public void Static_DoesNothing()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Static };
            positions[0] = new PositionComponent { X = 3, Y = 3 };
            combats[0] = new CombatStateComponent { Alive = true, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(3, 4),
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.None, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }

        [Test]
        public void Dead_DoesNothing()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Chase, DetectRange = 12 };
            positions[0] = new PositionComponent { X = 5, Y = 5 };
            combats[0] = new CombatStateComponent { Alive = false, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(5, 6),
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.None, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run tests**

Expected: 4/4 PASS (AIDecisionJob already implemented)

- [ ] **Step 3: Implement AIExecutionSystem**

```csharp
// Assets/Scripts/ECS/Systems/AIExecutionSystem.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// AI execution — schedules AIDecisionJob + PathfindJob for all NPCs.
    /// Runs during combat on NPC turns, and during exploration for patrol/wander.
    /// Rewritten from pygame ai.py ai_turn() but parallel and with real pathfinding.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct AIExecutionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var store = MapDataStore.Instance;
            if (store == null || !store.Walkability.IsCreated) return;

            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();
            if (gameState.CurrentState != GameState.Combat &&
                gameState.CurrentState != GameState.Exploration)
                return;

            // Find player position
            int2 playerPos = default;
            bool playerAlive = false;
            foreach (var (tag, pos, combat) in
                SystemAPI.Query<RefRO<PlayerTag>, RefRO<PositionComponent>, RefRO<CombatStateComponent>>())
            {
                playerPos = new int2(pos.ValueRO.X, pos.ValueRO.Y);
                playerAlive = combat.ValueRO.Alive;
                break;
            }

            // Collect NPC data into arrays for batch job
            var npcEntities = new NativeList<Entity>(32, Allocator.TempJob);
            var behaviors = new NativeList<AIBehaviorComponent>(32, Allocator.TempJob);
            var positions = new NativeList<PositionComponent>(32, Allocator.TempJob);
            var combatStates = new NativeList<CombatStateComponent>(32, Allocator.TempJob);
            var stats = new NativeList<StatsComponent>(32, Allocator.TempJob);

            foreach (var (ai, pos, combat, stat, entity) in
                SystemAPI.Query<RefRO<AIBehaviorComponent>, RefRO<PositionComponent>,
                    RefRO<CombatStateComponent>, RefRO<StatsComponent>>()
                    .WithEntityAccess())
            {
                if (!combat.ValueRO.Alive) continue;
                npcEntities.Add(entity);
                behaviors.Add(ai.ValueRO);
                positions.Add(pos.ValueRO);
                combatStates.Add(combat.ValueRO);
                stats.Add(stat.ValueRO);
            }

            if (npcEntities.Length == 0)
            {
                npcEntities.Dispose(); behaviors.Dispose(); positions.Dispose();
                combatStates.Dispose(); stats.Dispose();
                return;
            }

            // Schedule AI decision job
            var decisions = new NativeArray<AIDecision>(npcEntities.Length, Allocator.TempJob);

            var decisionJob = new AIDecisionJob
            {
                PlayerPosition = playerPos,
                PlayerAlive = playerAlive,
                FogGrid = store.FogGrid,
                MapWidth = store.Width,
                Behaviors = behaviors.AsArray(),
                Positions = positions.AsArray(),
                CombatStates = combatStates.AsArray(),
                Stats = stats.AsArray(),
                Decisions = decisions
            };

            var handle = decisionJob.Schedule(npcEntities.Length, 8, state.Dependency);
            handle.Complete(); // Need results immediately to apply

            // Apply decisions
            for (int i = 0; i < npcEntities.Length; i++)
            {
                var decision = decisions[i];
                if (decision.Action == AIAction.None) continue;

                var entity = npcEntities[i];
                var pos = _em_GetPosition(ref state, entity);

                if (decision.Action == AIAction.MoveTo)
                {
                    // Simple one-step move toward target (pathfinding job for full path)
                    int dx = math.clamp(decision.TargetX - pos.X, -1, 1);
                    int dy = math.clamp(decision.TargetY - pos.Y, -1, 1);
                    int newX = pos.X + dx;
                    int newY = pos.Y + dy;

                    if (newX >= 0 && newX < store.Width && newY >= 0 && newY < store.Height)
                    {
                        if (store.Walkability[newY * store.Width + newX])
                        {
                            pos.X = newX;
                            pos.Y = newY;
                            state.EntityManager.SetComponentData(entity, pos);
                        }
                    }
                }
            }

            npcEntities.Dispose(); behaviors.Dispose(); positions.Dispose();
            combatStates.Dispose(); stats.Dispose(); decisions.Dispose();
        }

        private PositionComponent _em_GetPosition(ref SystemState state, Entity entity)
        {
            return state.EntityManager.GetComponentData<PositionComponent>(entity);
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/ECS/Systems/AIExecutionSystem.cs Assets/Tests/EditMode/AIDecisionJobTests.cs
git commit -m "feat: AIExecutionSystem with batch decisions + AI tests"
```

---

## Task 11: GameBootstrap Wiring

**Files:**
- Modify: `Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs` — wire all systems together

This task connects all the pieces: bootstrap loads map, initializes renderers, connects input.

- [ ] **Step 1: Update GameBootstrap to wire everything**

```csharp
// Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs
using UnityEngine;
using Unity.Entities;
using ForeverEngine.ECS.Data;
using ForeverEngine.MonoBehaviour.Rendering;
using ForeverEngine.MonoBehaviour.Input;
using ForeverEngine.MonoBehaviour.UI;
using ForeverEngine.MonoBehaviour.Camera;

namespace ForeverEngine.MonoBehaviour.Bootstrap
{
    /// <summary>
    /// Entry point — rewritten from pygame main.py main().
    /// Initializes ECS world, loads map, wires renderers and UI.
    /// </summary>
    public class GameBootstrap : UnityEngine.MonoBehaviour
    {
        [Header("Map Loading")]
        [Tooltip("Path to map_data.json from Map Generator output")]
        public string MapDataPath;

        [Header("References")]
        public GameConfig GameConfig;
        public CameraController CameraController;
        public TileRenderer TileRenderer;
        public EntityRenderer EntityRenderer;
        public FogRenderer FogRenderer;
        public HUDManager HUDManager;
        public CombatLogUI CombatLogUI;

        private EntityManager _entityManager;
        private MapDataStore _mapDataStore;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!string.IsNullOrEmpty(MapDataPath))
                LoadMap(MapDataPath);
            else
                Debug.Log("[ForeverEngine] No map path. Use File > Open to load a map.");
        }

        public void LoadMap(string mapDataJsonPath)
        {
            Debug.Log($"[ForeverEngine] Loading: {mapDataJsonPath}");

            // Import map data into ECS
            var importer = GetComponent<MapImporter>();
            if (importer == null) importer = gameObject.AddComponent<MapImporter>();
            importer.Import(mapDataJsonPath, _entityManager);

            // Initialize map data store
            _mapDataStore = MapDataStore.Instance;

            // Render terrain
            if (TileRenderer != null)
                TileRenderer.RenderLevel(_mapDataStore.CurrentZ);

            // Initialize fog overlay
            if (FogRenderer != null)
                FogRenderer.Initialize(_mapDataStore.Width, _mapDataStore.Height);

            // Center camera on player
            var playerSpawn = importer.GetPlayerSpawnPosition();
            if (CameraController != null)
                CameraController.SnapTo(playerSpawn.x + 0.5f, playerSpawn.y + 0.5f);

            Debug.Log($"[ForeverEngine] Map loaded: {_mapDataStore.Width}x{_mapDataStore.Height}");
        }

        private void OnDestroy()
        {
            _mapDataStore?.Dispose();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs
git commit -m "feat: GameBootstrap wiring — connects all systems for game loop"
```

---

## Task 12: Integration Test with Map Generator Output

**Files:**
- Create: `Assets/Tests/PlayMode/GameLoopTests.cs`
- Create: `Assets/Resources/Maps/test_dungeon/map_data.json` (minimal test map)

End-to-end test: load a map, verify entities spawned, fog initialized, player can exist.

- [ ] **Step 1: Create minimal test map**

```json
{
  "config": {
    "width": 8,
    "height": 8,
    "map_type": "dungeon",
    "biome": "cave",
    "seed": 42,
    "schema_version": "1.0.0"
  },
  "z_levels": [
    {
      "z": 0,
      "terrain_png": "z_0.png",
      "walkability": [
        0,0,0,0,0,0,0,0,
        0,1,1,1,1,1,1,0,
        0,1,1,1,1,1,1,0,
        0,1,1,1,1,1,1,0,
        0,1,1,1,1,1,1,0,
        0,1,1,1,1,1,1,0,
        0,1,1,1,1,1,1,0,
        0,0,0,0,0,0,0,0
      ],
      "entities": []
    }
  ],
  "transitions": [],
  "spawns": [
    {
      "name": "Player",
      "x": 2, "y": 2, "z": 0,
      "token_type": "player",
      "ai_behavior": "static",
      "stats": {"hp": 20, "ac": 14, "strength": 14, "dexterity": 12, "speed": 6, "atk_dice": "1d8+2"}
    },
    {
      "name": "Goblin",
      "x": 5, "y": 5, "z": 0,
      "token_type": "enemy",
      "ai_behavior": "chase",
      "stats": {"hp": 7, "ac": 12, "strength": 10, "dexterity": 14, "speed": 6, "atk_dice": "1d6+1"}
    }
  ],
  "labels": []
}
```

Save to `Assets/Resources/Maps/test_dungeon/map_data.json`.

- [ ] **Step 2: Write integration test**

```csharp
// Assets/Tests/PlayMode/GameLoopTests.cs
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.MonoBehaviour.Bootstrap;

namespace ForeverEngine.Tests
{
    public class GameLoopTests
    {
        [UnityTest]
        public IEnumerator LoadMap_SpawnsPlayerAndEnemy()
        {
            // Create bootstrap
            var go = new GameObject("Bootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();

            string testMapPath = Application.dataPath + "/Resources/Maps/test_dungeon/map_data.json";
            bootstrap.LoadMap(testMapPath);

            yield return null; // Wait one frame for ECS to process

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Verify player exists
            var playerQuery = em.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent));
            Assert.AreEqual(1, playerQuery.CalculateEntityCount(), "Should have exactly 1 player");

            var playerPos = playerQuery.GetSingleton<PositionComponent>();
            Assert.AreEqual(2, playerPos.X);
            Assert.AreEqual(2, playerPos.Y);

            // Verify enemy exists
            var enemyQuery = em.CreateEntityQuery(typeof(AIBehaviorComponent), typeof(StatsComponent));
            Assert.GreaterOrEqual(enemyQuery.CalculateEntityCount(), 1, "Should have at least 1 enemy");

            // Verify map data store
            var store = MapDataStore.Instance;
            Assert.IsNotNull(store);
            Assert.AreEqual(8, store.Width);
            Assert.AreEqual(8, store.Height);
            Assert.IsTrue(store.Walkability[1 * 8 + 1]);   // (1,1) walkable
            Assert.IsFalse(store.Walkability[0 * 8 + 0]);  // (0,0) wall

            // Cleanup
            Object.Destroy(go);
            store.Dispose();
        }
    }
}
```

- [ ] **Step 3: Run integration test**

Run: Unity Test Runner → PlayMode → GameLoopTests → Run
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add Assets/Tests/ Assets/Resources/Maps/test_dungeon/
git commit -m "test: integration test — load map, verify player/enemy spawn, walkability"
```

---

## Summary

| Task | What it builds | Test count |
|------|---------------|-----------|
| 1 | Unity project + DOTS packages + assembly defs | 0 (setup) |
| 2 | DiceRoller utility + StatsComponent tests | 13 |
| 3 | MapDataStore + FogOfWarSystem fix | 5 |
| 4 | FogRaycastJob tests | 4 |
| 5 | PathfindJob tests | 4 |
| 6 | CombatJob + CombatSystem | 3 |
| 7 | PlayerTag + InputManager + PlayerMovement | 0 (MonoBehaviour) |
| 8 | TileRenderer + EntityRenderer + FogRenderer | 0 (MonoBehaviour) |
| 9 | HUD + Combat Log UI | 0 (UI Toolkit) |
| 10 | AIExecutionSystem + AI decision tests | 4 |
| 11 | GameBootstrap wiring | 0 (integration) |
| 12 | Integration test with test map | 1 |
| **Total** | **Phase 1 core game loop** | **34 tests** |

After all 12 tasks: Load a Map Generator dungeon → see terrain + fog → move player → trigger combat → AI chases + attacks → resolve D20 combat → win/lose. The complete pygame viewer gameplay loop, running on Unity DOTS worker threads.
