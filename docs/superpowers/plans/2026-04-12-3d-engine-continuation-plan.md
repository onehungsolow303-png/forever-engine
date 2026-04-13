# 3D Engine Continuation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace capsule tokens with 3D GLB models, polish battle AI with externalized config and encounter templates, and build explorable 3D dungeon interiors.

**Architecture:** Three independent phases execute in order. Phase 1 adds a static ModelRegistry lookup that wires 80+ imported GLB models into the existing BattleRenderer3D pipeline. Phase 2 externalizes Q-learning hyperparameters and reward values to GameConfig, adds encounter templates for mixed enemy compositions. Phase 3 creates a DungeonAssembler that stitches asset pack room prefabs into explorable dungeons with fog of war and battle triggers.

**Tech Stack:** Unity 6 (C#), URP, Unity Sentis 2.5, ScriptableObjects, UI Toolkit

**Spec:** `docs/superpowers/specs/2026-04-12-3d-engine-continuation-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Demo/Battle/ModelRegistry.cs` | Static enemy-name-to-GLB-path lookup with scale overrides |
| `Assets/Scripts/Demo/Encounters/EncounterTemplate.cs` | Predefined group compositions for mixed enemy encounters |
| `Assets/Scripts/Demo/Dungeon/RoomCatalog.cs` | ScriptableObject defining tagged room prefab entries |
| `Assets/Scripts/Demo/Dungeon/DungeonState.cs` | Serializable dungeon progress (visited rooms, triggered zones, player position) |
| `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` | Trigger collider that starts battle when player enters |
| `Assets/Scripts/Demo/Dungeon/DungeonAssembler.cs` | Runtime room prefab placement from PipelineCoordinator layout |
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | Dungeon exploration controller (movement, fog, scene transitions) |

### Modified Files
| File | Changes |
|------|---------|
| `Assets/Scripts/Demo/Encounters/EncounterData.cs` | Populate ModelId via ModelRegistry; add template selection in GenerateRandom() |
| `Assets/Scripts/Demo/Battle/BattleCombatant.cs` | Add ModelScale field; populate in FromEnemy()/FromCharacterSheet() |
| `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs` | Apply ModelScale after model instantiation |
| `Assets/Scripts/RPG/Character/CharacterSheet.cs` | Add ModelId field |
| `Assets/Scripts/Demo/PlayerData.cs` | Add ModelId field |
| `Assets/Scripts/ScriptableObjects/GameConfig.cs` | Add AI tuning + encounter config fields |
| `Assets/Scripts/Demo/Battle/CombatBrain.cs` | Read hyperparameters from GameConfig |
| `Assets/Scripts/Demo/Battle/BattleManager.cs` | Read rewards from GameConfig; apply EnemyDamageMult |
| `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs` | Route dungeons to DungeonExplorer instead of battle fallback |
| `Assets/Scripts/Demo/GameManager.cs` | Add DungeonState persistence + dungeon scene transitions |

---

## Phase 1: GLB Model Wiring

### Task 1: Create ModelRegistry

**Files:**
- Create: `Assets/Scripts/Demo/Battle/ModelRegistry.cs`

This is a pure static class with no Unity dependencies beyond `UnityEngine.Random`. It maps every enemy name used in `EncounterData` biome pools to one or more GLB resource paths under `Resources/Models/`.

- [ ] **Step 1: Create ModelRegistry.cs**

```csharp
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public static class ModelRegistry
    {
        public struct ModelEntry
        {
            public string[] Paths;
            public float Scale;

            public ModelEntry(float scale, params string[] paths)
            {
                Paths = paths;
                Scale = scale;
            }
        }

        private static readonly Dictionary<string, ModelEntry> _map = new()
        {
            // ── Forest biome ──
            ["Wolf"]       = new(0.8f, "Models/Monsters/Giant Rat"),
            ["Dire Wolf"]  = new(1.2f, "Models/Monsters/Giant Rat"),
            ["Alpha Wolf"] = new(1.4f, "Models/Monsters/Giant Rat"),

            // ── Road / Ruins biome ──
            ["Skeleton"]       = new(1.0f, "Models/Monsters/skeleton Fighter"),
            ["Skeleton Archer"]= new(1.0f, "Models/Monsters/skeleton archer"),
            ["Mutant"]         = new(1.0f, "Models/Monsters/Zombie male fighter",
                                           "Models/Monsters/Zombie female fighter"),
            ["Mutant Hulk"]    = new(1.3f, "Models/Monsters/zombie male warrior"),

            // ── Plains biome ──
            ["Bandit"]         = new(1.0f, "Models/Monsters/Human Female Bandit",
                                           "Models/Monsters/Human male bandit fighter",
                                           "Models/Monsters/Human Male Bandit fighter 4",
                                           "Models/Monsters/Human male bandit fighter 5"),
            ["Bandit Captain"] = new(1.1f, "Models/Monsters/Halfling male bandit"),
            ["Cultist"]        = new(1.0f, "Models/Monsters/human feamle assassin",
                                           "Models/Monsters/Human male Alchenist"),

            // ── Dungeon biome (Phase 2 encounter expansion) ──
            ["Goblin"]       = new(0.9f, "Models/Monsters/goblin female fighter",
                                         "Models/Monsters/Goblin male archer",
                                         "Models/Monsters/goblin male rogue"),
            ["Goblin King"]  = new(1.1f, "Models/Monsters/Goblin King"),
            ["Mummy"]        = new(1.0f, "Models/Monsters/Mummy"),
            ["Lizard Folk"]  = new(1.0f, "Models/Monsters/Lizard folk fighter",
                                         "Models/Monsters/Lizardfolk fighter"),
            ["Lizard Folk Archer"] = new(1.0f, "Models/Monsters/Lizard folk archer"),
            ["Orc"]          = new(1.1f, "Models/Monsters/Orc male fighterr",
                                         "Models/Monsters/Orc female fighter"),
            ["Kobold"]       = new(0.8f, "Models/Monsters/Kobold male fighter"),

            // ── Player race/class combos ──
            ["Dwarf_Fighter"]   = new(0.9f, "Models/NPCs/Dwarf male fighter",
                                             "Models/NPCs/Dwarf male fighter 2",
                                             "Models/NPCs/Dwarf male fighter 3"),
            ["Dwarf_Cleric"]    = new(0.9f, "Models/NPCs/Dwarf male cleric"),
            ["Elf_Ranger"]      = new(1.0f, "Models/NPCs/Elf female ranger"),
            ["Elf_Fighter"]     = new(1.0f, "Models/NPCs/Elf male fighter"),
            ["Elf_Wizard"]      = new(1.0f, "Models/NPCs/Elf male wizard",
                                             "Models/NPCs/Elf male wizard 2"),
            ["Human_Fighter"]   = new(1.0f, "Models/NPCs/Human male fighter",
                                             "Models/NPCs/Human male fighter 2",
                                             "Models/NPCs/Human male fighter 3",
                                             "Models/NPCs/Human female fighter"),
            ["Dragonborn_Fighter"] = new(1.1f, "Models/NPCs/Dragon born male fighter"),
            ["Dragonborn_Sorcerer"]= new(1.1f, "Models/NPCs/Dragon born male sorcerer"),
            ["Default_Player"]  = new(1.0f, "Models/NPCs/Human male fighter"),
        };

        /// <summary>
        /// Resolve an enemy name or player key to a model path and scale.
        /// Returns a random variant if multiple paths are registered.
        /// Returns null path if no mapping exists (caller should use capsule fallback).
        /// </summary>
        public static (string path, float scale) Resolve(string name)
        {
            if (string.IsNullOrEmpty(name) || !_map.TryGetValue(name, out var entry))
                return (null, 1f);

            var path = entry.Paths[UnityEngine.Random.Range(0, entry.Paths.Length)];
            return (path, entry.Scale);
        }

        /// <summary>
        /// Check if a mapping exists for the given name.
        /// </summary>
        public static bool HasMapping(string name)
            => !string.IsNullOrEmpty(name) && _map.ContainsKey(name);
    }
}
```

- [ ] **Step 2: Compile check**

Run the Roslyn compile check to verify no syntax errors:
```bash
cd "C:/Dev/Forever engine"
# Find the Assembly-CSharp rsp file and compile
RSP=$(find Library/Bee -name "Assembly-CSharp.rsp" 2>/dev/null | head -1)
if [ -n "$RSP" ]; then
  dotnet exec "$(find Library -name 'csc.dll' | head -1)" @"$RSP" 2>&1 | tail -20
fi
```

If the RSP approach isn't available, verify with a quick syntax parse or wait for the next full compile step.

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Battle/ModelRegistry.cs
git commit -m "feat: add ModelRegistry for enemy-name-to-GLB-path lookup

Static dictionary mapping all biome pool enemy names to Resources/Models/
GLB paths with scale overrides and random variant selection."
```

---

### Task 2: Wire ModelId and ModelScale into the encounter pipeline

**Files:**
- Modify: `Assets/Scripts/Demo/Encounters/EncounterData.cs` (lines 7-19, 213-226)
- Modify: `Assets/Scripts/Demo/Battle/BattleCombatant.cs` (lines 12-40, 90-106)

- [ ] **Step 1: Add ModelScale to EnemyDef**

In `Assets/Scripts/Demo/Encounters/EncounterData.cs`, the `EnemyDef` class (lines 7-19) already has a `ModelId` field. Add `ModelScale` after it:

```csharp
// After the existing ModelId field (around line 18):
public float ModelScale = 1f;
```

- [ ] **Step 2: Populate ModelId and ModelScale in MakeCREnemyDef()**

In `EncounterData.cs`, the `MakeCREnemyDef()` method (lines 213-226) creates an `EnemyDef` but never sets `ModelId`. At the end of the method, before the return statement, add the ModelRegistry lookup:

```csharp
// At the end of MakeCREnemyDef(), before the return:
var (modelPath, modelScale) = ForeverEngine.Demo.Battle.ModelRegistry.Resolve(def.Name);
if (modelPath != null)
{
    def.ModelId = modelPath;
    def.ModelScale = modelScale;
}
```

- [ ] **Step 3: Add ModelScale to BattleCombatant**

In `Assets/Scripts/Demo/Battle/BattleCombatant.cs`, add `ModelScale` next to the existing `ModelId` field (around line 39):

```csharp
// After the existing ModelId field:
public float ModelScale = 1f;
```

- [ ] **Step 4: Copy ModelScale in FromEnemy()**

In `BattleCombatant.cs`, the `FromEnemy()` method (lines 90-106) already copies `ModelId = def.ModelId`. Add `ModelScale` copy on the next line:

```csharp
// After ModelId = def.ModelId:
ModelScale = def.ModelScale,
```

Note: If `ModelId` assignment uses object initializer syntax (comma-separated), add `ModelScale` as another initializer field. If it uses separate assignment statements, add `c.ModelScale = def.ModelScale;`.

- [ ] **Step 5: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Encounters/EncounterData.cs Assets/Scripts/Demo/Battle/BattleCombatant.cs
git commit -m "feat: wire ModelId and ModelScale through encounter pipeline

MakeCREnemyDef() now calls ModelRegistry.Resolve() to populate ModelId
and ModelScale on every EnemyDef. BattleCombatant.FromEnemy() copies both."
```

---

### Task 3: Apply ModelScale in BattleRenderer3D

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs` (lines 83-115)

- [ ] **Step 1: Apply scale after model instantiation**

In `BattleRenderer3D.cs`, the `SpawnModel()` method (lines 83-115) loads models at line 86-91:

```csharp
var prefab = Resources.Load<GameObject>($"Models/{combatant.ModelId}");
if (prefab != null)
    model = Instantiate(prefab);
```

After the model is instantiated from prefab (around line 91), apply the scale:

```csharp
if (prefab != null)
{
    model = Instantiate(prefab);
    model.transform.localScale *= combatant.ModelScale;
}
```

The capsule fallback path (lines 93-110) should remain unchanged — it only runs when `prefab == null`.

- [ ] **Step 2: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Battle/BattleRenderer3D.cs
git commit -m "feat: apply ModelScale to instantiated GLB models in battle

Models loaded from Resources/Models/ now scale by BattleCombatant.ModelScale.
Capsule fallback unchanged."
```

---

### Task 4: Wire player model through CharacterSheet and PlayerData

**Files:**
- Modify: `Assets/Scripts/RPG/Character/CharacterSheet.cs` (add field around line 86)
- Modify: `Assets/Scripts/Demo/PlayerData.cs` (add field around line 40)
- Modify: `Assets/Scripts/Demo/Battle/BattleCombatant.cs` (lines 78-88, 111-136)

- [ ] **Step 1: Add ModelId to CharacterSheet**

In `Assets/Scripts/RPG/Character/CharacterSheet.cs`, add after the `Expertise` field (line 86):

```csharp
    [Header("Visual")]
    public string ModelId;
```

- [ ] **Step 2: Add ModelId to PlayerData**

In `Assets/Scripts/Demo/PlayerData.cs`, add after the `ArmorName` field (line 40):

```csharp
    public string ModelId = "Default_Player";
```

- [ ] **Step 3: Wire ModelId in BattleCombatant.FromPlayer()**

In `BattleCombatant.cs`, the `FromPlayer()` factory (lines 78-88) creates a combatant from `PlayerData`. In the object initializer, add:

```csharp
// Add alongside the other field assignments:
ModelId = player.ModelId,
```

Then after the initializer, resolve through ModelRegistry for the actual path:

```csharp
var (modelPath, modelScale) = ModelRegistry.Resolve(c.ModelId ?? "Default_Player");
if (modelPath != null)
{
    c.ModelId = modelPath;
    c.ModelScale = modelScale;
}
```

- [ ] **Step 4: Wire ModelId in BattleCombatant.FromCharacterSheet()**

In `BattleCombatant.cs`, the `FromCharacterSheet()` factory (lines 111-136) creates from a full `CharacterSheet`. In the object initializer, add:

```csharp
// Add alongside the other field assignments:
ModelId = sheet.ModelId,
```

Then after the initializer, resolve through ModelRegistry:

```csharp
var (modelPath, modelScale) = ModelRegistry.Resolve(c.ModelId ?? "Default_Player");
if (modelPath != null)
{
    c.ModelId = modelPath;
    c.ModelScale = modelScale;
}
```

- [ ] **Step 5: Populate ModelId during character creation**

In `PlayerData.FromCharacterData()` (line 55-80), after setting stats from `CharacterData`, build the ModelRegistry key from species + class:

```csharp
// After existing stat assignments:
string speciesKey = cd.Species?.Replace(" ", "") ?? "Human";
string classKey = cd.Classes?.Count > 0 ? cd.Classes[0].Name : "Fighter";
p.ModelId = $"{speciesKey}_{classKey}";
```

This produces keys like `"Human_Fighter"`, `"Elf_Wizard"`, `"Dwarf_Cleric"` that match ModelRegistry entries. If no match exists, `FromPlayer()` falls back to `"Default_Player"`.

- [ ] **Step 6: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/RPG/Character/CharacterSheet.cs \
        Assets/Scripts/Demo/PlayerData.cs \
        Assets/Scripts/Demo/Battle/BattleCombatant.cs
git commit -m "feat: wire player model through CharacterSheet and PlayerData

Player characters now get a ModelId based on species+class combo,
resolved through ModelRegistry to a GLB model path."
```

---

### Task 5: Phase 1 integration verify

- [ ] **Step 1: Full compile check**

```bash
cd "C:/Dev/Forever engine"
RSP=$(find Library/Bee -name "Assembly-CSharp.rsp" 2>/dev/null | head -1)
if [ -n "$RSP" ]; then
  dotnet exec "$(find Library -name 'csc.dll' | head -1)" @"$RSP" 2>&1 | tail -30
fi
```

- [ ] **Step 2: Verify model files exist for all registry entries**

Quick sanity check that the GLB files referenced in ModelRegistry actually exist:

```bash
cd "C:/Dev/Forever engine/Assets/Resources"
for model in \
  "Models/Monsters/Giant Rat.glb" \
  "Models/Monsters/skeleton Fighter.glb" \
  "Models/Monsters/skeleton archer.glb" \
  "Models/Monsters/Zombie male fighter.glb" \
  "Models/Monsters/zombie male warrior.glb" \
  "Models/Monsters/Human Female Bandit.glb" \
  "Models/Monsters/Human male bandit fighter.glb" \
  "Models/Monsters/Halfling male bandit.glb" \
  "Models/Monsters/Mummy.glb" \
  "Models/Monsters/Goblin King.glb" \
  "Models/Monsters/goblin female fighter.glb" \
  "Models/NPCs/Human male fighter.glb" \
  "Models/NPCs/Dwarf male fighter.glb" \
  "Models/NPCs/Elf male wizard.glb"; do
  [ -f "$model" ] && echo "OK: $model" || echo "MISSING: $model"
done
```

Fix any MISSING entries by adjusting the path in ModelRegistry to match actual filenames (case-sensitive on some platforms).

- [ ] **Step 3: Tag phase completion**

```bash
cd "C:/Dev/Forever engine"
git tag phase1-glb-wiring
```

---

## Phase 2: Battle AI Polish

### Task 6: Add AI tuning fields to GameConfig

**Files:**
- Modify: `Assets/Scripts/ScriptableObjects/GameConfig.cs` (after line 43)

- [ ] **Step 1: Add Q-learning and encounter config fields**

In `Assets/Scripts/ScriptableObjects/GameConfig.cs`, after the existing `AITurnDelay` field (line 43), add:

```csharp
    [Header("AI — Q-Learning")]
    public float QLearningRate = 0.15f;
    public float QDiscountFactor = 0.85f;
    public float QExplorationRate = 0.25f;

    [Header("AI — Rewards")]
    public float RewardAdvanceHit = 0.1f;
    public float RewardAttackAdjacent = 0.3f;
    public float RewardRetreatLowHP = 0.2f;
    public float RewardHoldGuard = 0.1f;
    public float PenaltyHoldChase = -0.05f;
    public float RewardKill = 0.5f;
    public float PenaltyDamageTaken = -0.1f;
    public float RewardHit = 0.5f;
    public float PenaltyMiss = -0.1f;

    [Header("Encounters")]
    public int DayXPBudgetPerLevel = 40;
    public int NightXPBudgetPerLevel = 60;
    public int MaxEnemiesPerEncounter = 4;
    [Range(0f, 1f)]
    public float EncounterTemplateChance = 0.6f;
```

- [ ] **Step 2: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/ScriptableObjects/GameConfig.cs
git commit -m "feat: add AI tuning and encounter config fields to GameConfig

Q-learning hyperparameters, reward values, and encounter budget
multipliers now exposed as ScriptableObject fields for inspector tuning."
```

---

### Task 7: Wire CombatBrain to read from GameConfig

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/CombatBrain.cs` (lines 18-21)

- [ ] **Step 1: Replace hardcoded hyperparameters**

In `Assets/Scripts/Demo/Battle/CombatBrain.cs`, the constructor (around lines 18-21) initializes the QLearner with hardcoded values. The class needs access to GameConfig. The GameConfig singleton is accessed via `Resources.Load<GameConfig>("GameConfig")` (check the existing pattern in other scripts).

Replace the hardcoded values in the constructor:

```csharp
// Replace lines 18-21 (the hardcoded values) with:
var config = Resources.Load<GameConfig>("GameConfig");
float lr = config != null ? config.QLearningRate : 0.15f;
float df = config != null ? config.QDiscountFactor : 0.85f;
float er = config != null ? config.QExplorationRate : 0.25f;
```

Then pass `lr`, `df`, `er` to the QLearner constructor instead of the hardcoded `0.15f`, `0.85f`, `0.25f`.

- [ ] **Step 2: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Battle/CombatBrain.cs
git commit -m "feat: wire CombatBrain hyperparameters to GameConfig

Q-learning rate, discount factor, and exploration rate now read from
GameConfig SO instead of hardcoded constants."
```

---

### Task 8: Wire BattleManager rewards from GameConfig and apply EnemyDamageMult

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs` (lines 692-761, 822-1021)

This task has two parts: externalize reward values and apply the unused EnemyDamageMult.

- [ ] **Step 1: Cache GameConfig reference in BattleManager**

At the top of `BattleManager.cs`, add a field and load it in `Start()`:

```csharp
// Add field near other private fields:
private GameConfig _gameConfig;

// In Start(), after existing initialization:
_gameConfig = Resources.Load<GameConfig>("GameConfig");
```

- [ ] **Step 2: Replace hardcoded rewards in ProcessAITurn()**

In `ProcessAITurn()` (lines 692-761), replace each hardcoded reward float with the GameConfig field. The pattern for each replacement:

```csharp
// Line ~729: Advance + hit reward
// Replace: brain.AddReward(0.1f);
// With:
brain.AddReward(_gameConfig != null ? _gameConfig.RewardAdvanceHit : 0.1f);

// Line ~735: Retreat when low HP
// Replace: brain.AddReward(0.2f);
// With:
brain.AddReward(_gameConfig != null ? _gameConfig.RewardRetreatLowHP : 0.2f);

// Line ~747: Direct attack adjacent
// Replace: brain.AddReward(0.3f);
// With:
brain.AddReward(_gameConfig != null ? _gameConfig.RewardAttackAdjacent : 0.3f);

// Line ~756: Hold guard / chase
// Replace: brain.AddReward(0.1f); and brain.AddReward(-0.05f);
// With:
brain.AddReward(_gameConfig != null ? _gameConfig.RewardHoldGuard : 0.1f);
brain.AddReward(_gameConfig != null ? _gameConfig.PenaltyHoldChase : -0.05f);
```

- [ ] **Step 3: Replace hardcoded rewards in ResolveAttack()**

In `ResolveAttack()` (around lines 1001-1020), replace hit/miss reward values:

```csharp
// Line ~1020: Hit reward
// Replace: brain.AddReward(0.5f);
// With:
brain.AddReward(_gameConfig != null ? _gameConfig.RewardHit : 0.5f);

// Line ~1020: Miss penalty
// Replace: brain.AddReward(-0.1f);
// With:
brain.AddReward(_gameConfig != null ? _gameConfig.PenaltyMiss : -0.1f);
```

- [ ] **Step 4: Add kill reward**

In the death handling section of `ResolveAttack()` (around lines 986-999), when a target reaches 0 HP and is not the player, add:

```csharp
// After confirming target death (e.g., after the death log line):
if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var killerBrain))
    killerBrain.AddReward(_gameConfig != null ? _gameConfig.RewardKill : 0.5f);
