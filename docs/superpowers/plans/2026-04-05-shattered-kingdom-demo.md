# Shattered Kingdom Demo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable 30-minute vertical slice demo — hex overworld with survival, location discovery, turn-based RPG combat, 4-quest chain, and full AI integration.

**Architecture:** Two Unity scenes (Overworld + BattleMap) connected by a GameManager singleton that persists across scene loads. Overworld uses the existing HexGrid + SurvivalSystem. BattleMap uses the existing ECS combat loop. New code wires these together with overworld generation, encounter triggers, scene transitions, quest progression, NPC interaction, and a shop system.

**Tech Stack:** Unity 6+ DOTS, existing ForeverEngine systems, SceneManager for transitions, ScriptableObject for game data

**Spec:** `docs/superpowers/specs/2026-04-05-shattered-kingdom-demo.md`

---

## File Map

### Task 1 — Game Manager (persistent singleton across scenes)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/GameManager.cs` | Persistent game state: player data, quest state, scene transitions |
| `Assets/Scripts/Demo/PlayerData.cs` | Player stats, inventory, position, survival meters |

### Task 2 — Overworld Generation

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Overworld/OverworldGenerator.cs` | Seed-based 20x20 hex terrain + location placement |
| `Assets/Scripts/Demo/Overworld/OverworldManager.cs` | Overworld scene controller: input, movement, encounter checks |
| `Assets/Scripts/Demo/Overworld/HexTileVisual.cs` | Visual representation of each hex tile |

### Task 3 — Overworld Player + Movement + Fog

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Overworld/OverworldPlayer.cs` | Player token on hex grid, move tile-to-tile, trigger survival drain |
| `Assets/Scripts/Demo/Overworld/OverworldFog.cs` | Hex-based fog of war (reveal radius around player) |

### Task 4 — Random Encounters + Scene Transition

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Encounters/EncounterManager.cs` | Random encounter rolls, encounter data, transition to BattleMap |
| `Assets/Scripts/Demo/Encounters/EncounterData.cs` | Encounter definitions: enemy types, battle map size, loot tables |

### Task 5 — Battle Map Scene

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Battle/BattleManager.cs` | Battle scene controller: init from encounter data, run combat, return results |
| `Assets/Scripts/Demo/Battle/BattleGrid.cs` | Small tile grid for tactical combat positioning |
| `Assets/Scripts/Demo/Battle/BattleCombatant.cs` | Turn-based combatant: player + enemies on the battle grid |

### Task 6 — NPC Dialogue + Shop

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/NPCs/NPCManager.cs` | NPC definitions, dialogue tree registration, shop inventory |
| `Assets/Scripts/Demo/NPCs/ShopSystem.cs` | Buy/sell items with gold |

### Task 7 — Quest Chain

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Quests/DemoQuests.cs` | All 4 quest definitions, triggers, rewards |

### Task 8 — Locations (Camp, Town, Dungeon, Fortress, Castle)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Locations/LocationData.cs` | Location definitions: type, position, interior config |
| `Assets/Scripts/Demo/Locations/LocationManager.cs` | Enter/exit locations, generate interiors via PipelineCoordinator |

### Task 9 — UI (HUD, menus, victory screen)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/UI/OverworldHUD.cs` | Survival bars, minimap, quest tracker, day/night indicator |
| `Assets/Scripts/Demo/UI/BattleHUD.cs` | Turn order, HP bars, action buttons |
| `Assets/Scripts/Demo/UI/DemoMainMenu.cs` | Title screen: new game, continue, quit |
| `Assets/Scripts/Demo/UI/VictoryScreen.cs` | End game display |
| `Assets/Scripts/Demo/UI/LootScreen.cs` | Post-combat loot display |

### Task 10 — Scene Setup + Integration

| File | Responsibility |
|------|---------------|
| `Assets/Editor/DemoSceneBuilder.cs` | Programmatically creates Overworld + BattleMap + MainMenu scenes |

---

## Task 1: GameManager + PlayerData

**Files:**
- Create: `Assets/Scripts/Demo/PlayerData.cs`
- Create: `Assets/Scripts/Demo/GameManager.cs`

- [ ] **Step 1: Implement PlayerData**

