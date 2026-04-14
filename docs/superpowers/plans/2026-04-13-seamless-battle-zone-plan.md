# Seamless Battle Zone Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace scene-based battle arenas with in-world per-enemy 8x8 grid zones. Combat happens seamlessly in the current scene with escape via perception check and dynamic enemy joining.

**Architecture:** BattleZone (per-enemy grid overlay with geometry scanning) replaces BattleRenderer3D's arena. BattleManager's combat logic is preserved but its initialization, rendering, and end-of-battle flow are rewritten to operate in world-space. GameManager, DungeonExplorer, OverworldManager, and EncounterZone are rewired to start battles without scene transitions.

**Tech Stack:** Unity 6 C#, Physics.OverlapBox for geometry scanning, LineRenderer for grid boundary, existing PerspectiveCameraController.

**Spec:** `docs/superpowers/specs/2026-04-13-seamless-battle-zone-design.md`

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `Assets/Scripts/Demo/Battle/BattleEffects.cs` | Create | Extract DamagePopup + HitFlash from BattleRenderer3D |
| `Assets/Scripts/Demo/Battle/BattleZone.cs` | Create | Per-enemy grid zone with geometry scan and boundary visual |
| `Assets/Scripts/Demo/Battle/WorldLoot.cs` | Create | Loot pickup MonoBehaviour at enemy death positions |
| `Assets/Scripts/Demo/Battle/BattleManager.cs` | Major rewrite | Multi-zone seamless battle, escape mechanic, dynamic join |
| `Assets/Scripts/Demo/GameManager.cs` | Modify | StartSeamlessBattle(), OnBattleComplete(), IsInCombat |
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | Modify | No scene load, IsInCombat guard |
| `Assets/Scripts/Demo/Overworld/OverworldManager.cs` | Modify | Seamless battle trigger |
| `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` | Modify | Call StartSeamlessBattle |
| `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs` | Gut | Remove arena geometry, keep file for reference only |
| `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs` | Remove | No longer needed |

---

### Task 1: Extract BattleEffects from BattleRenderer3D

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleEffects.cs`

- [ ] **Step 1: Create BattleEffects.cs**

Copy the `DamagePopup` and `HitFlash` classes from `BattleRenderer3D.cs` (lines 267-318) into a new file. These are self-contained MonoBehaviours with no dependency on the arena.

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class DamagePopup : UnityEngine.MonoBehaviour
    {
        private float _timer;
        private TextMesh _tm;

        private void Start() => _tm = GetComponent<TextMesh>();

        private void Update()
        {
            _timer += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 1.5f;
            if (_tm != null)
            {
                Color c = _tm.color;
                c.a = Mathf.Lerp(1f, 0f, _timer);
                _tm.color = c;
            }
            var cam = Camera.main;
            if (cam != null) transform.forward = cam.transform.forward;
            if (_timer >= 1f) Destroy(gameObject);
        }
    }

    public class HitFlash : UnityEngine.MonoBehaviour
    {
        private float _timer = -1f;
        private Renderer _mr;
        private Color _originalColor;
        private static readonly Color FLASH_COLOR = Color.white;
        private const float FLASH_DURATION = 0.15f;

        public void Trigger()
        {
            if (_mr == null) _mr = GetComponentInChildren<Renderer>();
            if (_mr == null) return;
            _originalColor = _mr.material.color;
            _mr.material.color = FLASH_COLOR;
            _timer = FLASH_DURATION;
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                if (_mr != null) _mr.material.color = _originalColor;
                _timer = -1f;
            }
        }
    }

    /// <summary>Spawn damage number or hit flash on a combatant model.</summary>
    public static class BattleEffectsHelper
    {
        public static void ShowDamage(GameObject model, int amount, bool isCrit)
        {
            if (model == null) return;
            var go = new GameObject("DmgNum");
            go.transform.position = model.transform.position + Vector3.up * 1.5f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = amount.ToString();
            tm.characterSize = 0.15f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            if (isCrit) tm.color = Color.yellow;
            else if (amount >= 15) tm.color = Color.red;
            else if (amount >= 8) tm.color = new Color(1f, 0.6f, 0f);
            else tm.color = Color.white;

            go.AddComponent<DamagePopup>();
        }

        public static void ShowHitFlash(GameObject model)
        {
            if (model == null) return;
            var flash = model.GetComponent<HitFlash>();
            if (flash == null) flash = model.AddComponent<HitFlash>();
            flash.Trigger();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleEffects.cs && git commit -m "refactor: extract DamagePopup + HitFlash into BattleEffects.cs"
```

---