```

- [ ] **Step 5: Add damage-taken penalty**

In `ResolveAttack()`, after damage is applied to the target (around line 915-920), if the target is an AI combatant, penalize it:

```csharp
// After target.TakeDamage(hpDamage):
if (!target.IsPlayer && _brains.TryGetValue(target, out var targetBrain))
    targetBrain.AddReward(_gameConfig != null ? _gameConfig.PenaltyDamageTaken : -0.1f);
```

- [ ] **Step 6: Apply EnemyDamageMult**

In `ResolveAttack()`, after `DamageResolver.Apply()` calculates `hpDamage` (around line 911) but before applying it, scale by DynamicDifficulty when the attacker is an enemy:

```csharp
// After DamageResolver.Apply() returns hpDamage:
if (!attacker.IsPlayer)
{
    var dda = ForeverEngine.AI.Learning.DynamicDifficulty.Instance;
    if (dda != null)
        hpDamage = Mathf.RoundToInt(hpDamage * dda.EnemyDamageMult);
}
```

Check the exact namespace/access pattern for `DynamicDifficulty.Instance` — it should be a singleton. If it uses a different access pattern (e.g., `FindObjectOfType`), match the existing code style.

- [ ] **Step 7: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Battle/BattleManager.cs
git commit -m "feat: externalize AI rewards to GameConfig + apply EnemyDamageMult

All Q-learning reward values now read from GameConfig SO. Added explicit
kill reward and damage-taken penalty. DynamicDifficulty.EnemyDamageMult
now scales enemy attack damage, completing the adaptive difficulty loop."
```

