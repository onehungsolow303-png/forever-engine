# 3D Battle Scenes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 2D procedural grid battle renderer with 3D dungeon room prefabs, BG3-style grid overlay, 3D character models, and mouse+keyboard hybrid input while keeping BattleManager game logic unchanged.

**Architecture:** BattleManager stays as-is. A new BattleRenderer3D replaces the 2D renderer, loading room prefabs from BattleSceneTemplate ScriptableObjects with runtime variation. Mouse+keyboard input via BattleInputController raycasts onto the grid plane. UI Toolkit for HUD/action bar (no enemy HP exposed).

**Tech Stack:** Unity 6 / C# / UI Toolkit / URP / MonoBehaviour

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs` | CREATE | ScriptableObject: room prefab, grid size, spawn zones, biome, boss flag |
| `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs` | CREATE | Instantiate room, spawn models, manage visuals, damage popups, death FX |
| `Assets/Scripts/Demo/Battle/BattleGridOverlay.cs` | CREATE | On-demand grid mesh overlay: reachable (blue), threatened (red), path (bright), current (white) |
| `Assets/Scripts/Demo/Battle/BattleInputController.cs` | CREATE | Mouse raycast → tile selection, click-to-move, click-to-attack, hover inspect |
| `Assets/Scripts/Demo/Battle/BattleVariation.cs` | CREATE | Runtime randomizer: props, lighting, rotation, obstacle density |
| `Assets/Scripts/Demo/Battle/BattleUI.cs` | CREATE | UI Toolkit: player HUD, action bar, enemy inspect tooltip, damage numbers |
| `Assets/Scripts/Demo/Battle/BattleManager.cs` | MODIFY | Swap renderer creation to use BattleRenderer3D when in 3D mode |
| `Assets/Scripts/Demo/Battle/BattleCombatant.cs` | MODIFY | Add `ModelId` field |
| `Assets/Scripts/Demo/Encounters/EncounterData.cs` | MODIFY | Add `ModelId` to EnemyDef, add `Biome` field to EncounterData |
| `Assets/Scripts/Demo/GameManager.cs` | MODIFY | Add `Use3DBattle` flag, route to 3D renderer |

---

### Task 1: BattleSceneTemplate ScriptableObject

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs`