### Task 2: BattleZone

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleZone.cs`

- [ ] **Step 1: Create BattleZone.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class BattleZone : UnityEngine.MonoBehaviour
    {
        public BattleCombatant OwnerEnemy { get; private set; }
        public Vector3 Origin { get; private set; }
        public BattleGrid Grid { get; private set; }

        public const int GridSize = 8;
        public const float CellSize = 1f;

        private LineRenderer _boundary;
        private static readonly Color BoundaryColor = new(0.7f, 0.85f, 1f, 0.8f);

        public void Activate(BattleCombatant owner, Vector3 position)
        {
            OwnerEnemy = owner;
            Origin = SnapToGrid(position);
            Grid = new BattleGrid(GridSize, GridSize, position.GetHashCode());
            ScanGeometry();
            CreateBoundaryVisual();
        }

        public void ReCenter(Vector3 newPosition)
        {
            Origin = SnapToGrid(newPosition);
            ScanGeometry();
            UpdateBoundaryVisual();
        }

        public void Deactivate()
        {
            if (_boundary != null) Destroy(_boundary.gameObject);
            Destroy(gameObject);
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return Origin + new Vector3(
                (x - GridSize / 2) * CellSize + CellSize * 0.5f,
                0.1f,
                (y - GridSize / 2) * CellSize + CellSize * 0.5f);
        }

        public (int x, int y) WorldToGrid(Vector3 pos)
        {
            Vector3 local = pos - Origin;
            int x = Mathf.FloorToInt(local.x / CellSize) + GridSize / 2;
            int y = Mathf.FloorToInt(local.z / CellSize) + GridSize / 2;
            return (Mathf.Clamp(x, 0, GridSize - 1), Mathf.Clamp(y, 0, GridSize - 1));
        }

        public bool ContainsWorldPos(Vector3 pos)
        {
            var (x, y) = WorldToGrid(pos);
            return x >= 0 && x < GridSize && y >= 0 && y < GridSize;
        }

        private void ScanGeometry()
        {
            // Reset all cells to walkable, then mark occupied cells
            for (int i = 0; i < Grid.Walkable.Length; i++)
                Grid.Walkable[i] = true;

            // Border cells non-walkable
            for (int y = 0; y < GridSize; y++)
                for (int x = 0; x < GridSize; x++)
                {
                    if (x == 0 || x == GridSize - 1 || y == 0 || y == GridSize - 1)
                    {
                        Grid.Walkable[y * GridSize + x] = false;
                        continue;
                    }

                    Vector3 cellWorld = GridToWorld(x, y);
                    var halfExtents = new Vector3(CellSize * 0.45f, 0.5f, CellSize * 0.45f);
                    var colliders = Physics.OverlapBox(cellWorld, halfExtents);

                    foreach (var col in colliders)
                    {
                        // Skip player and combatant objects
                        if (col.CompareTag("Player")) continue;
                        if (col.GetComponent<BattleCombatant>() != null) continue;
                        if (col.GetComponentInParent<Dungeon.DungeonNPC>() != null) continue;

                        // Found scenery — mark non-walkable
                        Grid.Walkable[y * GridSize + x] = false;
                        break;
                    }
                }

            // Ensure player start area is walkable (center cells)
            int mid = GridSize / 2;
            Grid.Walkable[mid * GridSize + mid] = true;
            Grid.Walkable[mid * GridSize + (mid - 1)] = true;
            Grid.Walkable[(mid - 1) * GridSize + mid] = true;
        }

        private void CreateBoundaryVisual()
        {
            var go = new GameObject("ZoneBoundary");
            go.transform.SetParent(transform);
            _boundary = go.AddComponent<LineRenderer>();

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = BoundaryColor;
            _boundary.material = mat;
            _boundary.startWidth = 0.06f;
            _boundary.endWidth = 0.06f;
            _boundary.positionCount = 5;
            _boundary.loop = false;
            _boundary.useWorldSpace = true;

            UpdateBoundaryVisual();
        }

        private void UpdateBoundaryVisual()
        {
            if (_boundary == null) return;

            float halfGrid = GridSize * CellSize / 2f;
            float y = 0.05f; // slightly above ground
            Vector3[] corners = new Vector3[]
            {
                Origin + new Vector3(-halfGrid, y, -halfGrid),
                Origin + new Vector3(halfGrid, y, -halfGrid),
                Origin + new Vector3(halfGrid, y, halfGrid),
                Origin + new Vector3(-halfGrid, y, halfGrid),
                Origin + new Vector3(-halfGrid, y, -halfGrid), // close loop
            };
            _boundary.SetPositions(corners);
        }

        private static Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(
                Mathf.Round(pos.x),
                pos.y,
                Mathf.Round(pos.z));
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleZone.cs && git commit -m "feat: add BattleZone — per-enemy 8x8 grid overlay with geometry scanning"
```

---

### Task 3: WorldLoot

**Files:**
- Create: `Assets/Scripts/Demo/Battle/WorldLoot.cs`