---

### Task 9: Create EncounterTemplate system

**Files:**
- Create: `Assets/Scripts/Demo/Encounters/EncounterTemplate.cs`
- Modify: `Assets/Scripts/Demo/Encounters/EncounterData.cs` (lines 42-201)

- [ ] **Step 1: Create EncounterTemplate data structure**

```csharp
using System;

namespace ForeverEngine.Demo.Encounters
{
    [Serializable]
    public class EnemySlot
    {
        public string Name;
        public string Behavior;
        public int XPCost;

        public EnemySlot(string name, string behavior, int xpCost)
        {
            Name = name;
            Behavior = behavior;
            XPCost = xpCost;
        }
    }

    public class EncounterTemplate
    {
        public string Name;
        public int MinXP;
        public int MaxXP;
        public string[] Biomes;
        public EnemySlot[] Slots;

        public EncounterTemplate(string name, int minXP, int maxXP, string[] biomes, params EnemySlot[] slots)
        {
            Name = name;
            MinXP = minXP;
            MaxXP = maxXP;
            Biomes = biomes;
            Slots = slots;
        }

        public int TotalXP
        {
            get
            {
                int sum = 0;
                foreach (var s in Slots) sum += s.XPCost;
                return sum;
            }
        }

        /// <summary>
        /// All predefined encounter templates.
        /// </summary>
        public static readonly EncounterTemplate[] All = new[]
        {
            new EncounterTemplate("Goblin Raiding Party", 150, 300,
                new[] { "dungeon", "ruins" },
                new EnemySlot("Goblin King", "guard", 100),
                new EnemySlot("Goblin", "chase", 50),
                new EnemySlot("Goblin", "chase", 50)),

            new EncounterTemplate("Undead Patrol", 200, 400,
                new[] { "dungeon", "ruins", "crypt" },
                new EnemySlot("Mummy", "chase", 200),
                new EnemySlot("Skeleton", "guard", 50),
                new EnemySlot("Skeleton", "guard", 50)),

            new EncounterTemplate("Bandit Ambush", 125, 250,
                new[] { "road", "plains", "ruins" },
                new EnemySlot("Bandit Captain", "guard", 100),
                new EnemySlot("Bandit", "chase", 25),
                new EnemySlot("Bandit", "chase", 25),
                new EnemySlot("Bandit", "chase", 25)),

            new EncounterTemplate("Wolf Pack", 125, 250,
                new[] { "forest" },
                new EnemySlot("Alpha Wolf", "guard", 100),
                new EnemySlot("Wolf", "chase", 25),
                new EnemySlot("Wolf", "chase", 25),
                new EnemySlot("Wolf", "chase", 25)),

            new EncounterTemplate("Cultist Cell", 100, 200,
                new[] { "ruins", "dungeon", "crypt" },
                new EnemySlot("Cultist", "chase", 50),
                new EnemySlot("Cultist", "chase", 50),
                new EnemySlot("Skeleton", "guard", 50)),

            new EncounterTemplate("Lizardfolk Warband", 150, 350,
                new[] { "dungeon" },
                new EnemySlot("Lizard Folk Archer", "guard", 100),
                new EnemySlot("Lizard Folk", "chase", 50),
                new EnemySlot("Lizard Folk", "chase", 50)),

            new EncounterTemplate("Orc Raiders", 200, 400,
                new[] { "dungeon", "plains" },
                new EnemySlot("Orc", "guard", 100),
                new EnemySlot("Orc", "chase", 100),
                new EnemySlot("Kobold", "chase", 25),
                new EnemySlot("Kobold", "chase", 25)),
        };

        /// <summary>
        /// Find templates that fit the given XP budget and biome.
        /// </summary>
        public static System.Collections.Generic.List<EncounterTemplate> FindMatching(
            int xpBudget, string biome)
        {
            var results = new System.Collections.Generic.List<EncounterTemplate>();
            foreach (var t in All)
            {
                if (t.TotalXP > xpBudget) continue;
                bool biomeMatch = false;
                foreach (var b in t.Biomes)
                {
                    if (b == biome) { biomeMatch = true; break; }
                }
                if (biomeMatch) results.Add(t);
            }
            return results;
        }
    }
}
```