- [ ] **Step 1: Create the ScriptableObject class**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    [CreateAssetMenu(fileName = "BattleTemplate", menuName = "Forever/Battle Scene Template")]
    public class BattleSceneTemplate : ScriptableObject
    {
        [Header("Room")]
        public GameObject RoomPrefab;
        public int GridWidth = 8;
        public int GridHeight = 8;

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

- [ ] **Step 2: Compile check**

```bash
cd "C:/Dev/Forever engine"
CSC="C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll"
RSP=$(find "Library/Bee" -name "ForeverEngine.rsp" 2>/dev/null | head -1)
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

Expected: exit code 0.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleSceneTemplate.cs
git commit -m "feat: add BattleSceneTemplate ScriptableObject for 3D battle rooms"
```

---

### Task 2: BattleRenderer3D — Foundation

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleRenderer3D.cs`

This is the core renderer. Starts minimal: instantiate room prefab, create capsule tokens for combatants (model import comes in Task 6), position camera, handle turn indicator and death visuals.

- [ ] **Step 1: Create the renderer class**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleRenderer3D : UnityEngine.MonoBehaviour
    {
        private BattleSceneTemplate _template;
        private GameObject _roomInstance;
        private Dictionary<BattleCombatant, GameObject> _models = new();
        private BattleGridOverlay _gridOverlay;
        private ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController _camCtrl;
        private float _cellSize = 1f;

        public void Initialize(BattleSceneTemplate template, BattleGrid grid,
            List<BattleCombatant> combatants, Camera cam)
        {
            _template = template;
            _cellSize = 1f;

            // Instantiate room prefab (or empty container if no prefab assigned)
            if (template.RoomPrefab != null)
            {
                _roomInstance = Instantiate(template.RoomPrefab, Vector3.zero, Quaternion.identity);
                _roomInstance.name = "BattleRoom";
            }
            else
            {
                _roomInstance = new GameObject("BattleRoom");
                // Fallback floor plane
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.SetParent(_roomInstance.transform);
                float floorW = grid.Width * _cellSize / 10f;
                float floorH = grid.Height * _cellSize / 10f;
                floor.transform.localScale = new Vector3(floorW, 1f, floorH);
                floor.transform.position = new Vector3(
                    grid.Width * _cellSize / 2f, 0f, grid.Height * _cellSize / 2f);
                var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                floorMat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.35f));
                floor.GetComponent<Renderer>().material = floorMat;
            }

            // Setup lighting
            var lightGO = new GameObject("BattleLight");
            lightGO.transform.SetParent(_roomInstance.transform);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = template.LightIntensity;
            light.color = template.AmbientColor;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Setup camera
            _camCtrl = cam.GetComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            if (_camCtrl == null)
                _camCtrl = cam.gameObject.AddComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            // Battle-specific camera defaults
            var gridCenter = new GameObject("GridCenter");
            gridCenter.transform.position = GridToWorld(grid.Width / 2, grid.Height / 2);
            _camCtrl.FollowTarget = gridCenter.transform;
            _camCtrl.SetDistance(15f);
            _camCtrl.SnapToTarget();

            // Spawn combatant models
            foreach (var c in combatants)
                SpawnModel(c);

            // Create grid overlay (hidden by default)
            var overlayGO = new GameObject("GridOverlay");
            _gridOverlay = overlayGO.AddComponent<BattleGridOverlay>();
            _gridOverlay.Initialize(grid, _cellSize);
        }

        private void SpawnModel(BattleCombatant combatant)
        {
            // Try to load model by ModelId, fall back to capsule
            GameObject model = null;
            if (!string.IsNullOrEmpty(combatant.ModelId))
            {
                var prefab = Resources.Load<GameObject>($"Models/{combatant.ModelId}");
                if (prefab != null)
                    model = Instantiate(prefab);
            }

            if (model == null)
            {
                // Fallback: colored capsule
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
                var mr = model.GetComponent<Renderer>();
                Color color = combatant.IsPlayer
                    ? new Color(0.2f, 0.6f, 1f)
                    : new Color(0.9f, 0.2f, 0.2f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else
                    mat.color = color;
                mr.material = mat;
                var col = model.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }

            model.name = $"Model_{combatant.Name}";
            model.transform.position = GridToWorld(combatant.X, combatant.Y);
            _models[combatant] = model;
        }

        public void UpdateVisuals(List<BattleCombatant> combatants, BattleCombatant currentTurn)
        {
            foreach (var c in combatants)
            {
                if (!_models.TryGetValue(c, out var model)) continue;
                if (model == null) continue;

                // Lerp to grid position
                Vector3 targetPos = GridToWorld(c.X, c.Y);
                model.transform.position = Vector3.Lerp(
                    model.transform.position, targetPos, Time.deltaTime * 12f);

                // Death visual
                if (!c.IsAlive && model.activeSelf)
                {
                    // Tilt and fade
                    model.transform.rotation = Quaternion.Lerp(
                        model.transform.rotation,
                        Quaternion.Euler(90f, 0f, 0f),
                        Time.deltaTime * 4f);
                    var mr = model.GetComponentInChildren<Renderer>();
                    if (mr != null)
                    {
                        Color col = mr.material.color;
                        col.a = Mathf.Lerp(col.a, 0f, Time.deltaTime * 2f);
                        mr.material.color = col;
                        if (col.a < 0.05f) model.SetActive(false);
                    }
                }

                // Turn indicator: scale pulse on active combatant
                if (c == currentTurn && c.IsAlive)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
                    model.transform.localScale = Vector3.one * 0.6f * pulse;
                }
                else if (c.IsAlive)
                {
                    model.transform.localScale = Vector3.one * 0.6f;
                }

                // Face nearest enemy/player
                BattleCombatant faceTarget = null;
                float bestDist = float.MaxValue;
                foreach (var other in combatants)
                {
                    if (other == c || !other.IsAlive || other.IsPlayer == c.IsPlayer) continue;
                    float dist = Mathf.Abs(c.X - other.X) + Mathf.Abs(c.Y - other.Y);
                    if (dist < bestDist) { bestDist = dist; faceTarget = other; }
                }
                if (faceTarget != null)
                {
                    Vector3 dir = GridToWorld(faceTarget.X, faceTarget.Y) - model.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        model.transform.rotation = Quaternion.Lerp(
                            model.transform.rotation,
                            Quaternion.LookRotation(dir),
                            Time.deltaTime * 8f);
                }
            }
        }

        /// <summary>Show floating damage number at a combatant's position.</summary>
        public void ShowDamage(BattleCombatant target, int amount, bool isCrit)
        {
            if (!_models.TryGetValue(target, out var model) || model == null) return;
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

        public void ShowGrid(BattleGrid grid, BattleCombatant mover, List<BattleCombatant> combatants)
        {
            if (_gridOverlay != null)
                _gridOverlay.Show(grid, mover, combatants, _cellSize);
        }

        public void HideGrid()
        {
            if (_gridOverlay != null)
                _gridOverlay.Hide();
        }

        /// <summary>Convert grid coordinates to 3D world position.</summary>
        public Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(x * _cellSize + _cellSize * 0.5f, 0.1f, y * _cellSize + _cellSize * 0.5f);
        }

        /// <summary>Convert world position to grid coordinates.</summary>
        public (int x, int y) WorldToGrid(Vector3 pos)
        {
            int x = Mathf.FloorToInt(pos.x / _cellSize);
            int y = Mathf.FloorToInt(pos.z / _cellSize);
            return (x, y);
        }

        public void Cleanup()
        {
            if (_roomInstance != null) Destroy(_roomInstance);
            foreach (var kv in _models)
                if (kv.Value != null) Destroy(kv.Value);
            _models.Clear();
            if (_gridOverlay != null) Destroy(_gridOverlay.gameObject);
        }
    }

    /// <summary>Floating damage number that rises and fades over 1 second.</summary>
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
            // Billboard
            var cam = Camera.main;
            if (cam != null) transform.forward = cam.transform.forward;
            if (_timer >= 1f) Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 2: Compile check**

```bash
cd "C:/Dev/Forever engine"
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

Expected: will fail because `BattleGridOverlay` and `BattleCombatant.ModelId` don't exist yet. That's OK — Task 3 creates the overlay, Task 6 adds ModelId. For now, just verify no syntax errors by reading the compiler output.

- [ ] **Step 3: Commit (even if compile fails on missing types — they come in later tasks)**

```bash
git add Assets/Scripts/Demo/Battle/BattleRenderer3D.cs
git commit -m "feat: add BattleRenderer3D foundation with room loading and capsule tokens"
```

---

### Task 3: BattleGridOverlay

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleGridOverlay.cs`

On-demand grid mesh overlay projected on the floor. Shows reachable tiles (blue), threatened tiles (red), current tile (white). Hidden by default.

- [ ] **Step 1: Create the overlay class**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleGridOverlay : UnityEngine.MonoBehaviour
    {
        private Mesh _mesh;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Material _material;
        private int _gridWidth, _gridHeight;
        private float _cellSize;

        private static readonly Color COLOR_REACHABLE = new Color(0.2f, 0.5f, 1f, 0.25f);
        private static readonly Color COLOR_THREATENED = new Color(1f, 0.2f, 0.2f, 0.25f);
        private static readonly Color COLOR_CURRENT = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color COLOR_PATH = new Color(0.3f, 0.7f, 1f, 0.4f);

        public void Initialize(BattleGrid grid, float cellSize)
        {
            _gridWidth = grid.Width;
            _gridHeight = grid.Height;
            _cellSize = cellSize;

            _mf = gameObject.AddComponent<MeshFilter>();
            _mr = gameObject.AddComponent<MeshRenderer>();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _material = new Material(shader);
            _material.SetFloat("_Surface", 1f); // transparent
            _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_ZWrite", 0);
            _material.renderQueue = 3000;
            _mr.material = _material;

            gameObject.SetActive(false);
        }

        public void Show(BattleGrid grid, BattleCombatant mover, List<BattleCombatant> combatants, float cellSize)
        {
            _cellSize = cellSize;
            gameObject.SetActive(true);
            RebuildMesh(grid, mover, combatants);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RebuildMesh(BattleGrid grid, BattleCombatant mover, List<BattleCombatant> combatants)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var colors = new List<Color>();

            // Build set of occupied cells and enemy-adjacent cells
            var occupied = new HashSet<(int, int)>();
            var threatened = new HashSet<(int, int)>();
            foreach (var c in combatants)
            {
                if (!c.IsAlive) continue;
                occupied.Add((c.X, c.Y));
                if (!c.IsPlayer)
                {
                    // Mark cells adjacent to enemies as threatened
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (dx != 0 || dy != 0)
                                threatened.Add((c.X + dx, c.Y + dy));
                }
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!grid.IsWalkable(x, y)) continue;

                    Color cellColor;
                    if (x == mover.X && y == mover.Y)
                        cellColor = COLOR_CURRENT;
                    else if (threatened.Contains((x, y)))
                        cellColor = COLOR_THREATENED;
                    else
                    {
                        int dist = Mathf.Abs(x - mover.X) + Mathf.Abs(y - mover.Y);
                        if (dist <= mover.MovementRemaining && !occupied.Contains((x, y)))
                            cellColor = COLOR_REACHABLE;
                        else
                            continue; // Don't show unreachable, non-threatened tiles
                    }

                    AddQuad(verts, tris, colors, x, y, cellColor);
                }
            }

            if (_mesh == null) _mesh = new Mesh();
            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.SetColors(colors);
            _mesh.RecalculateNormals();
            _mf.mesh = _mesh;
        }

        private void AddQuad(List<Vector3> verts, List<int> tris, List<Color> colors,
            int x, int y, Color color)
        {
            float padding = 0.05f;
            float y3d = 0.01f; // Just above floor to prevent Z-fighting
            int idx = verts.Count;

            float x0 = x * _cellSize + padding;
            float x1 = (x + 1) * _cellSize - padding;
            float z0 = y * _cellSize + padding;
            float z1 = (y + 1) * _cellSize - padding;

            verts.Add(new Vector3(x0, y3d, z0));
            verts.Add(new Vector3(x0, y3d, z1));
            verts.Add(new Vector3(x1, y3d, z1));
            verts.Add(new Vector3(x1, y3d, z0));

            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);

            colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
        }
    }
}
```

- [ ] **Step 2: Compile check**

```bash
cd "C:/Dev/Forever engine"
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleGridOverlay.cs
git commit -m "feat: add BattleGridOverlay with reachable/threatened/current tile colors"
```

---

### Task 4: BattleInputController

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleInputController.cs`

Mouse raycast for tile selection. Click tile = move, click enemy = attack. Hover shows inspect tooltip.

- [ ] **Step 1: Create the input controller**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleInputController : UnityEngine.MonoBehaviour
    {
        private BattleRenderer3D _renderer;
        private BattleManager _battle;
        private Camera _cam;
        private (int x, int y) _hoveredCell = (-1, -1);

        public (int x, int y) HoveredCell => _hoveredCell;
        public BattleCombatant HoveredEnemy { get; private set; }

        public void Initialize(BattleRenderer3D renderer, BattleManager battle, Camera cam)
        {
            _renderer = renderer;
            _battle = battle;
            _cam = cam;
        }

        private void Update()
        {
            if (_battle == null || _battle.BattleOver) return;
            if (_battle.CurrentTurn == null || !_battle.CurrentTurn.IsPlayer) return;

            UpdateHover();
            HandleClick();
        }

        private void UpdateHover()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            // Intersect with Y=0 plane
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) { _hoveredCell = (-1, -1); HoveredEnemy = null; return; }

            Vector3 hitPoint = ray.origin + ray.direction * t;
            _hoveredCell = _renderer.WorldToGrid(hitPoint);

            // Check if hovering an enemy
            HoveredEnemy = null;
            foreach (var c in _battle.Combatants)
            {
                if (!c.IsAlive || c.IsPlayer) continue;
                if (c.X == _hoveredCell.x && c.Y == _hoveredCell.y)
                {
                    HoveredEnemy = c;
                    break;
                }
            }
        }

        private void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_hoveredCell.x < 0) return;

            var current = _battle.CurrentTurn;
            if (current == null || !current.IsPlayer) return;

            // Click on enemy = attack
            if (HoveredEnemy != null && current.HasAction)
            {
                int dist = Mathf.Abs(current.X - HoveredEnemy.X) + Mathf.Abs(current.Y - HoveredEnemy.Y);
                if (dist <= 1)
                {
                    // Adjacent — trigger attack via BattleManager's public method
                    _battle.PlayerAttack(HoveredEnemy);
                    return;
                }
            }

            // Click on walkable tile = move
            if (_battle.Grid.IsWalkable(_hoveredCell.x, _hoveredCell.y))
            {
                int dist = Mathf.Abs(current.X - _hoveredCell.x) + Mathf.Abs(current.Y - _hoveredCell.y);
                if (dist <= current.MovementRemaining)
                {
                    // Check not occupied
                    bool occupied = false;
                    foreach (var c in _battle.Combatants)
                    {
                        if (c.IsAlive && c.X == _hoveredCell.x && c.Y == _hoveredCell.y)
                        { occupied = true; break; }
                    }
                    if (!occupied)
                    {
                        _battle.PlayerMoveTo(_hoveredCell.x, _hoveredCell.y);
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: Compile check**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleInputController.cs
git commit -m "feat: add BattleInputController with mouse raycast click-to-move/attack"
```

---

### Task 5: Wire BattleManager for 3D rendering

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs`
- Modify: `Assets/Scripts/Demo/GameManager.cs`

Add `PlayerMoveTo` and `PlayerAttack` public methods to BattleManager (used by BattleInputController). Switch renderer creation to use BattleRenderer3D when in 3D mode.

- [ ] **Step 1: Add ModelId to BattleCombatant**

In `Assets/Scripts/Demo/Battle/BattleCombatant.cs`, add after line 38 (`public int TempHP;`):

```csharp
        public string ModelId;
```

- [ ] **Step 2: Add ModelId to EnemyDef**

In `Assets/Scripts/Demo/Encounters/EncounterData.cs`, add after line 17 (after the `Immunities` field):

```csharp
        public string ModelId;
```

- [ ] **Step 3: Wire ModelId in BattleCombatant.FromEnemy**

In `BattleCombatant.cs`, in the `FromEnemy` method, add after line 102 (`AttackDamageType = def.AttackDamageType`):

```csharp
                ModelId = def.ModelId
```

(Add comma to the line above it.)

- [ ] **Step 4: Add Biome field to EncounterData**

In `EncounterData.cs`, add after line 27 (`public int XPReward;`):

```csharp
        public string Biome;
```

- [ ] **Step 5: Set Biome in GenerateRandom**

In `EncounterData.cs`, in `GenerateRandom`, after line 43 (`var enc = new EncounterData { Id = id, GridWidth = 8, GridHeight = 8 };`), add:

```csharp
            if (id.Contains("Forest")) enc.Biome = "forest";
            else if (id.Contains("Road")) enc.Biome = "dungeon";
            else enc.Biome = "plains";
```

- [ ] **Step 6: Add PlayerMoveTo and PlayerAttack to BattleManager**

In `BattleManager.cs`, add these public methods (used by BattleInputController for mouse input):

```csharp
        /// <summary>Move player to a specific tile (called by mouse input).</summary>
        public void PlayerMoveTo(int x, int y)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || BattleOver) return;
            int dist = Mathf.Abs(CurrentTurn.X - x) + Mathf.Abs(CurrentTurn.Y - y);
            if (dist > CurrentTurn.MovementRemaining) return;
            if (!Grid.IsWalkable(x, y)) return;
            CurrentTurn.X = x;
            CurrentTurn.Y = y;
            CurrentTurn.MovementRemaining -= dist;
        }

        /// <summary>Attack a specific enemy (called by mouse input).</summary>
        public void PlayerAttack(BattleCombatant target)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || BattleOver) return;
            if (!CurrentTurn.HasAction) return;
            int dist = Mathf.Abs(CurrentTurn.X - target.X) + Mathf.Abs(CurrentTurn.Y - target.Y);
            if (dist > 1) return;
            ResolveAttack(CurrentTurn, target);
            CurrentTurn.HasAction = false;
        }