- [ ] **Step 1: Create WorldLoot.cs**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class WorldLoot : UnityEngine.MonoBehaviour
    {
        public int GoldAmount;
        public int XPAmount;

        private float _bobTimer;
        private Vector3 _basePos;
        private float _despawnTimer;
        private const float DespawnTime = 60f;
        private const float CollectRadius = 1.5f;

        private void Start()
        {
            _basePos = transform.position;

            // Visual: small gold cube
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            var goldColor = new Color(1f, 0.84f, 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", goldColor);
            else mat.color = goldColor;
            cube.GetComponent<Renderer>().material = mat;
            var col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void Update()
        {
            // Bob animation
            _bobTimer += Time.deltaTime;
            transform.position = _basePos + Vector3.up * (Mathf.Sin(_bobTimer * 3f) * 0.15f);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);

            // Auto-despawn
            _despawnTimer += Time.deltaTime;
            if (_despawnTimer >= DespawnTime) { Destroy(gameObject); return; }

            // Auto-collect when player walks near
            var player = GameObject.FindWithTag("Player");
            if (player != null && Vector3.Distance(transform.position, player.transform.position) <= CollectRadius)
                Collect();
        }

        private void Collect()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (gm.Player != null) gm.Player.Gold += GoldAmount;
                if (gm.Character != null) gm.Character.GainXP(XPAmount);
            }

            // Floating text popup
            var go = new GameObject("LootPopup");
            go.transform.position = transform.position + Vector3.up * 0.5f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = $"+{GoldAmount}g +{XPAmount}xp";
            tm.characterSize = 0.12f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.84f, 0f);
            go.AddComponent<DamagePopup>(); // Reuse float-up-and-fade behavior

            Debug.Log($"[WorldLoot] Collected: {GoldAmount} gold, {XPAmount} XP");
            Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/WorldLoot.cs && git commit -m "feat: add WorldLoot pickup with bob animation and auto-collect"
```

---

### Task 4: GameManager Seamless Battle API

**Files:**
- Modify: `Assets/Scripts/Demo/GameManager.cs`

- [ ] **Step 1: Add IsInCombat property and active battle tracking**

After the existing properties (around line 25, after `public int LastBattleXPEarned`), add:

```csharp
        public bool IsInCombat { get; private set; }
        private Battle.BattleManager _activeBattleManager;
        private readonly List<Battle.BattleZone> _activeZones = new();
```

Add `using System.Collections.Generic;` at the top if not already present.

- [ ] **Step 2: Add StartSeamlessBattle method**

After the existing `EnterBattle` method (line 170), add the new seamless method:

```csharp
        public void StartSeamlessBattle(Vector3 position, string encounterId)
        {
            if (IsInCombat) return; // Already in combat

            var encounterData = Encounters.EncounterData.Get(encounterId);
            encounterData = Encounters.EncounterManager.Instance?.ScaleEncounter(encounterData) ?? encounterData;

            // Create one BattleZone per enemy, offset slightly from encounter position
            _activeZones.Clear();
            var rng = new System.Random(encounterId.GetHashCode());
            float offset = 0f;
            foreach (var enemyDef in encounterData.Enemies)
            {
                var zoneGO = new GameObject($"BattleZone_{enemyDef.Name}");
                var zone = zoneGO.AddComponent<Battle.BattleZone>();
                Vector3 enemyPos = position + new Vector3(offset, 0, offset * 0.5f);
                var combatant = Battle.BattleCombatant.FromEnemy(enemyDef, 0, 0); // grid pos set after zone activates
                zone.Activate(combatant, enemyPos);

                // Place enemy on zone grid
                var (gx, gy) = zone.WorldToGrid(enemyPos);
                combatant.X = gx;
                combatant.Y = gy;

                _activeZones.Add(zone);
                offset += 2f;
            }

            // Create or get BattleManager
            if (_activeBattleManager == null)
            {
                var bmGO = new GameObject("BattleManager");
                _activeBattleManager = bmGO.AddComponent<Battle.BattleManager>();
            }

            // Create player combatant
            Battle.BattleCombatant playerCombatant;
            if (Character != null)
                playerCombatant = Battle.BattleCombatant.FromCharacterSheet(Character);
            else
                playerCombatant = Battle.BattleCombatant.FromPlayer(Player);

            // Place player on first zone grid center
            if (_activeZones.Count > 0)
            {
                var firstZone = _activeZones[0];
                playerCombatant.X = Battle.BattleZone.GridSize / 2;
                playerCombatant.Y = Battle.BattleZone.GridSize / 2 - 2;
            }

            var enemies = new List<Battle.BattleCombatant>();
            foreach (var zone in _activeZones)
                enemies.Add(zone.OwnerEnemy);

            _activeBattleManager.StartSeamlessBattle(_activeZones, enemies, playerCombatant, encounterData);

            PendingEncounterId = encounterId;
            IsInCombat = true;
        }

        public void OnBattleComplete(bool playerWon, Encounters.EncounterData encounterData)
        {
            // Spawn loot at defeated enemy positions
            if (playerWon && encounterData != null)
            {
                int goldPer = encounterData.GoldReward / Mathf.Max(1, encounterData.Enemies.Count);
                int xpPer = encounterData.XPReward / Mathf.Max(1, encounterData.Enemies.Count);
                foreach (var zone in _activeZones)
                {
                    if (zone != null && zone.OwnerEnemy != null && !zone.OwnerEnemy.IsAlive)
                    {
                        var lootGO = new GameObject("WorldLoot");
                        lootGO.transform.position = zone.GridToWorld(zone.OwnerEnemy.X, zone.OwnerEnemy.Y) + Vector3.up * 0.5f;
                        var loot = lootGO.AddComponent<Battle.WorldLoot>();
                        loot.GoldAmount = goldPer;
                        loot.XPAmount = xpPer;
                    }
                }
            }

            // Clean up zones
            foreach (var zone in _activeZones)
                if (zone != null) zone.Deactivate();
            _activeZones.Clear();

            // Clean up BattleManager
            if (_activeBattleManager != null)
            {
                Destroy(_activeBattleManager.gameObject);
                _activeBattleManager = null;
            }

            LastBattleWon = playerWon;
            IsInCombat = false;
            PendingEncounterId = null;

            if (!playerWon)
                PlayerDied();
        }
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/GameManager.cs && git commit -m "feat: add StartSeamlessBattle + OnBattleComplete to GameManager"
```

---

### Task 5: BattleManager Rewrite — Seamless Entry

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs`