```csharp
// Assets/Scripts/Demo/PlayerData.cs
using System.Collections.Generic;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Demo
{
    [System.Serializable]
    public class PlayerData
    {
        // Position on hex grid
        public int HexQ, HexR;

        // D&D Stats
        public int HP = 20, MaxHP = 20, AC = 12;
        public int Strength = 14, Dexterity = 12, Constitution = 12;
        public int Speed = 6;
        public string AttackDice = "1d8+2";
        public int Level = 1;

        // Survival
        public float Hunger = 100, Thirst = 100;
        public float MaxHunger = 100, MaxThirst = 100;

        // Economy
        public int Gold = 10;
        public Inventory Inventory;

        // Progress
        public int DayCount = 1;
        public HashSet<string> ExploredHexes = new();
        public HashSet<string> DiscoveredLocations = new();
        public string LastSafeLocation = "camp";

        // Equipped
        public string WeaponName = "Rusty Sword";
        public string ArmorName = "Leather Armor";

        public PlayerData()
        {
            Inventory = new Inventory(20);
            // Starting items
            Inventory.Add(new ItemInstance { ItemId = 100, StackCount = 3, MaxStack = 10 }); // Food
            Inventory.Add(new ItemInstance { ItemId = 101, StackCount = 3, MaxStack = 10 }); // Water
        }

        public bool IsAlive => HP > 0;
        public bool IsStarving => Hunger <= 0;
        public bool IsDehydrated => Thirst <= 0;
        public float HungerPercent => Hunger / MaxHunger;
        public float ThirstPercent => Thirst / MaxThirst;
        public float HPPercent => MaxHP > 0 ? (float)HP / MaxHP : 0;

        public void DrainHunger(float amount) { Hunger = System.Math.Max(0, Hunger - amount); }
        public void DrainThirst(float amount) { Thirst = System.Math.Max(0, Thirst - amount); }
        public void Eat(float amount) { Hunger = System.Math.Min(MaxHunger, Hunger + amount); }
        public void Drink(float amount) { Thirst = System.Math.Min(MaxThirst, Thirst + amount); }
        public void Heal(int amount) { HP = System.Math.Min(MaxHP, HP + amount); }
        public void TakeDamage(int amount) { HP = System.Math.Max(0, HP - amount); }

        public void LevelUp()
        {
            Level++;
            MaxHP += 5;
            HP = MaxHP;
        }

        public void FullRest()
        {
            HP = MaxHP;
            Hunger = MaxHunger;
            Thirst = MaxThirst;
        }

        public string HexKey => $"{HexQ},{HexR}";
    }
}
```

- [ ] **Step 2: Implement GameManager**

```csharp
// Assets/Scripts/Demo/GameManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.AI.Memory;
using ForeverEngine.AI.Learning;
using ForeverEngine.AI.Director;
using ForeverEngine.AI.PlayerModeling;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Systems;
using ForeverEngine.MonoBehaviour.SaveLoad;

namespace ForeverEngine.Demo
{
    public class GameManager : UnityEngine.MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerData Player { get; private set; }
        public int CurrentSeed { get; private set; } = 42;
        public string PendingEncounterId { get; set; }
        public string PendingLocationId { get; set; }

        // Battle results passed back from BattleMap scene
        public bool LastBattleWon { get; set; }
        public int LastBattleGoldEarned { get; set; }
        public int LastBattleXPEarned { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void NewGame(int seed = 42)
        {
            CurrentSeed = seed;
            Player = new PlayerData { HexQ = 2, HexR = 2 }; // Start at camp
            Player.DiscoveredLocations.Add("camp");
            SceneManager.LoadScene("Overworld");
        }

        public void EnterBattle(string encounterId)
        {
            PendingEncounterId = encounterId;
            SceneManager.LoadScene("BattleMap");
        }

        public void EnterLocation(string locationId)
        {
            PendingLocationId = locationId;
            // Towns don't use BattleMap — handled in Overworld via dialogue overlay
            if (locationId == "town" || locationId == "camp" || locationId == "fortress")
                return; // Dialogue handled in overworld
            SceneManager.LoadScene("BattleMap");
        }

        public void ReturnToOverworld()
        {
            PendingEncounterId = null;
            PendingLocationId = null;
            SceneManager.LoadScene("Overworld");
        }

        public void PlayerDied()
        {
            Player.HP = Player.MaxHP / 2;
            Player.Hunger = 50;
            Player.Thirst = 50;
            // Respawn at last safe location
            var loc = LocationData.Get(Player.LastSafeLocation);
            if (loc != null) { Player.HexQ = loc.HexQ; Player.HexR = loc.HexR; }
            ReturnToOverworld();
        }

        public void GameComplete()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/
git commit -m "feat: GameManager + PlayerData — persistent game state across scenes"
```

---

## Task 2: Overworld Generation

**Files:**
- Create: `Assets/Scripts/Demo/Overworld/OverworldGenerator.cs`
- Create: `Assets/Scripts/Demo/Locations/LocationData.cs`

- [ ] **Step 1: Implement LocationData**