```

- [ ] **Step 7: Add 3D renderer creation in BattleManager.Start()**

In `BattleManager.cs`, replace lines 136-138:

```csharp
            // Create visual renderer
            var rendererGO = new GameObject("BattleRenderer");
            var renderer = rendererGO.AddComponent<BattleRenderer>();
            renderer.Initialize(Grid, Combatants, Camera.main);
```

With:

```csharp
            // Create visual renderer — 3D if a template is available, else 2D fallback
            var template = FindBattleTemplate(_encounterData);
            if (template != null)
            {
                var rendererGO = new GameObject("BattleRenderer3D");
                var renderer3D = rendererGO.AddComponent<BattleRenderer3D>();
                renderer3D.Initialize(template, Grid, Combatants, Camera.main);
                _renderer3D = renderer3D;

                // Setup input controller
                var inputGO = new GameObject("BattleInput");
                var input = inputGO.AddComponent<BattleInputController>();
                input.Initialize(renderer3D, this, Camera.main);
            }
            else
            {
                var rendererGO = new GameObject("BattleRenderer");
                var renderer = rendererGO.AddComponent<BattleRenderer>();
                renderer.Initialize(Grid, Combatants, Camera.main);
            }
```

And add the field and helper method:

```csharp
        private BattleRenderer3D _renderer3D;

        private BattleSceneTemplate FindBattleTemplate(Encounters.EncounterData encounter)
        {
            string biome = encounter.Biome ?? "dungeon";
            var templates = Resources.LoadAll<BattleSceneTemplate>($"BattleTemplates/{biome}");
            if (templates == null || templates.Length == 0)
                templates = Resources.LoadAll<BattleSceneTemplate>("BattleTemplates");
            if (templates == null || templates.Length == 0) return null;

            // Pick a random template from the pool
            var rng = new System.Random((int)_rngSeed);
            return templates[rng.Next(templates.Length)];
        }