This is the largest change. The combat logic (lines 298-1137: StartTurn, NextTurn, ProcessAITurn, ResolveAttack, CheckBattleEnd, spells, potions, movement) stays **exactly as-is**. We rewrite:
1. Fields (add zone tracking, remove renderer references)
2. `Start()` (gut the scene-load initialization)
3. `Update()` (remove renderer calls, add model visual updates inline)
4. Add `StartSeamlessBattle()` new entry point
5. Rewrite `CheckBattleEnd()` to call `GameManager.OnBattleComplete()`
6. Add escape perception check
7. Add dynamic enemy join check
8. Add model management (spawn/track models in world space)

- [ ] **Step 1: Update fields and add new state**

Replace the field block (lines 16-37) with:

```csharp
        public static BattleManager Instance { get; private set; }

        public BattleGrid Grid { get; private set; }
        public List<BattleCombatant> Combatants { get; private set; } = new();
        public BattleCombatant CurrentTurn { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public bool BattleOver { get; private set; }
        public bool PlayerWon { get; private set; }
        public List<string> Log { get; private set; } = new();

        private int _turnIndex;
        private uint _rngSeed;
        private Encounters.EncounterData _encounterData;
        private Dictionary<BattleCombatant, CombatBrain> _brains = new();
        private CombatIntelligence _neuralBrain;
        private GameConfig _gameConfig;

        // Seamless battle state
        private List<BattleZone> _zones = new();
        private Dictionary<BattleCombatant, GameObject> _models = new();
        private bool _seamlessMode;

        // Spell casting UI state
        private bool _spellMenuOpen;
        private List<SpellData> _availableSpells = new();
```

- [ ] **Step 2: Add StartSeamlessBattle method**

Add this method after `Awake()`:

```csharp
        public void StartSeamlessBattle(List<BattleZone> zones, List<BattleCombatant> enemies,
            BattleCombatant player, Encounters.EncounterData encounterData)
        {
            _gameConfig = Resources.Load<GameConfig>("GameConfig");
            _zones = zones;
            _encounterData = encounterData;
            _seamlessMode = true;
            _rngSeed = (uint)(Time.frameCount + encounterData.Id.GetHashCode());

            // Add player
            Combatants.Clear();
            Combatants.Add(player);

            // Spawn enemy models and add to combatants
            foreach (var enemy in enemies)
            {
                Combatants.Add(enemy);
                var zone = zones.Find(z => z.OwnerEnemy == enemy);
                if (zone != null)
                    SpawnModelForCombatant(enemy, zone);
            }

            // Player model already exists in the world — find and track it
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
                _models[player] = playerGO;

            // Roll initiative, player first in round 1
            foreach (var c in Combatants) c.RollInitiative(ref _rngSeed);
            Combatants = Combatants.OrderByDescending(c => c.InitiativeRoll).ToList();
            int playerIdx = Combatants.FindIndex(c => c.IsPlayer);
            if (playerIdx > 0)
            {
                var p = Combatants[playerIdx];
                Combatants.RemoveAt(playerIdx);
                Combatants.Insert(0, p);
            }

            // Setup AI brains
            foreach (var c in Combatants)
            {
                if (!c.IsPlayer && c.IsAlive)
                    _brains[c] = new CombatBrain(null, seed: (int)_rngSeed + c.X * 100 + c.Y);
            }

            // Neural brain
            var inferEngine = ForeverEngine.AI.Inference.InferenceEngine.Instance;
            if (inferEngine != null && inferEngine.IsAvailable)
            {
                var go = new GameObject("CombatIntelligence");
                go.transform.SetParent(transform);
                _neuralBrain = go.AddComponent<CombatIntelligence>();
            }

            // Use first zone's grid as the primary grid for movement checks
            if (_zones.Count > 0)
                Grid = _zones[0].Grid;

            Demo.AI.DirectorEvents.Send($"combat started: {encounterData.Id}");
            StartTurn();
            Log.Add($"Battle begins! {enemies.Count} enemies.");
        }

        private void SpawnModelForCombatant(BattleCombatant combatant, BattleZone zone)
        {
            GameObject model = null;
            if (!string.IsNullOrEmpty(combatant.ModelId))
            {
                var prefab = Resources.Load<GameObject>($"Models/{combatant.ModelId}");
                if (prefab != null)
                {
                    model = Instantiate(prefab);
                    model.transform.localScale *= combatant.ModelScale;
                }
            }

            if (model == null)
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
                var mr = model.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.9f, 0.2f, 0.2f));
                else mat.color = new Color(0.9f, 0.2f, 0.2f);
                mr.material = mat;
                var col = model.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            model.name = $"Model_{combatant.Name}";
            model.transform.position = zone.GridToWorld(combatant.X, combatant.Y);
            _models[combatant] = model;
        }
```