```csharp
// Assets/Scripts/Demo/Locations/LocationData.cs
using System.Collections.Generic;

namespace ForeverEngine.Demo
{
    [System.Serializable]
    public class LocationData
    {
        public string Id;
        public string Name;
        public string Type; // camp, town, dungeon, fortress, castle
        public int HexQ, HexR;
        public bool IsSafe; // Can rest/save here
        public string MapType; // For PipelineCoordinator generation
        public int InteriorSize = 64;

        private static Dictionary<string, LocationData> _locations;

        public static LocationData Get(string id)
        {
            if (_locations == null) Init();
            return _locations.TryGetValue(id, out var loc) ? loc : null;
        }

        public static List<LocationData> GetAll()
        {
            if (_locations == null) Init();
            return new List<LocationData>(_locations.Values);
        }

        private static void Init()
        {
            _locations = new Dictionary<string, LocationData>
            {
                ["camp"] = new LocationData { Id = "camp", Name = "Survivor's Camp", Type = "camp", HexQ = 2, HexR = 2, IsSafe = true },
                ["town"] = new LocationData { Id = "town", Name = "Ashwick Ruins", Type = "town", HexQ = 8, HexR = 5, IsSafe = true, MapType = "village", InteriorSize = 64 },
                ["dungeon"] = new LocationData { Id = "dungeon", Name = "The Hollow", Type = "dungeon", HexQ = 12, HexR = 10, IsSafe = false, MapType = "dungeon", InteriorSize = 64 },
                ["fortress"] = new LocationData { Id = "fortress", Name = "Ironhold", Type = "fortress", HexQ = 5, HexR = 15, IsSafe = true, MapType = "castle", InteriorSize = 64 },
                ["castle"] = new LocationData { Id = "castle", Name = "Throne of Rot", Type = "castle", HexQ = 17, HexR = 17, IsSafe = false, MapType = "castle", InteriorSize = 96 }
            };
        }
    }
}
```

- [ ] **Step 2: Implement OverworldGenerator**

```csharp
// Assets/Scripts/Demo/Overworld/OverworldGenerator.cs
using System.Collections.Generic;
using ForeverEngine.Generation.Utility;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    public static class OverworldGenerator
    {
        public static Dictionary<(int,int), HexTile> Generate(int width, int height, int seed)
        {
            PerlinNoise.Seed(seed);
            var tiles = new Dictionary<(int,int), HexTile>();

            for (int q = 0; q < width; q++)
            {
                for (int r = 0; r < height; r++)
                {
                    float elevation = PerlinNoise.Octave(q * 0.15f, r * 0.15f, 4);
                    float moisture = PerlinNoise.Octave(q * 0.15f + 50f, r * 0.15f + 50f, 3);

                    TileType type;
                    if (elevation < 0.25f) type = TileType.Water;
                    else if (elevation < 0.4f) type = TileType.Plains;
                    else if (elevation < 0.6f) type = moisture > 0.5f ? TileType.Forest : TileType.Plains;
                    else if (elevation < 0.75f) type = TileType.Forest;
                    else type = TileType.Mountain;

                    // Scatter ruins on plains/forest
                    float ruinNoise = PerlinNoise.Sample(q * 0.3f + 200f, r * 0.3f + 200f);
                    if (ruinNoise > 0.75f && type != TileType.Water && type != TileType.Mountain)
                        type = TileType.Road; // Reuse Road as "Ruins" for demo

                    tiles[(q, r)] = new HexTile
                    {
                        Q = q, R = r, Type = type,
                        Height = (int)(elevation * 3),
                        MovementCost = HexGrid.GetMovementCost(type),
                        DefenseBonus = HexGrid.GetDefenseBonus(type)
                    };
                }
            }

            // Ensure location hexes are walkable plains
            foreach (var loc in LocationData.GetAll())
            {
                tiles[(loc.HexQ, loc.HexR)] = new HexTile
                {
                    Q = loc.HexQ, R = loc.HexR, Type = TileType.Road,
                    MovementCost = 0.5f, DefenseBonus = 0f
                };
            }

            // Ensure starting area is accessible (plains around camp)
            for (int dq = -1; dq <= 1; dq++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    var key = (2 + dq, 2 + dr);
                    if (tiles.ContainsKey(key) && tiles[key].Type == TileType.Water)
                        tiles[key] = new HexTile { Q = key.Item1, R = key.Item2, Type = TileType.Plains, MovementCost = 1f };
                }

            return tiles;
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/
git commit -m "feat: Overworld generator — 20x20 hex terrain with Perlin noise + 5 locations"
```

---

## Task 3: Overworld Player + Fog

**Files:**
- Create: `Assets/Scripts/Demo/Overworld/OverworldPlayer.cs`
- Create: `Assets/Scripts/Demo/Overworld/OverworldFog.cs`
- Create: `Assets/Scripts/Demo/Overworld/OverworldManager.cs`