- [ ] **Step 2: Wire template selection into EncounterData.GenerateRandom()**

In `Assets/Scripts/Demo/Encounters/EncounterData.cs`, at the beginning of `GenerateRandom()` (around line 42-70), after the XP budget is calculated (line 65) and the RNG is seeded (line 69), add template selection logic before the existing biome switch:

```csharp
// After xpBudget calculation and RNG seeding, before the biome switch:
var config = Resources.Load<GameConfig>("GameConfig");
float templateChance = config != null ? config.EncounterTemplateChance : 0.6f;
int maxEnemies = config != null ? config.MaxEnemiesPerEncounter : 4;

// Try template-based generation first
string biomeHint = /* extract from the existing biome parsing logic above */;
var templates = EncounterTemplate.FindMatching(xpBudget, biomeHint);
if (templates.Count > 0 && rng.NextDouble() < templateChance)
{
    var template = templates[rng.Next(templates.Count)];
    var enemies = new List<EnemyDef>();
    foreach (var slot in template.Slots)
    {
        if (enemies.Count >= maxEnemies) break;
        var def = MakeCREnemyDef(slot.Name, slot.XPCost);
        def.Behavior = slot.Behavior;
        enemies.Add(def);
    }
    return new EncounterResult
    {
        Id = id,
        Enemies = enemies,
        GoldReward = xpBudget / 10,
        XPReward = xpBudget,
    };
}

// Existing biome switch follows as fallback...
```

Note: The exact integration depends on the return type and structure. Match the existing `EncounterResult` or equivalent return pattern. The `MakeCREnemyDef` call may need the name passed differently — check if it takes a name parameter or builds the name from CR. If `MakeCREnemyDef` only takes XP, add a name override:

```csharp
// If MakeCREnemyDef doesn't accept a name, set it after:
var def = MakeCREnemyDef(slot.XPCost);
def.Name = slot.Name;
def.Behavior = slot.Behavior;
```

- [ ] **Step 3: Wire XP budget to GameConfig**

In `GenerateRandom()`, replace the hardcoded budget multipliers (line 65):

```csharp
// Replace: int xpBudget = (int)((night ? 60 : 40) * playerLevel * pacingMult);
// With:
int dayMult = config != null ? config.DayXPBudgetPerLevel : 40;
int nightMult = config != null ? config.NightXPBudgetPerLevel : 60;
int xpBudget = (int)((night ? nightMult : dayMult) * playerLevel * pacingMult);
```

Note: `config` is loaded at the top of the method (from Step 2). If it's loaded later in the existing code, move the load to before this line.

- [ ] **Step 4: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Encounters/EncounterTemplate.cs \
        Assets/Scripts/Demo/Encounters/EncounterData.cs
git commit -m "feat: add encounter templates for mixed enemy compositions

Seven predefined encounter templates with role-based compositions
(guard + chase combos). GenerateRandom() tries template selection at
60% probability before falling back to random biome pools. XP budget
multipliers now read from GameConfig."
```

---

### Task 10: Expand biome pools with new monsters

**Files:**
- Modify: `Assets/Scripts/Demo/Encounters/EncounterData.cs` (lines 71-198)

- [ ] **Step 1: Add dungeon biome pool**

In `EncounterData.GenerateRandom()`, after the existing biome switch cases (plains ends around line 198), add a dungeon/crypt case:

```csharp
case "dungeon":
case "crypt":
{
    float roll = (float)rng.NextDouble();
    if (roll < 0.3f)
    {
        // Skeleton garrison
        int count = Math.Min(xpBudget / 50, maxEnemies);
        for (int i = 0; i < count; i++)
        {
            var def = MakeCREnemyDef(50);
            def.Name = rng.NextDouble() < 0.5 ? "Skeleton" : "Skeleton Archer";
            def.Behavior = "guard";
            enemies.Add(def);
        }
    }
    else if (roll < 0.6f)
    {
        // Mummy + minions
        if (xpBudget >= 200)
        {
            var boss = MakeCREnemyDef(200);
            boss.Name = "Mummy";
            boss.Behavior = "chase";
            enemies.Add(boss);
            xpBudget -= 200;
        }
        int minions = Math.Min(xpBudget / 25, maxEnemies - enemies.Count);
        for (int i = 0; i < minions; i++)
        {
            var def = MakeCREnemyDef(25);
            def.Name = "Skeleton";
            def.Behavior = "guard";
            enemies.Add(def);
        }
    }
    else
    {
        // Lizardfolk patrol
        int count = Math.Min(xpBudget / 50, maxEnemies);
        for (int i = 0; i < count; i++)
        {
            var def = MakeCREnemyDef(50);
            def.Name = rng.NextDouble() < 0.4 ? "Lizard Folk Archer" : "Lizard Folk";
            def.Behavior = i == 0 ? "guard" : "chase";
            enemies.Add(def);
        }
    }
    break;
}
```

- [ ] **Step 2: Add orc encounters to forest pool**

In the forest biome case (lines 71-105), add an orc encounter variant. After the existing Alpha Wolf branch (around line 85), add:

```csharp
// Add a new branch before the default wolf pack, e.g. at 10% chance:
// Adjust existing percentages: Alpha Wolf 15% → 12%, Orc 8%, Dire Wolf 25% → 22%
else if (roll < 0.20f && playerLevel >= 2)
{
    // Orc scout
    var orc = MakeCREnemyDef(100);
    orc.Name = "Orc";
    orc.Behavior = "chase";
    enemies.Add(orc);
    int packCount = Math.Min((xpBudget - 100) / 25, maxEnemies - 1);
    for (int i = 0; i < packCount; i++)
    {
        var wolf = MakeCREnemyDef(25);
        wolf.Name = "Wolf";
        wolf.Behavior = "chase";
        enemies.Add(wolf);
    }
}
```

Adjust the existing probability thresholds to accommodate the new branch. The exact roll values depend on the existing code structure — read the current thresholds and redistribute.

- [ ] **Step 3: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Encounters/EncounterData.cs
git commit -m "feat: expand biome pools with dungeon/crypt encounters and orcs

Added dungeon biome with skeleton garrisons, mummy+minion combos, and
lizardfolk patrols. Added orc scout variant to forest encounters."
```