- [ ] **Step 3: Rewrite Update() for seamless mode**

Replace the `Update()` method (lines 41-77) with:

```csharp
        private void Update()
        {
            if (_seamlessMode)
                UpdateSeamlessVisuals();

            // Player input during their turn
            if (CurrentTurn != null && CurrentTurn.IsPlayer && !BattleOver)
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                    CameraRelativeMove(0, 1);
                else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                    CameraRelativeMove(0, -1);
                else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                    CameraRelativeMove(-1, 0);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    CameraRelativeMove(1, 0);
                else if (!_spellMenuOpen && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F)))
                    AttackNearestEnemy();
                else if (Input.GetKeyDown(KeyCode.Q))
                    ToggleSpellMenu();
                else if (_spellMenuOpen)
                    HandleSpellInput();
                else if (Input.GetKeyDown(KeyCode.H))
                    UseHealthPotion();
                else if (Input.GetKeyDown(KeyCode.Space)) PlayerEndTurn();
            }
        }

        private void UpdateSeamlessVisuals()
        {
            foreach (var c in Combatants)
            {
                if (!_models.TryGetValue(c, out var model) || model == null) continue;

                // Get the zone this combatant belongs to
                BattleZone zone = c.IsPlayer ? (_zones.Count > 0 ? _zones[0] : null)
                    : _zones.Find(z => z.OwnerEnemy == c);
                if (zone == null) continue;

                Vector3 targetPos = zone.GridToWorld(c.X, c.Y);
                model.transform.position = Vector3.Lerp(model.transform.position, targetPos, Time.deltaTime * 12f);

                if (!c.IsAlive && model.activeSelf)
                {
                    model.transform.rotation = Quaternion.Lerp(
                        model.transform.rotation, Quaternion.Euler(90f, 0f, 0f), Time.deltaTime * 4f);
                    var mr = model.GetComponentInChildren<Renderer>();
                    if (mr != null)
                    {
                        Color col = mr.material.color;
                        col.a = Mathf.Lerp(col.a, 0f, Time.deltaTime * 2f);
                        mr.material.color = col;
                        if (col.a < 0.05f) model.SetActive(false);
                    }
                }

                if (c.IsAlive && c == CurrentTurn)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
                    float baseScale = c.IsPlayer ? 1f : 0.6f * c.ModelScale;
                    model.transform.localScale = Vector3.one * baseScale * pulse;
                }
            }

            // Check for dynamic enemy joining (once per round start)
            if (_seamlessMode && RoundNumber > _lastJoinCheckRound)
            {
                _lastJoinCheckRound = RoundNumber;
                CheckDynamicEnemyJoin();
            }
        }

        private int _lastJoinCheckRound;
```

- [ ] **Step 4: Gut Start() — keep as fallback only**

Replace the `Start()` method (lines 80-194) with:

```csharp
        private void Start()
        {
            // Seamless battles are initialized via StartSeamlessBattle(), not Start().
            // This remains for backwards compatibility if BattleMap scene is still loaded directly.
            if (_seamlessMode) return;

            _gameConfig = Resources.Load<GameConfig>("GameConfig");
            var gm = GameManager.Instance;
            if (gm == null || string.IsNullOrEmpty(gm.PendingEncounterId)) return;

            // Legacy scene-based battle path (will be removed in future cleanup)
            Debug.LogWarning("[BattleManager] Legacy scene-based battle — use StartSeamlessBattle instead");
        }
```

- [ ] **Step 5: Add escape perception check**

Add this method after `PlayerMove`:

```csharp
        /// <summary>
        /// After player moves, check if they've exited any enemy's zone.
        /// Triggers perception check for escape.
        /// </summary>
        private void CheckPlayerEscape()
        {
            if (!_seamlessMode || _zones.Count == 0) return;

            var player = Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player == null) return;

            // Get player world position from their current grid position in the first zone
            var firstZone = _zones[0];
            Vector3 playerWorld = firstZone.GridToWorld(player.X, player.Y);

            var zonesToRemove = new List<BattleZone>();
            foreach (var zone in _zones)
            {
                if (!zone.ContainsWorldPos(playerWorld) && zone.OwnerEnemy.IsAlive)
                {
                    // Player has exited this zone — enemy rolls perception
                    int playerDex = 10;
                    var gm = GameManager.Instance;
                    if (gm?.Character != null) playerDex = gm.Character.Dex;
                    else if (gm?.Player != null) playerDex = gm.Player.Dex;
                    int dexMod = (playerDex - 10) / 2;
                    int dc = 10 + dexMod;

                    int roll = Random.Range(1, 21);
                    if (roll >= dc)
                    {
                        // Enemy spotted player — pull back
                        Log.Add($"{zone.OwnerEnemy.Name} spots you! (rolled {roll} vs DC {dc})");
                        // Snap player back to nearest valid cell inside zone
                        var (bx, by) = zone.WorldToGrid(playerWorld);
                        bx = Mathf.Clamp(bx, 1, BattleZone.GridSize - 2);
                        by = Mathf.Clamp(by, 1, BattleZone.GridSize - 2);
                        player.X = bx;
                        player.Y = by;
                    }
                    else
                    {
                        // Escaped this enemy
                        Log.Add($"You slip away from {zone.OwnerEnemy.Name}! (rolled {roll} vs DC {dc})");
                        zonesToRemove.Add(zone);
                    }
                }
            }

            foreach (var zone in zonesToRemove)
            {
                // Remove enemy from combat
                var enemy = zone.OwnerEnemy;
                Combatants.Remove(enemy);
                if (_models.TryGetValue(enemy, out var model))
                {
                    // Enemy goes back to patrol (don't destroy model)
                    _models.Remove(enemy);
                }
                _brains.Remove(enemy);
                zone.Deactivate();
                _zones.Remove(zone);
            }

            // If no zones left, combat is over
            if (_zones.Count == 0)
            {
                BattleOver = true;
                PlayerWon = true;
                Log.Add("Escaped all enemies!");
                GameManager.Instance?.OnBattleComplete(true, _encounterData);
            }
        }
```

- [ ] **Step 6: Add dynamic enemy join check**

```csharp
        private void CheckDynamicEnemyJoin()
        {
            if (!_seamlessMode) return;
            var player = Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player == null) return;

            var firstZone = _zones.Count > 0 ? _zones[0] : null;
            if (firstZone == null) return;
            Vector3 playerWorld = firstZone.GridToWorld(player.X, player.Y);

            var dungeonNPCs = FindObjectsByType<Dungeon.DungeonNPC>(FindObjectsSortMode.None);
            foreach (var npc in dungeonNPCs)
            {
                if (npc.Role != Dungeon.DungeonNPCRole.AmbientEnemy) continue;
                // Check if this enemy's hypothetical zone would contain the player
                float dist = Vector3.Distance(npc.transform.position, playerWorld);
                if (dist > BattleZone.GridSize * BattleZone.CellSize) continue;

                // Already in combat?
                if (Combatants.Any(c => !c.IsPlayer && c.Name == npc.NPCName
                    && Mathf.Abs(c.X - player.X) < 10)) continue;

                // Join the fight
                Log.Add($"{npc.NPCName} joins the fight!");
                var enemyDef = new Encounters.EnemyDef
                {
                    Name = npc.NPCName, HP = 15, AC = 12, Str = 12, Dex = 12,
                    Spd = 5, AtkDice = "1d8+1", Behavior = "chase"
                };
                var (modelPath, modelScale) = ModelRegistry.Resolve(npc.NPCName);
                if (modelPath != null) { enemyDef.ModelId = modelPath; enemyDef.ModelScale = modelScale; }

                var combatant = BattleCombatant.FromEnemy(enemyDef, BattleZone.GridSize / 2, BattleZone.GridSize - 2);

                var zoneGO = new GameObject($"BattleZone_{npc.NPCName}");
                var zone = zoneGO.AddComponent<BattleZone>();
                zone.Activate(combatant, npc.transform.position);
                _zones.Add(zone);

                combatant.RollInitiative(ref _rngSeed);
                Combatants.Add(combatant);
                _brains[combatant] = new CombatBrain(null, seed: (int)_rngSeed + combatant.X * 100);

                SpawnModelForCombatant(combatant, zone);

                // Destroy the dungeon NPC (now represented by the combatant)
                Destroy(npc.gameObject);
                break; // One join per round
            }
        }
```

- [ ] **Step 7: Wire escape check into PlayerMove**

In the existing `PlayerMove` method, after the player position is updated (after `CurrentTurn.MovementRemaining--;`), add:

```csharp
            CheckPlayerEscape();
```

- [ ] **Step 8: Rewrite CheckBattleEnd for seamless mode**

In `CheckBattleEnd()`, replace the victory block (lines 1085-1116) — after the `if (playerDead)` block, change the victory condition:

Replace:
```csharp
            else if (Combatants.All(c => c.IsPlayer || !c.IsAlive))
            {
                BattleOver = true; PlayerWon = true;
                Log.Add("Victory!");
                Audio.SoundManager.Instance?.PlayVictory();
                // ... existing reward logic ...
            }
```