- [ ] **Step 1: Implement all 3 files**

OverworldPlayer handles hex movement, survival drain, location detection.
OverworldFog tracks revealed hexes.
OverworldManager is the scene controller wiring everything together.

- [ ] **Step 2: Commit** `feat: Overworld scene — player movement, fog of war, survival drain`

---

## Task 4: Random Encounters + Scene Transition

**Files:**
- Create: `Assets/Scripts/Demo/Encounters/EncounterData.cs`
- Create: `Assets/Scripts/Demo/Encounters/EncounterManager.cs`

Encounter rolls on hostile terrain, generates encounter data, calls GameManager.EnterBattle().

- [ ] **Step 1: Implement both files**
- [ ] **Step 2: Commit** `feat: Random encounter system with terrain-based trigger rates`

---

## Task 5: Battle Map Scene

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleManager.cs`
- Create: `Assets/Scripts/Demo/Battle/BattleGrid.cs`
- Create: `Assets/Scripts/Demo/Battle/BattleCombatant.cs`

Turn-based combat on a small generated grid. Uses existing D&D dice, stats, AI decision systems.

- [ ] **Step 1: Implement all 3 files**
- [ ] **Step 2: Commit** `feat: Battle map scene — turn-based tactical combat with D&D mechanics`

---

## Task 6: NPC Dialogue + Shop

**Files:**
- Create: `Assets/Scripts/Demo/NPCs/NPCManager.cs`
- Create: `Assets/Scripts/Demo/NPCs/ShopSystem.cs`

Town NPCs with dialogue trees, merchant with buy/sell.

- [ ] **Step 1: Implement both files**
- [ ] **Step 2: Commit** `feat: NPC dialogue and shop system for town interactions`

---

## Task 7: Quest Chain

**Files:**
- Create: `Assets/Scripts/Demo/Quests/DemoQuests.cs`

Registers all 4 quests with the existing QuestSystem, wires triggers.

- [ ] **Step 1: Implement quest definitions and triggers**
- [ ] **Step 2: Commit** `feat: 4-quest chain — Signal Fire, Merchant's Plea, Dwarven Alliance, Cursed Throne`

---

## Task 8: Location Interiors

**Files:**
- Create: `Assets/Scripts/Demo/Locations/LocationManager.cs`

Generates interior maps for dungeon/castle using PipelineCoordinator, handles town/fortress as dialogue overlays.

- [ ] **Step 1: Implement LocationManager**
- [ ] **Step 2: Commit** `feat: Location system — dungeon/castle generation, town/fortress dialogue`

---

## Task 9: Demo UI

**Files:**
- Create: `Assets/Scripts/Demo/UI/OverworldHUD.cs`
- Create: `Assets/Scripts/Demo/UI/BattleHUD.cs`
- Create: `Assets/Scripts/Demo/UI/DemoMainMenu.cs`
- Create: `Assets/Scripts/Demo/UI/VictoryScreen.cs`
- Create: `Assets/Scripts/Demo/UI/LootScreen.cs`

All UI using Unity's IMGUI (OnGUI) for fast iteration — no UI Toolkit layout files needed.

- [ ] **Step 1: Implement all 5 UI files**
- [ ] **Step 2: Commit** `feat: Demo UI — overworld HUD, battle HUD, menus, loot and victory screens`

---

## Task 10: Scene Setup + Integration

**Files:**
- Create: `Assets/Editor/DemoSceneBuilder.cs`

Editor script that creates MainMenu, Overworld, and BattleMap scenes programmatically with all required GameObjects wired.

- [ ] **Step 1: Implement DemoSceneBuilder**
- [ ] **Step 2: Run DemoSceneBuilder in batch mode**
- [ ] **Step 3: Run full test suite to verify nothing broke**
- [ ] **Step 4: Commit** `feat: Demo scenes — MainMenu, Overworld, BattleMap wired and playable`

---

## Summary

| Task | What | New Files |
|------|------|-----------|
| 1 | GameManager + PlayerData | 2 |
| 2 | Overworld Generation + Locations | 2 |
| 3 | Overworld Player + Fog + Manager | 3 |
| 4 | Random Encounters | 2 |
| 5 | Battle Map Scene | 3 |
| 6 | NPC Dialogue + Shop | 2 |
| 7 | Quest Chain | 1 |
| 8 | Location Interiors | 1 |
| 9 | Demo UI (5 screens) | 5 |
| 10 | Scene Setup | 1 |
| **Total** | | **22 new files** |

After all 10 tasks: A player can start a new game, explore a hex overworld with fog/survival, encounter random enemies in turn-based combat, visit 5 locations, interact with NPCs, buy/sell items, complete 4 quests, defeat a boss, and see the victory screen.