---

### Task 11: Phase 2 integration verify

- [ ] **Step 1: Full compile check**

```bash
cd "C:/Dev/Forever engine"
RSP=$(find Library/Bee -name "Assembly-CSharp.rsp" 2>/dev/null | head -1)
if [ -n "$RSP" ]; then
  dotnet exec "$(find Library -name 'csc.dll' | head -1)" @"$RSP" 2>&1 | tail -30
fi
```

- [ ] **Step 2: Verify all template enemy names have ModelRegistry entries**

```bash
cd "C:/Dev/Forever engine"
# Check that every enemy name in EncounterTemplate.All has a ModelRegistry mapping
grep -oP 'new EnemySlot\("([^"]+)"' Assets/Scripts/Demo/Encounters/EncounterTemplate.cs | \
  sed 's/new EnemySlot("//;s/"//' | sort -u | while read name; do
  grep -q "\"$name\"" Assets/Scripts/Demo/Battle/ModelRegistry.cs && \
    echo "OK: $name" || echo "MISSING: $name"
done
```

Fix any MISSING entries by adding them to ModelRegistry.

- [ ] **Step 3: Tag phase completion**

```bash
cd "C:/Dev/Forever engine"
git tag phase2-ai-polish
```

---

## Phase 3: 3D Dungeon Interiors

### Task 12: Create RoomCatalog ScriptableObject

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/RoomCatalog.cs`

- [ ] **Step 1: Create the Dungeon directory**

```bash
mkdir -p "C:/Dev/Forever engine/Assets/Scripts/Demo/Dungeon"
```

- [ ] **Step 2: Create RoomCatalog.cs**

```csharp
using System;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    public enum RoomTag { Entrance, Corridor, Chamber, DeadEnd, Boss, Treasure }
    public enum DoorSide { North, South, East, West }

    [Serializable]
    public class DoorPosition
    {
        public DoorSide Side;
        public int Offset;
    }

    [Serializable]
    public class RoomEntry
    {
        public string Id;
        public RoomTag Tag;
        public GameObject Prefab;
        public Vector2Int Dimensions = new(4, 4);
        public DoorPosition[] Doors;
        [Tooltip("torch, dark, boss_glow")]
        public string LightingPreset = "torch";
        [Tooltip("Source asset pack for reference")]
        public string Pack;
    }

    [CreateAssetMenu(menuName = "Forever Engine/Room Catalog")]
    public class RoomCatalog : ScriptableObject
    {
        public RoomEntry[] Rooms;

        /// <summary>
        /// Get all rooms matching the given tag.
        /// </summary>
        public RoomEntry[] GetByTag(RoomTag tag)
        {
            var results = new System.Collections.Generic.List<RoomEntry>();
            if (Rooms == null) return results.ToArray();
            foreach (var r in Rooms)
            {
                if (r.Tag == tag) results.Add(r);
            }
            return results.ToArray();
        }

        /// <summary>
        /// Pick a random room matching the tag. Returns null if none available.
        /// </summary>
        public RoomEntry PickRandom(RoomTag tag)
        {
            var matching = GetByTag(tag);
            if (matching.Length == 0) return null;
            return matching[UnityEngine.Random.Range(0, matching.Length)];
        }
    }
}
```

- [ ] **Step 3: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Dungeon/RoomCatalog.cs
git commit -m "feat: add RoomCatalog SO for tagged dungeon room prefabs

RoomEntry defines prefab, dimensions, door positions, and lighting
preset. RoomCatalog provides tag-based lookup and random selection."
```

---

### Task 13: Create DungeonState for persistence across scene loads

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonState.cs`

- [ ] **Step 1: Create DungeonState.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Serializable dungeon progress that persists across battle scene transitions.
    /// Stored on GameManager as a static field.
    /// </summary>
    [Serializable]
    public class DungeonState
    {
        public string LocationId;
        public Vector3 PlayerPosition;
        public float PlayerRotationY;
        public float CameraOrbitAngle;
        public float CameraDistance;
        public HashSet<int> VisitedRooms = new();
        public HashSet<int> TriggeredEncounters = new();
        public int RoomCount;
        public int BossRoomIndex = -1;
        public bool BossDefeated;

        /// <summary>
        /// Mark a room as visited (activates its lighting).
        /// </summary>
        public void VisitRoom(int roomIndex)
        {
            VisitedRooms.Add(roomIndex);
        }

        /// <summary>
        /// Mark an encounter zone as triggered (prevents re-trigger).
        /// </summary>
        public void TriggerEncounter(int encounterIndex)
        {
            TriggeredEncounters.Add(encounterIndex);
        }

        /// <summary>
        /// Check if the dungeon is fully cleared (boss defeated).
        /// </summary>
        public bool IsCleared => BossDefeated;
    }
}
```

- [ ] **Step 2: Add DungeonState field to GameManager**

In `Assets/Scripts/Demo/GameManager.cs`, add a static field near the other state fields (around line 18-20):

```csharp
// Add near PendingEncounterId/PendingLocationId:
public DungeonState PendingDungeonState;
```

Add the using directive at the top of GameManager.cs:
```csharp
using ForeverEngine.Demo.Dungeon;
```

- [ ] **Step 3: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Dungeon/DungeonState.cs \
        Assets/Scripts/Demo/GameManager.cs
git commit -m "feat: add DungeonState for persistence across battle transitions

Tracks visited rooms, triggered encounters, player position, and boss
status. Stored on GameManager for scene load survival."
```

---

### Task 14: Create EncounterZone trigger

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/EncounterZone.cs`

- [ ] **Step 1: Create EncounterZone.cs**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    [RequireComponent(typeof(BoxCollider))]
    public class EncounterZone : MonoBehaviour
    {
        [Tooltip("Links to EncounterData.Get() or GenerateRandom()")]
        public string EncounterId;

        [Tooltip("Index in DungeonState.TriggeredEncounters")]
        public int ZoneIndex;

        [Tooltip("If true, defeating this encounter completes the dungeon")]
        public bool IsBoss;

        private bool _triggered;

        private void Awake()
        {
            // Ensure collider is a trigger
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;

            // Check if already triggered from restored DungeonState
            var gm = GameManager.Instance;
            if (gm != null && gm.PendingDungeonState != null)
            {
                _triggered = gm.PendingDungeonState.TriggeredEncounters.Contains(ZoneIndex);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            _triggered = true;

            var explorer = FindFirstObjectByType<DungeonExplorer>();
            if (explorer != null)
            {
                explorer.EnterBattle(EncounterId, ZoneIndex, IsBoss);
            }
        }
    }
}
```

- [ ] **Step 2: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Dungeon/EncounterZone.cs
git commit -m "feat: add EncounterZone trigger for dungeon battles

BoxCollider trigger that fires once per dungeon run. Checks
DungeonState to skip already-triggered encounters on scene restore."
```