```

- [ ] **Step 8: Update BattleManager.Update() to handle 3D renderer**

In `BattleManager.Update()`, replace lines 39-42:

```csharp
            if (_renderer == null) _renderer = FindAnyObjectByType<BattleRenderer>();
            if (_renderer != null) _renderer.UpdateVisuals(Combatants, CurrentTurn);
```

With:

```csharp
            if (_renderer3D != null)
                _renderer3D.UpdateVisuals(Combatants, CurrentTurn);
            else
            {
                if (_renderer == null) _renderer = FindAnyObjectByType<BattleRenderer>();
                if (_renderer != null) _renderer.UpdateVisuals(Combatants, CurrentTurn);
            }
```

- [ ] **Step 9: Compile check**

```bash
cd "C:/Dev/Forever engine"
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

- [ ] **Step 10: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleManager.cs Assets/Scripts/Demo/Battle/BattleCombatant.cs Assets/Scripts/Demo/Encounters/EncounterData.cs
git commit -m "feat: wire BattleManager for 3D rendering with template selection and mouse input"
```

---

### Task 6: Import GLB Models

**Files:**
- Copy GLB files to `Assets/Resources/Models/NPCs/` and `Assets/Resources/Models/Monsters/`

Note: models go under `Resources/` so `Resources.Load` works at runtime.

- [ ] **Step 1: Create model directories and copy GLBs**

```bash
mkdir -p "C:/Dev/Forever engine/Assets/Resources/Models/NPCs"
mkdir -p "C:/Dev/Forever engine/Assets/Resources/Models/Monsters"