With:
```csharp
            else if (Combatants.All(c => c.IsPlayer || !c.IsAlive))
            {
                BattleOver = true; PlayerWon = true;
                Log.Add("Victory!");
                Audio.SoundManager.Instance?.PlayVictory();
                Demo.AI.DirectorEvents.Send(
                    $"victory: gold={_encounterData?.GoldReward ?? 0} xp={_encounterData?.XPReward ?? 0}");

                if (_seamlessMode)
                {
                    // Seamless: GameManager handles loot and cleanup
                    GameManager.Instance?.OnBattleComplete(true, _encounterData);
                }
                else
                {
                    // Legacy path
                    var gm = GameManager.Instance;
                    if (gm != null)
                    {
                        gm.LastBattleWon = true;
                        gm.LastBattleGoldEarned = _encounterData?.GoldReward ?? 0;
                        gm.LastBattleXPEarned = _encounterData?.XPReward ?? 0;
                    }
                }
            }
```

And update the player death path similarly:
```csharp
            if (playerDead)
            {
                BattleOver = true; PlayerWon = false;
                Log.Add("You have fallen...");
                Demo.AI.DirectorEvents.Send("player died");
                if (_seamlessMode)
                    GameManager.Instance?.OnBattleComplete(false, _encounterData);
            }
```

- [ ] **Step 9: Re-center zones on enemy turns**

In `ProcessAITurn()`, after the enemy moves (in the switch cases for Advance/Retreat/Flank/Hold), add zone re-centering. After the switch block (around line 800), add:

```csharp
            // Re-center zone on enemy's new position
            if (_seamlessMode && _models.TryGetValue(ai, out var aiModel))
            {
                var zone = _zones.Find(z => z.OwnerEnemy == ai);
                if (zone != null)
                    zone.ReCenter(aiModel.transform.position);
            }
```

- [ ] **Step 10: Add ShowDamage and ShowHitFlash using BattleEffectsHelper**

Find all calls to `_renderer3D.ShowDamage` and `_renderer3D.ShowHitFlash` in the file and add seamless alternatives. In `ResolveAttack` (around line 844), where damage is shown, add:

After any existing `_renderer3D?.ShowDamage(target, damage, isCrit);` call, add:
```csharp
                if (_seamlessMode && _models.TryGetValue(target, out var targetModel))
                    BattleEffectsHelper.ShowDamage(targetModel, damage, isCrit);
```

After any existing `_renderer3D?.ShowHitFlash(target);` call, add:
```csharp
                if (_seamlessMode && _models.TryGetValue(target, out var flashModel))
                    BattleEffectsHelper.ShowHitFlash(flashModel);
```

- [ ] **Step 11: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleManager.cs && git commit -m "feat: rewrite BattleManager for seamless in-world combat with escape and dynamic join"
```

---

### Task 6: Rewire DungeonExplorer and EncounterZone

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`
- Modify: `Assets/Scripts/Demo/Dungeon/EncounterZone.cs`

- [ ] **Step 1: Rewrite DungeonExplorer.EnterBattle**

Replace the `EnterBattle` method (lines 103-114) with:

```csharp
        public void EnterBattle(string encounterId, int zoneIndex, bool isBoss)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Get encounter zone position for seamless battle
            Vector3 battlePos = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            if (_daBuilder != null && _daBuilder.Rooms != null && zoneIndex >= 0 && zoneIndex < _daBuilder.Rooms.Length)
                battlePos = _daBuilder.Rooms[zoneIndex].WorldBounds.center;

            Debug.Log($"[DungeonExplorer] Starting seamless battle: {encounterId} (zone {zoneIndex}, boss={isBoss})");
            gm.StartSeamlessBattle(battlePos, encounterId);
        }
```

- [ ] **Step 2: Add IsInCombat guard to HandleMovement**

At the top of `HandleMovement()` (line 253), add:

```csharp
            if (GameManager.Instance?.IsInCombat == true) return;
```

- [ ] **Step 3: Rewrite EncounterZone.OnTriggerEnter**

Replace the `OnTriggerEnter` method in EncounterZone.cs with:

```csharp
        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag("Player")) return;
            _triggered = true;

            // Destroy ambient enemy NPCs in this room (combat system spawns its own)
            var allNPCs = FindObjectsByType<DungeonNPC>(FindObjectsSortMode.None);
            foreach (var npc in allNPCs)
            {
                if (npc.Role == DungeonNPCRole.AmbientEnemy && npc.RoomIndex == ZoneIndex)
                    Destroy(npc.gameObject);
            }

            // Start seamless battle at this zone's position
            var gm = GameManager.Instance;
            if (gm != null)
                gm.StartSeamlessBattle(transform.position, EncounterId);
        }
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs Assets/Scripts/Demo/Dungeon/EncounterZone.cs && git commit -m "feat: rewire DungeonExplorer + EncounterZone for seamless battles"
```