---

### Task 15: Create DungeonAssembler

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonAssembler.cs`

This reads the room graph from PipelineCoordinator and instantiates prefabs from RoomCatalog.

- [ ] **Step 1: Create DungeonAssembler.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    public class DungeonAssembler
    {
        private readonly RoomCatalog _catalog;
        private readonly float _roomSpacing;

        public struct PlacedRoom
        {
            public int Index;
            public RoomEntry Entry;
            public GameObject Instance;
            public Vector3 WorldPosition;
            public RoomTag Tag;
        }

        public DungeonAssembler(RoomCatalog catalog, float roomSpacing = 12f)
        {
            _catalog = catalog;
            _roomSpacing = roomSpacing;
        }

        /// <summary>
        /// Build a dungeon from a simple room layout.
        /// Returns placed rooms and a parent GameObject containing everything.
        /// </summary>
        /// <param name="roomCount">Number of rooms to generate</param>
        /// <param name="parent">Parent transform for all room instances</param>
        /// <returns>List of placed rooms</returns>
        public List<PlacedRoom> Build(int roomCount, Transform parent)
        {
            var placed = new List<PlacedRoom>();

            // Generate a simple linear-with-branches layout
            // Room 0 = entrance, last room = boss, middle rooms = mix of chambers/corridors
            var positions = GenerateLayout(roomCount);

            for (int i = 0; i < roomCount; i++)
            {
                RoomTag tag;
                if (i == 0) tag = RoomTag.Entrance;
                else if (i == roomCount - 1) tag = RoomTag.Boss;
                else if (i % 3 == 0) tag = RoomTag.Corridor;
                else tag = RoomTag.Chamber;

                var entry = _catalog.PickRandom(tag);
                GameObject instance = null;
                Vector3 worldPos = positions[i] * _roomSpacing;

                if (entry != null && entry.Prefab != null)
                {
                    instance = Object.Instantiate(entry.Prefab, worldPos, Quaternion.identity, parent);
                }
                else
                {
                    // Fallback: create a simple floor plane as placeholder
                    instance = CreateFallbackRoom(tag, worldPos, parent);
                    entry ??= new RoomEntry { Id = $"fallback_{i}", Tag = tag, Dimensions = new Vector2Int(4, 4) };
                }

                instance.name = $"Room_{i}_{tag}";

                // Apply lighting preset
                ApplyLighting(instance, entry?.LightingPreset ?? "dark", i == 0);

                // Add encounter zone for chamber and boss rooms
                if (tag == RoomTag.Chamber || tag == RoomTag.Boss)
                {
                    var zone = instance.AddComponent<EncounterZone>();
                    zone.ZoneIndex = i;
                    zone.IsBoss = (tag == RoomTag.Boss);
                    // Generate encounter ID based on dungeon context
                    zone.EncounterId = tag == RoomTag.Boss
                        ? "boss_dungeon"
                        : $"random_dungeon_room{i}";
                }

                // Add room trigger for fog of war
                var triggerObj = new GameObject($"RoomTrigger_{i}");
                triggerObj.transform.SetParent(instance.transform);
                triggerObj.transform.localPosition = Vector3.zero;
                var triggerCol = triggerObj.AddComponent<BoxCollider>();
                triggerCol.isTrigger = true;
                triggerCol.size = new Vector3(
                    entry.Dimensions.x * 2f,
                    4f,
                    entry.Dimensions.y * 2f);
                triggerObj.layer = LayerMask.NameToLayer("Default");

                placed.Add(new PlacedRoom
                {
                    Index = i,
                    Entry = entry,
                    Instance = instance,
                    WorldPosition = worldPos,
                    Tag = tag,
                });
            }

            return placed;
        }

        /// <summary>
        /// Generate grid positions for rooms in a dungeon-like layout.
        /// Uses a simple path with occasional branches.
        /// </summary>
        private List<Vector3> GenerateLayout(int roomCount)
        {
            var positions = new List<Vector3>();
            var occupied = new HashSet<(int, int)>();

            int x = 0, z = 0;
            var dirs = new (int dx, int dz)[] { (1, 0), (0, 1), (-1, 0), (0, -1) };
            int dirIndex = 0;

            for (int i = 0; i < roomCount; i++)
            {
                positions.Add(new Vector3(x, 0, z));
                occupied.Add((x, z));

                // Pick next position: try forward, then turn
                bool placed = false;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    int tryDir = (dirIndex + attempt) % 4;
                    int nx = x + dirs[tryDir].dx;
                    int nz = z + dirs[tryDir].dz;
                    if (!occupied.Contains((nx, nz)))
                    {
                        x = nx;
                        z = nz;
                        dirIndex = tryDir;
                        // Occasionally turn for variety
                        if (Random.value < 0.3f)
                            dirIndex = (dirIndex + (Random.value < 0.5f ? 1 : 3)) % 4;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    // Dead end — backtrack by trying all directions
                    x += 1;
                    z += 1;
                }
            }

            return positions;
        }

        private GameObject CreateFallbackRoom(RoomTag tag, Vector3 position, Transform parent)
        {
            var room = new GameObject();
            room.transform.position = position;
            room.transform.SetParent(parent);

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(room.transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            float size = tag == RoomTag.Boss ? 10f : tag == RoomTag.Corridor ? 4f : 6f;
            floor.transform.localScale = new Vector3(size, 0.1f, size);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = tag switch
            {
                RoomTag.Entrance => new Color(0.3f, 0.3f, 0.25f),
                RoomTag.Boss => new Color(0.4f, 0.2f, 0.2f),
                RoomTag.Corridor => new Color(0.25f, 0.25f, 0.25f),
                _ => new Color(0.3f, 0.28f, 0.25f),
            };
            floor.GetComponent<Renderer>().material = mat;

            // Walls (4 sides)
            float wallHeight = 3f;
            var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wallMat.color = new Color(0.2f, 0.2f, 0.22f);

            for (int i = 0; i < 4; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(room.transform);
                wall.GetComponent<Renderer>().material = wallMat;

                float half = size / 2f;
                switch (i)
                {
                    case 0: // North
                        wall.transform.localPosition = new Vector3(0, wallHeight / 2f, half);
                        wall.transform.localScale = new Vector3(size, wallHeight, 0.2f);
                        break;
                    case 1: // South
                        wall.transform.localPosition = new Vector3(0, wallHeight / 2f, -half);
                        wall.transform.localScale = new Vector3(size, wallHeight, 0.2f);
                        break;
                    case 2: // East
                        wall.transform.localPosition = new Vector3(half, wallHeight / 2f, 0);
                        wall.transform.localScale = new Vector3(0.2f, wallHeight, size);
                        break;
                    case 3: // West
                        wall.transform.localPosition = new Vector3(-half, wallHeight / 2f, 0);
                        wall.transform.localScale = new Vector3(0.2f, wallHeight, size);
                        break;
                }
            }

            return room;
        }

        /// <summary>
        /// Apply lighting based on preset. Entrance starts lit; others start dark.
        /// </summary>
        private void ApplyLighting(GameObject room, string preset, bool startLit)
        {
            var lightObj = new GameObject("RoomLight");
            lightObj.transform.SetParent(room.transform);
            lightObj.transform.localPosition = new Vector3(0, 3f, 0);

            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 15f;

            switch (preset)
            {
                case "torch":
                    light.color = new Color(1f, 0.85f, 0.6f);
                    light.intensity = 1.5f;
                    break;
                case "boss_glow":
                    light.color = new Color(0.8f, 0.3f, 0.3f);
                    light.intensity = 2.0f;
                    break;
                case "dark":
                default:
                    light.color = new Color(0.4f, 0.45f, 0.5f);
                    light.intensity = 0.8f;
                    break;
            }

            // Fog of war: only entrance starts lit
            light.enabled = startLit;
        }
    }
}
```

- [ ] **Step 2: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Dungeon/DungeonAssembler.cs
git commit -m "feat: add DungeonAssembler for runtime room prefab placement

Generates a grid layout, instantiates RoomCatalog prefabs with fallback
primitive rooms, applies lighting presets, and places EncounterZones in
chamber/boss rooms."
```

---

### Task 16: Create DungeonExplorer controller

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`

This is the main dungeon exploration MonoBehaviour — handles player movement, fog of war, and battle transitions.