# Copy hero models
cp "C:/Pictures/Assets/NPCs/"*.glb "C:/Dev/Forever engine/Assets/Resources/Models/NPCs/"

# Copy monster models
cp "C:/Pictures/Assets/NPCs/Monsters/"*.glb "C:/Dev/Forever engine/Assets/Resources/Models/Monsters/"
```

- [ ] **Step 2: Add ModelId mappings to EnemyDef defaults in EncounterData**

In `EncounterData.cs`, update `MakeCREnemyDef` to accept an optional modelId parameter:

Add `string modelId = null` to the method signature (after `DamageType atkDmgType`):

```csharp
        private static EnemyDef MakeCREnemyDef(string name, int xp, string behavior, string biome,
            DamageType atkDmgType, string modelId = null)
```

And add `ModelId = modelId` to each return branch in the switch. For example the first branch:

```csharp
                <= 25  => new EnemyDef { Name = name, HP = 10, AC = 11, Str = 12, Dex = 14, Spd = 6, AtkDice = "1d6+1",  Behavior = behavior, CR = 0, AttackDamageType = atkDmgType, ModelId = modelId },
```

(Same pattern for all 6 branches.)

- [ ] **Step 3: Wire model IDs for existing encounters**

Update encounter generation calls to include model IDs. For example in the Forest section:

```csharp
enc.Enemies.Add(MakeCREnemyDef("Wolf", 25, "chase", "Forest", DamageType.Piercing, "Monsters/Giant Rat"));
```

And for skeletons:

```csharp
MakeCREnemyDef("Skeleton", 25, "guard", "Ruins", DamageType.Slashing, "Monsters/skeleton Fighter")
```

Map as many as reasonable — unmapped enemies fall back to colored capsules.

- [ ] **Step 4: Commit**

```bash
git add Assets/Resources/Models/
git add Assets/Scripts/Demo/Encounters/EncounterData.cs
git commit -m "feat: import GLB character models and wire ModelId mappings"
```

---

### Task 7: BattleVariation

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleVariation.cs`