---

### Task 7: Rewire OverworldManager

**Files:**
- Modify: `Assets/Scripts/Demo/Overworld/OverworldManager.cs`

- [ ] **Step 1: Replace encounter trigger**

Find the encounter trigger block (around line 266-270):

```csharp
            if (Random.Range(0f, 1f) < chance)
            {
                EncountersSinceRest++;
                GameManager.Instance.EnterBattle($"random_{tile.Type}_{(IsNight ? "night" : "day")}");
            }
```

Replace with:

```csharp
            if (Random.Range(0f, 1f) < chance)
            {
                EncountersSinceRest++;
                var playerPos = Player?.transform?.position ?? Vector3.zero;
                GameManager.Instance.StartSeamlessBattle(playerPos, $"random_{tile.Type}_{(IsNight ? "night" : "day")}");
            }
```

- [ ] **Step 2: Add IsInCombat movement guard**

In the overworld movement handling method, add at the start:

```csharp
            if (GameManager.Instance?.IsInCombat == true) return;
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Overworld/OverworldManager.cs && git commit -m "feat: rewire OverworldManager for seamless battles"
```

---

### Task 8: Gut BattleRenderer3D and Remove BattleSceneTemplate

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs`
- Delete: `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs`

- [ ] **Step 1: Gut BattleRenderer3D**

Replace the entire file content with a stub that explains the removal:

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// DEPRECATED: Arena-based battle rendering replaced by seamless in-world BattleZone system.
    /// DamagePopup and HitFlash moved to BattleEffects.cs.
    /// This file retained only for the BattleGridOverlay reference used by BattleInputController.
    /// Will be fully removed once BattleInputController is updated.
    /// </summary>
    public class BattleRenderer3D : UnityEngine.MonoBehaviour
    {
        public void Initialize(BattleSceneTemplate template, BattleGrid grid,
            List<BattleCombatant> combatants, Camera cam) { }
        public void UpdateVisuals(List<BattleCombatant> combatants, BattleCombatant currentTurn) { }
        public void ShowDamage(BattleCombatant target, int amount, bool isCrit) { }
        public void ShowHitFlash(BattleCombatant target) { }
        public void ShowPathPreview(BattleGrid grid, BattleCombatant mover,
            int targetX, int targetY, List<BattleCombatant> combatants) { }
        public void ClearPathPreview() { }
        public Vector3 GridToWorld(int x, int y) => Vector3.zero;
        public (int x, int y) WorldToGrid(Vector3 pos) => (0, 0);
        public void Cleanup() { }
    }
}
```

- [ ] **Step 2: Gut BattleSceneTemplate**

Replace with a minimal stub:

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>DEPRECATED: Replaced by seamless BattleZone system.</summary>
    public enum ArenaType { Dungeon, Boss, Overworld }

    [CreateAssetMenu(fileName = "BattleTemplate", menuName = "Forever/Battle Scene Template")]
    public class BattleSceneTemplate : ScriptableObject
    {
        public GameObject RoomPrefab;
        public int GridWidth = 8;
        public int GridHeight = 8;
        public ArenaType Arena = ArenaType.Dungeon;
        public string Biome = "dungeon";
        public bool IsBossArena;
        public Color AmbientColor = new Color(0.3f, 0.3f, 0.4f);
        public float LightIntensity = 1.0f;
        public Vector2Int[] PlayerSpawnZone;
        public Vector2Int[] EnemySpawnZone;
        public Vector2Int[] BossSpawnPoints;
        public GameObject[] ObstacleProps;
    }
}
```

Note: We keep the ScriptableObject as a stub rather than deleting because existing `.asset` files in Resources/BattleTemplates/ reference this type. Deleting the class would cause Unity import errors for those assets. The assets can be cleaned up separately.

- [ ] **Step 3: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Battle/BattleRenderer3D.cs Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs && git commit -m "refactor: gut BattleRenderer3D and BattleSceneTemplate (replaced by BattleZone)"
```

---

### Task 9: Final Compile Check and Integration Verification

- [ ] **Step 1: Verify all files**

```bash
cd "C:/Dev/Forever engine" && git diff --stat $(git log --oneline -10 | tail -1 | cut -d' ' -f1)..HEAD -- Assets/Scripts/
```

- [ ] **Step 2: Check for broken references**

```bash
cd "C:/Dev/Forever engine" && grep -r "BattleRenderer3D\|FindBattleTemplate\|BattleSceneTemplate" Assets/Scripts/ --include="*.cs" | grep -v "BattleRenderer3D.cs" | grep -v "BattleSceneTemplate.cs" | grep -v "DEPRECATED"
```

Fix any remaining references to old types.

- [ ] **Step 3: Verify commit history**

```bash
cd "C:/Dev/Forever engine" && git log --oneline -10
```

- [ ] **Step 4: Fix any issues**

```bash
cd "C:/Dev/Forever engine" && git add -u && git commit -m "fix: resolve compile issues from seamless battle zone implementation"
```