- [ ] **Step 1: Create DungeonExplorer.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.MonoBehaviour.Camera;

namespace ForeverEngine.Demo.Dungeon
{
    public class DungeonExplorer : UnityEngine.MonoBehaviour
    {
        public static DungeonExplorer Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private int _roomCount = 8;

        private GameObject _playerModel;
        private PerspectiveCameraController _camera;
        private DungeonAssembler _assembler;
        private List<DungeonAssembler.PlacedRoom> _rooms;
        private DungeonState _state;
        private string _locationId;
        private int _currentRoomIndex = -1;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Initialize the dungeon exploration for a given location.
        /// </summary>
        public void Initialize(string locationId, RoomCatalog catalog)
        {
            _locationId = locationId;
            _assembler = new DungeonAssembler(catalog);

            // Restore or create state
            var gm = GameManager.Instance;
            _state = gm.PendingDungeonState;
            if (_state == null || _state.LocationId != locationId)
            {
                _state = new DungeonState
                {
                    LocationId = locationId,
                    RoomCount = _roomCount,
                };
            }

            // Build rooms
            var dungeonRoot = new GameObject("DungeonRoot");
            _rooms = _assembler.Build(_state.RoomCount, dungeonRoot.transform);

            // Track boss room
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].Tag == RoomTag.Boss)
                {
                    _state.BossRoomIndex = i;
                    break;
                }
            }

            // Spawn player
            SpawnPlayer();

            // Setup camera
            SetupCamera();

            // Restore fog of war state
            RestoreFogState();

            // Tag player for EncounterZone triggers
            _playerModel.tag = "Player";
        }

        private void SpawnPlayer()
        {
            var gm = GameManager.Instance;

            if (_state.PlayerPosition != Vector3.zero)
            {
                // Restore position from state
                _playerModel = CreatePlayerModel(_state.PlayerPosition);
                _playerModel.transform.eulerAngles = new Vector3(0, _state.PlayerRotationY, 0);
            }
            else
            {
                // Start at entrance room
                var entrance = _rooms[0];
                var spawnPos = entrance.WorldPosition + Vector3.up * 0.5f;
                _playerModel = CreatePlayerModel(spawnPos);
                _state.VisitRoom(0);
            }
        }

        private GameObject CreatePlayerModel(Vector3 position)
        {
            // Try to load the player's 3D model
            string modelId = null;
            var gm = GameManager.Instance;
            if (gm?.Player != null)
                modelId = gm.Player.ModelId;

            GameObject model;
            var (path, scale) = Battle.ModelRegistry.Resolve(modelId ?? "Default_Player");
            if (path != null)
            {
                var prefab = Resources.Load<GameObject>(path);
                if (prefab != null)
                {
                    model = Instantiate(prefab, position, Quaternion.identity);
                    model.transform.localScale *= scale;
                }
                else
                {
                    model = CreateCapsulePlayer(position);
                }
            }
            else
            {
                model = CreateCapsulePlayer(position);
            }

            model.name = "DungeonPlayer";

            // Add collider + rigidbody for trigger detection
            if (model.GetComponent<Collider>() == null)
            {
                var col = model.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.3f;
                col.center = new Vector3(0, 0.9f, 0);
            }

            var rb = model.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            return model;
        }

        private GameObject CreateCapsulePlayer(Vector3 position)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.position = position;
            capsule.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            var renderer = capsule.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.6f, 1f);
            renderer.material = mat;
            return capsule;
        }

        private void SetupCamera()
        {
            _camera = FindFirstObjectByType<PerspectiveCameraController>();
            if (_camera == null)
            {
                var camObj = new GameObject("DungeonCamera");
                _camera = camObj.AddComponent<PerspectiveCameraController>();
            }

            _camera.FollowTarget = _playerModel.transform;

            // Tighter camera for dungeon interiors
            _camera.SetDistance(8f);
            _camera.SetOrbitAngle(_state.CameraOrbitAngle != 0 ? _state.CameraOrbitAngle : 0f);
            _camera.SnapToTarget();
        }

        private void RestoreFogState()
        {
            // Relight visited rooms
            foreach (int roomIdx in _state.VisitedRooms)
            {
                if (roomIdx >= 0 && roomIdx < _rooms.Count)
                {
                    SetRoomLit(_rooms[roomIdx].Instance, true, false);
                }
            }

            // Entrance is always fully lit
            if (_rooms.Count > 0)
                SetRoomLit(_rooms[0].Instance, true, true);
        }

        private void Update()
        {
            HandleMovement();
            CheckRoomProximity();
        }

        private void HandleMovement()
        {
            if (_playerModel == null) return;

            float inputX = 0f, inputZ = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    inputZ += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  inputZ -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  inputX -= 1f;

            if (inputX == 0f && inputZ == 0f) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 camFwd = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camFwd.y = 0f; camFwd.Normalize();
            camRight.y = 0f; camRight.Normalize();

            Vector3 moveDir = (camFwd * inputZ + camRight * inputX).normalized;
            _playerModel.transform.position += moveDir * _moveSpeed * Time.deltaTime;
            _playerModel.transform.rotation = Quaternion.LookRotation(moveDir);
        }

        /// <summary>
        /// Check which room the player is closest to and update fog of war.
        /// </summary>
        private void CheckRoomProximity()
        {
            if (_playerModel == null || _rooms == null) return;

            float closestDist = float.MaxValue;
            int closestRoom = -1;
            var playerPos = _playerModel.transform.position;

            for (int i = 0; i < _rooms.Count; i++)
            {
                float dist = Vector3.Distance(playerPos, _rooms[i].WorldPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestRoom = i;
                }
            }

            if (closestRoom >= 0 && closestRoom != _currentRoomIndex && closestDist < 8f)
            {
                _currentRoomIndex = closestRoom;
                _state.VisitRoom(closestRoom);
                SetRoomLit(_rooms[closestRoom].Instance, true, true);
            }
        }

        /// <summary>
        /// Set a room's lighting state for fog of war.
        /// </summary>
        private void SetRoomLit(GameObject room, bool lit, bool fullBrightness)
        {
            var lights = room.GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                light.enabled = lit;
                if (lit && !fullBrightness)
                    light.intensity *= 0.5f;
            }
        }

        /// <summary>
        /// Called by EncounterZone when player triggers a battle.
        /// </summary>
        public void EnterBattle(string encounterId, int zoneIndex, bool isBoss)
        {
            // Save state for return
            _state.TriggerEncounter(zoneIndex);
            _state.PlayerPosition = _playerModel.transform.position;
            _state.PlayerRotationY = _playerModel.transform.eulerAngles.y;
            _state.CameraOrbitAngle = _camera.OrbitAngle;
            _state.CameraDistance = _camera.Distance;

            var gm = GameManager.Instance;
            gm.PendingDungeonState = _state;

            if (isBoss)
            {
                // Mark for post-battle check
                gm.PendingDungeonState.BossRoomIndex = zoneIndex;
            }

            gm.EnterBattle(encounterId);
        }

        /// <summary>
        /// Called when returning from a won battle. Checks if dungeon is complete.
        /// </summary>
        public void OnBattleWon(string encounterId)
        {
            // Check if boss was just defeated
            var gm = GameManager.Instance;
            if (_state.BossRoomIndex >= 0 &&
                _state.TriggeredEncounters.Contains(_state.BossRoomIndex))
            {
                _state.BossDefeated = true;
                CompleteDungeon();
            }
        }

        private void CompleteDungeon()
        {
            var gm = GameManager.Instance;
            gm.PendingDungeonState = null;

            // Could show completion UI here
            Debug.Log($"Dungeon '{_locationId}' cleared!");

            gm.ReturnToOverworld();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
```

- [ ] **Step 2: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs
git commit -m "feat: add DungeonExplorer for dungeon movement and fog of war

WASD movement reusing OverworldManager camera-relative pattern.
Room-proximity fog of war via Light toggling. State persistence for
battle scene transitions. Boss defeat triggers dungeon completion."
```

---

### Task 17: Wire LocationInteriorManager and GameManager for dungeons

**Files:**
- Modify: `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs` (lines 42-63)
- Modify: `Assets/Scripts/Demo/GameManager.cs` (lines 164-175)

- [ ] **Step 1: Route dungeons to DungeonExplorer in LocationInteriorManager**

In `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs`, replace the battle fallback in `EnterLocation()` (lines 60-62). The current code is:

```csharp
// Current (lines 60-62):
// Dungeon interior renderer not yet functional — fall back to battle encounter
GameManager.Instance.EnterBattle($"dungeon_{loc.Type}_{loc.Id}");
```

Replace with:

```csharp
// Route to dungeon exploration scene
GameManager.Instance.EnterDungeon(loc.Id);
```

- [ ] **Step 2: Add EnterDungeon() to GameManager**

In `Assets/Scripts/Demo/GameManager.cs`, add a new method after `EnterBattle()` (line 168):

```csharp
/// <summary>
/// Transition to dungeon exploration for the given location.
/// </summary>
public void EnterDungeon(string locationId)
{
    PendingLocationId = locationId;
    // Create fresh dungeon state if none exists for this location
    if (PendingDungeonState == null || PendingDungeonState.LocationId != locationId)
    {
        PendingDungeonState = new ForeverEngine.Demo.Dungeon.DungeonState
        {
            LocationId = locationId,
        };
    }
    UnityEngine.SceneManagement.SceneManager.LoadScene("DungeonExploration");
}
```

- [ ] **Step 3: Add dungeon return logic to ReturnToOverworld()**

In `GameManager.cs`, modify `ReturnToOverworld()` (lines 170-175) to clear dungeon state:

```csharp
// Add at the start of ReturnToOverworld():
PendingDungeonState = null;
```

- [ ] **Step 4: Add return-to-dungeon path after battle**

In `GameManager.cs`, the post-battle flow needs to check if the battle was dungeon-sourced. After the existing battle completion handling (check where `LastBattleWon` is set and `ReturnToOverworld()` is called), add a dungeon return path:

```csharp
// In the post-battle handler (likely in a method called after BattleManager signals completion):
if (LastBattleWon && PendingDungeonState != null)
{
    // Return to dungeon, not overworld
    UnityEngine.SceneManagement.SceneManager.LoadScene("DungeonExploration");
    return;
}
// Existing ReturnToOverworld() call follows
```

Find the exact location where battle completion is handled — it may be in `ReturnToOverworld()` itself, in a scene-loaded callback, or in BattleManager's victory handler. The key is: if `PendingDungeonState` is non-null after a won battle, load `DungeonExploration` instead of `Overworld3D`.

- [ ] **Step 5: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Locations/LocationInteriorManager.cs \
        Assets/Scripts/Demo/GameManager.cs
git commit -m "feat: wire dungeon exploration into scene flow

LocationInteriorManager routes dungeons to DungeonExplorer instead of
battle fallback. GameManager.EnterDungeon() manages state + scene load.
Post-battle returns to dungeon if PendingDungeonState exists."
```

---

### Task 18: Create DungeonExploration scene bootstrap

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs`

This MonoBehaviour lives in the DungeonExploration scene and bootstraps everything on scene load.

- [ ] **Step 1: Create DungeonSceneSetup.cs**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Bootstrap for the DungeonExploration scene.
    /// Attach to an empty GameObject in the scene.
    /// </summary>
    public class DungeonSceneSetup : UnityEngine.MonoBehaviour
    {
        [SerializeField] private RoomCatalog _roomCatalog;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("DungeonSceneSetup: GameManager not found");
                return;
            }

            string locationId = gm.PendingLocationId;
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogError("DungeonSceneSetup: No PendingLocationId");
                gm.ReturnToOverworld();
                return;
            }

            // Load catalog from Resources if not assigned in inspector
            if (_roomCatalog == null)
                _roomCatalog = Resources.Load<RoomCatalog>("RoomCatalog");

            if (_roomCatalog == null)
            {
                Debug.LogError("DungeonSceneSetup: No RoomCatalog found");
                gm.ReturnToOverworld();
                return;
            }

            // Create explorer and initialize
            var explorerObj = new GameObject("DungeonExplorer");
            var explorer = explorerObj.AddComponent<DungeonExplorer>();
            explorer.Initialize(locationId, _roomCatalog);

            // Check if returning from a won battle
            if (gm.LastBattleWon && gm.PendingDungeonState != null)
            {
                explorer.OnBattleWon(gm.PendingEncounterId);
            }

            // Clear battle result
            gm.LastBattleWon = false;
            gm.PendingEncounterId = null;
        }
    }
}
```

- [ ] **Step 2: Create the DungeonExploration scene**

The scene needs to be created via Unity Editor or a script. Since the user works via scripts/batchmode, create it programmatically:

```bash
# The scene will be created by a simple editor script or manually.
# At minimum, create a placeholder scene file that the build system recognizes.
# The DungeonSceneSetup component will be added to the scene programmatically.
```

Create an editor utility to build the scene:

```csharp
// This can be run as a one-time setup or the scene can be created manually.
// The key objects needed in the DungeonExploration scene:
// 1. Main Camera (with PerspectiveCameraController)
// 2. Directional Light
// 3. Empty GameObject with DungeonSceneSetup component
// 4. EventSystem (for UI)
```

For now, the simplest approach: duplicate an existing scene (like BattleMap) and strip it down, or create a minimal scene. The implementing agent should:

1. Check if `Assets/Scenes/DungeonExploration.unity` exists
2. If not, create it with the minimum required GameObjects
3. Add the scene to Build Settings

- [ ] **Step 3: Add scene to Build Settings**

The DungeonExploration scene must be in the build settings for `SceneManager.LoadScene()` to work. This is typically done via the Unity Editor, but can be scripted:

```csharp
// In an Editor script or manually:
// Edit → Build Settings → Add "Assets/Scenes/DungeonExploration"
```

The implementing agent should verify this is done or add it programmatically via `EditorBuildSettings.scenes`.

- [ ] **Step 4: Compile check + commit**

```bash
cd "C:/Dev/Forever engine"
git add Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs
# Also add the scene file if created:
# git add Assets/Scenes/DungeonExploration.unity
git commit -m "feat: add DungeonSceneSetup bootstrap for exploration scene