Runtime randomizer that scatters props, varies lighting, and rotates the room.

- [ ] **Step 1: Create the variation class**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public static class BattleVariation
    {
        /// <summary>
        /// Apply runtime variation to a battle room. Modifies the grid walkability
        /// to reflect placed obstacles. Skipped for boss arenas.
        /// </summary>
        public static void Apply(BattleSceneTemplate template, GameObject room,
            BattleGrid grid, int seed)
        {
            if (template.IsBossArena) return;

            var rng = new System.Random(seed);

            // Room rotation (0, 90, 180, 270)
            int rotIndex = rng.Next(4);
            room.transform.rotation = Quaternion.Euler(0f, rotIndex * 90f, 0f);

            // Lighting variation
            var lights = room.GetComponentsInChildren<Light>();
            float hueShift = (float)(rng.NextDouble() * 0.1 - 0.05);
            float intensityMult = 0.8f + (float)(rng.NextDouble() * 0.4);
            foreach (var light in lights)
            {
                Color.RGBToHSV(light.color, out float h, out float s, out float v);
                light.color = Color.HSVToRGB(Mathf.Repeat(h + hueShift, 1f), s, v);
                light.intensity *= intensityMult;
            }

            // Scatter obstacle props within walkable area
            if (template.ObstacleProps != null && template.ObstacleProps.Length > 0)
            {
                int propCount = 2 + rng.Next(5); // 2-6 props
                int attempts = 0;
                int placed = 0;

                while (placed < propCount && attempts < 50)
                {
                    attempts++;
                    int x = 2 + rng.Next(grid.Width - 4);
                    int y = 2 + rng.Next(grid.Height - 4);

                    if (!grid.IsWalkable(x, y)) continue;

                    // Don't block spawn zones
                    bool inSpawn = false;
                    foreach (var sp in template.PlayerSpawnZone)
                        if (sp.x == x && sp.y == y) { inSpawn = true; break; }
                    if (!inSpawn)
                        foreach (var sp in template.EnemySpawnZone)
                            if (sp.x == x && sp.y == y) { inSpawn = true; break; }
                    if (inSpawn) continue;

                    // Place prop
                    var prefab = template.ObstacleProps[rng.Next(template.ObstacleProps.Length)];
                    var prop = Object.Instantiate(prefab, room.transform);
                    prop.transform.position = new Vector3(x + 0.5f, 0f, y + 0.5f);
                    prop.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);

                    // Mark cell as unwalkable
                    grid.Walkable[y * grid.Width + x] = false;
                    placed++;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Wire variation into BattleRenderer3D.Initialize**

In `BattleRenderer3D.cs`, after instantiating the room prefab and before spawning combatants, add:

```csharp
            // Apply variation (props, lighting, rotation) — boss arenas skip this
            BattleVariation.Apply(template, _roomInstance, grid, (int)System.DateTime.Now.Ticks);
```

- [ ] **Step 3: Compile check**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleVariation.cs Assets/Scripts/Demo/Battle/BattleRenderer3D.cs
git commit -m "feat: add BattleVariation for runtime prop/lighting/rotation randomization"
```

---

### Task 8: BattleUI (UI Toolkit)

**Files:**
- Create: `Assets/Scripts/Demo/Battle/BattleUI.cs`

Player HUD (bottom-left), action bar (bottom-center), enemy inspect tooltip. No enemy HP exposed.

- [ ] **Step 1: Create the UI class**

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleUI : UnityEngine.MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        // HUD elements
        private Label _playerName;
        private VisualElement _hpBar;
        private Label _hpText;
        private Label _conditionsLabel;
        private Label _actionLabel;

        // Action bar
        private Button _btnMove, _btnAttack, _btnSpell, _btnPotion, _btnEndTurn;

        // Enemy inspect tooltip
        private VisualElement _tooltip;
        private Label _tooltipName;
        private Label _tooltipConditions;

        public void Initialize(BattleCombatant player)
        {
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = Resources.Load<PanelSettings>("UI/BattlePanelSettings");

            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _doc.rootVisualElement.Add(_root);

            BuildPlayerHUD(player);
            BuildActionBar();
            BuildTooltip();
        }

        private void BuildPlayerHUD(BattleCombatant player)
        {
            var hud = new VisualElement();
            hud.style.position = Position.Absolute;
            hud.style.left = 16; hud.style.bottom = 16;
            hud.style.width = 220; hud.style.height = 100;
            hud.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            hud.style.borderTopLeftRadius = hud.style.borderTopRightRadius =
                hud.style.borderBottomLeftRadius = hud.style.borderBottomRightRadius = 8;
            hud.style.paddingLeft = hud.style.paddingRight =
                hud.style.paddingTop = hud.style.paddingBottom = 8;

            _playerName = new Label(player.Name);
            _playerName.style.fontSize = 16;
            _playerName.style.color = new Color(0.9f, 0.85f, 0.6f);
            hud.Add(_playerName);

            // HP bar container
            var hpContainer = new VisualElement();
            hpContainer.style.height = 12;
            hpContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            hpContainer.style.borderTopLeftRadius = hpContainer.style.borderTopRightRadius =
                hpContainer.style.borderBottomLeftRadius = hpContainer.style.borderBottomRightRadius = 4;
            hpContainer.style.marginTop = 4;

            _hpBar = new VisualElement();
            _hpBar.style.height = new Length(100, LengthUnit.Percent);
            _hpBar.style.backgroundColor = Color.green;
            _hpBar.style.borderTopLeftRadius = _hpBar.style.borderBottomLeftRadius = 4;
            hpContainer.Add(_hpBar);
            hud.Add(hpContainer);

            _hpText = new Label($"{player.HP}/{player.MaxHP}");
            _hpText.style.fontSize = 11;
            _hpText.style.color = Color.white;
            hud.Add(_hpText);

            _conditionsLabel = new Label("");
            _conditionsLabel.style.fontSize = 11;
            _conditionsLabel.style.color = new Color(1f, 0.7f, 0.3f);
            hud.Add(_conditionsLabel);

            _actionLabel = new Label("");
            _actionLabel.style.fontSize = 11;
            _actionLabel.style.color = new Color(0.7f, 0.9f, 1f);
            hud.Add(_actionLabel);

            _root.Add(hud);
        }

        private void BuildActionBar()
        {
            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.bottom = 16;
            bar.style.left = new Length(50, LengthUnit.Percent);
            bar.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius =
                bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = 8;
            bar.style.paddingLeft = bar.style.paddingRight =
                bar.style.paddingTop = bar.style.paddingBottom = 6;

            _btnMove = MakeButton("Move [WASD]", () => { });
            _btnAttack = MakeButton("Attack [F]", () => BattleManager.Instance?.AttackNearestEnemy());
            _btnSpell = MakeButton("Spell [Q]", () => BattleManager.Instance?.ToggleSpellMenu());
            _btnPotion = MakeButton("Potion [H]", () => BattleManager.Instance?.UseHealthPotion());
            _btnEndTurn = MakeButton("End Turn [Space]", () => BattleManager.Instance?.PlayerEndTurn());

            bar.Add(_btnMove); bar.Add(_btnAttack); bar.Add(_btnSpell);
            bar.Add(_btnPotion); bar.Add(_btnEndTurn);
            _root.Add(bar);
        }

        private Button MakeButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick);
            btn.text = text;
            btn.style.fontSize = 12;
            btn.style.marginLeft = btn.style.marginRight = 4;
            btn.style.paddingLeft = btn.style.paddingRight = 10;
            btn.style.paddingTop = btn.style.paddingBottom = 6;
            btn.style.backgroundColor = new Color(0.25f, 0.22f, 0.2f);
            btn.style.color = new Color(0.9f, 0.85f, 0.7f);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
            return btn;
        }

        private void BuildTooltip()
        {
            _tooltip = new VisualElement();
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.width = 180;
            _tooltip.style.backgroundColor = new Color(0.12f, 0.1f, 0.1f, 0.9f);
            _tooltip.style.borderTopLeftRadius = _tooltip.style.borderTopRightRadius =
                _tooltip.style.borderBottomLeftRadius = _tooltip.style.borderBottomRightRadius = 6;
            _tooltip.style.paddingLeft = _tooltip.style.paddingRight =
                _tooltip.style.paddingTop = _tooltip.style.paddingBottom = 8;
            _tooltip.style.display = DisplayStyle.None;

            _tooltipName = new Label("");
            _tooltipName.style.fontSize = 14;
            _tooltipName.style.color = new Color(1f, 0.85f, 0.5f);
            _tooltip.Add(_tooltipName);

            _tooltipConditions = new Label("");
            _tooltipConditions.style.fontSize = 11;
            _tooltipConditions.style.color = new Color(1f, 0.6f, 0.3f);
            _tooltip.Add(_tooltipConditions);

            _root.Add(_tooltip);
        }

        public void UpdateHUD(BattleCombatant player)
        {
            if (player == null) return;

            float hpPct = (float)player.HP / player.MaxHP;
            _hpBar.style.width = new Length(hpPct * 100f, LengthUnit.Percent);
            _hpBar.style.backgroundColor = Color.Lerp(Color.red, Color.green, hpPct);
            _hpText.text = $"{player.HP}/{player.MaxHP}";

            string conds = "";
            if (player.Conditions != null)
            {
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Poisoned)) conds += "Poisoned ";
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Stunned)) conds += "Stunned ";
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Paralyzed)) conds += "Paralyzed ";
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Frightened)) conds += "Frightened ";
            }
            _conditionsLabel.text = conds;

            string action = player.HasAction ? "Action ready" : "Action used";
            action += $" | Move: {player.MovementRemaining}";
            _actionLabel.text = action;
        }

        public void ShowTooltip(BattleCombatant enemy, Vector2 screenPos)
        {
            if (enemy == null)
            {
                _tooltip.style.display = DisplayStyle.None;
                return;
            }

            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.style.left = screenPos.x + 20;
            _tooltip.style.top = Screen.height - screenPos.y;
            _tooltipName.text = enemy.Name;

            string conds = "";
            if (enemy.Conditions != null)
            {
                if (enemy.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Poisoned)) conds += "Poisoned ";
                if (enemy.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Stunned)) conds += "Stunned ";
            }
            _tooltipConditions.text = conds.Length > 0 ? conds : "No visible conditions";
        }

        public void HideTooltip()
        {
            _tooltip.style.display = DisplayStyle.None;
        }
    }
}
```

- [ ] **Step 2: Wire BattleUI into BattleRenderer3D**

In `BattleRenderer3D.cs`, add a field and creation in `Initialize`:

```csharp
        private BattleUI _ui;
```

At the end of `Initialize`, add:

```csharp
            // Create battle UI
            var uiGO = new GameObject("BattleUI");
            _ui = uiGO.AddComponent<BattleUI>();
            var player = combatants.Find(c => c.IsPlayer);
            if (player != null) _ui.Initialize(player);
```

In `UpdateVisuals`, add at the end:

```csharp
            // Update HUD
            if (_ui != null)
            {
                var player = combatants.Find(c => c.IsPlayer);
                _ui.UpdateHUD(player);
            }
```

In `Cleanup`, add:

```csharp
            if (_ui != null) Destroy(_ui.gameObject);
```

- [ ] **Step 3: Compile check**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleUI.cs Assets/Scripts/Demo/Battle/BattleRenderer3D.cs
git commit -m "feat: add BattleUI with player HUD, action bar, and enemy inspect tooltip"
```

---

### Task 9: Make BattleManager methods public for UI/input

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs`

The BattleUI action bar buttons and BattleInputController need to call `AttackNearestEnemy`, `ToggleSpellMenu`, `UseHealthPotion`, and `PlayerEndTurn`. These are currently private. Make them public (or internal).

- [ ] **Step 1: Change method visibility**

Find each of these methods in BattleManager.cs and change `private` to `public`:

- `AttackNearestEnemy()` 
- `ToggleSpellMenu()`
- `UseHealthPotion()`
- `PlayerEndTurn()`

- [ ] **Step 2: Compile check**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleManager.cs
git commit -m "feat: expose BattleManager combat methods for UI and input controller"
```

---

### Task 10: Create initial BattleSceneTemplate assets

**Files:**
- Create: `Assets/Resources/BattleTemplates/dungeon/` (at least 1 template for testing)

This task creates a minimal template ScriptableObject via an editor script so we can test the full pipeline.

- [ ] **Step 1: Create an editor script to generate a test template**

Create `Assets/Scripts/Editor/CreateTestBattleTemplate.cs`:

```csharp
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ForeverEngine.Editor
{
    public static class CreateTestBattleTemplate
    {
        [MenuItem("Forever/Create Test Battle Template")]
        public static void Create()
        {
            var template = ScriptableObject.CreateInstance<Demo.Battle.BattleSceneTemplate>();
            template.Biome = "dungeon";
            template.GridWidth = 8;
            template.GridHeight = 8;
            template.PlayerSpawnZone = new[] {
                new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1)
            };
            template.EnemySpawnZone = new[] {
                new Vector2Int(5, 5), new Vector2Int(5, 6), new Vector2Int(6, 5), new Vector2Int(6, 6)
            };
            template.BossSpawnPoints = new[] { new Vector2Int(4, 4) };

            string dir = "Assets/Resources/BattleTemplates/dungeon";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Resources/BattleTemplates", "dungeon");
            }
            AssetDatabase.CreateAsset(template, $"{dir}/TestDungeon_01.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[CreateTestBattleTemplate] Created TestDungeon_01 at " + dir);
        }
    }
}
#endif
```

- [ ] **Step 2: Compile check**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Editor/CreateTestBattleTemplate.cs
git commit -m "feat: add editor script to generate test battle template"
```

---

### Task 11: Final integration and manual verification

- [ ] **Step 1: Full compile check**

```bash
cd "C:/Dev/Forever engine"
CSC="C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll"
RSP=$(find "Library/Bee" -name "ForeverEngine.rsp" 2>/dev/null | head -1)
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

Fix any compilation errors.

- [ ] **Step 2: Manual verification in Unity**

1. Open Unity, let it recompile
2. Run `Forever > Create Test Battle Template` from the menu bar
3. Play the game, walk to an encounter on the overworld
4. Verify: 3D room loads (or fallback floor), capsule tokens appear, camera works
5. Verify: WASD moves on the grid, F attacks, Space ends turn
6. Verify: Mouse hover shows grid overlay, click-to-move works, click enemy to attack
7. Verify: Player HUD shows HP, action bar buttons work
8. Verify: Damage numbers float up on hits
9. Verify: Death causes tilt + fade
10. Victory returns to overworld

- [ ] **Step 3: Commit all remaining changes**

```bash
git add -A
git commit -m "feat: 3D battle scenes — complete initial integration"
```