Loads RoomCatalog, creates DungeonExplorer, handles return-from-battle
initialization. Scene needs DungeonExploration.unity with camera, light,
and this component."
```

---

### Task 19: Phase 3 integration verify

- [ ] **Step 1: Full compile check**

```bash
cd "C:/Dev/Forever engine"
RSP=$(find Library/Bee -name "Assembly-CSharp.rsp" 2>/dev/null | head -1)
if [ -n "$RSP" ]; then
  dotnet exec "$(find Library -name 'csc.dll' | head -1)" @"$RSP" 2>&1 | tail -30
fi
```

- [ ] **Step 2: Verify scene flow references**

Check that all scene names used in LoadScene calls exist:
```bash
cd "C:/Dev/Forever engine"
# Verify DungeonExploration scene exists
ls Assets/Scenes/DungeonExploration.unity 2>/dev/null || echo "MISSING: DungeonExploration scene"

# Verify all LoadScene references
grep -rn 'LoadScene(' Assets/Scripts/ | grep -v '\.meta'
```

- [ ] **Step 3: Verify namespace consistency**

```bash
cd "C:/Dev/Forever engine"
# Check all new Dungeon files use consistent namespace
grep -n 'namespace' Assets/Scripts/Demo/Dungeon/*.cs
# Expected: ForeverEngine.Demo.Dungeon in all files
```

- [ ] **Step 4: Tag phase completion**

```bash
cd "C:/Dev/Forever engine"
git tag phase3-dungeon-interiors
```

---

## Post-Implementation Checklist

After all three phases are complete:

- [ ] Create a RoomCatalog.asset ScriptableObject in `Assets/Resources/` and populate it with room prefabs from the purchased asset packs (Multistory Dungeons 2, Lordenfel, Eternal Temple)
- [ ] Create the DungeonExploration.unity scene with camera, directional light, and DungeonSceneSetup
- [ ] Add DungeonExploration to Build Settings
- [ ] Test battle flow: overworld → dungeon → encounter zone → battle → return to dungeon
- [ ] Test boss flow: defeat boss → dungeon completion → return to overworld
- [ ] Verify all GLB models load correctly in battle (check console for "MISSING" model warnings)
- [ ] Tune GameConfig AI values in inspector after observing a few battles
- [ ] Verify encounter templates produce mixed compositions (check battle log for role variety)
