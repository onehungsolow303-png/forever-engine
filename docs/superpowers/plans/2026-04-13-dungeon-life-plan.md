# Dungeon Life Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add NPC placement (friendly + ambient enemies with patrol) and a hybrid minimap to dungeon exploration.

**Architecture:** Four new files — DungeonNPC (MonoBehaviour + enum), DungeonNPCConfig (ScriptableObject), DungeonNPCSpawner (static placement logic), DungeonMinimap (IMGUI overlay). Three modified files — DungeonSceneSetup (call spawner), DungeonExplorer (create minimap + Tab toggle), EncounterZone (destroy ambient enemies on combat).

**Tech Stack:** Unity 6 C#, IMGUI for minimap, DA Snap builder RoomGraph, ModelRegistry for NPC models.

**Spec:** `docs/superpowers/specs/2026-04-13-dungeon-life-design.md`

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `Assets/Scripts/Demo/Dungeon/DungeonNPC.cs` | Create | NPC runtime behavior + DungeonNPCRole enum |
| `Assets/Scripts/Demo/Dungeon/DungeonNPCConfig.cs` | Create | ScriptableObject defining spawn rules |
| `Assets/Scripts/Demo/Dungeon/DungeonNPCSpawner.cs` | Create | Static placement logic |
| `Assets/Scripts/Demo/Dungeon/DungeonMinimap.cs` | Create | IMGUI minimap rendering (corner + full overlay) |
| `Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs` | Modify | Call NPC spawner after DA build |
| `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs` | Modify | Create minimap, Tab toggle, movement suppression |
| `Assets/Scripts/Demo/Dungeon/EncounterZone.cs` | Modify | Destroy ambient enemies when encounter fires |

---

### Task 1: DungeonNPC MonoBehaviour + DungeonNPCRole Enum

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonNPC.cs`

- [ ] **Step 1: Create DungeonNPC.cs with enum and full MonoBehaviour**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    public enum DungeonNPCRole { Merchant, Prisoner, QuestGiver, AmbientEnemy }

    public class DungeonNPC : UnityEngine.MonoBehaviour
    {
        public DungeonNPCRole Role;
        public string NPCName;
        public int RoomIndex;
        public float InteractionRadius = 2.0f;
        public bool HasInteracted;

        // Patrol state (ambient enemies only)
        public Vector3 WaypointA;
        public Vector3 WaypointB;
        public float PatrolSpeed = 1.5f;
        private int _currentTarget; // 0 = A, 1 = B

        // Interaction prompt
        private GameObject _promptGO;
        private TextMesh _promptText;
        private bool _playerInRange;

        private void Update()
        {
            if (Role == DungeonNPCRole.AmbientEnemy)
                UpdatePatrol();
            else
                UpdateInteraction();
        }

        private void UpdatePatrol()
        {
            Vector3 target = _currentTarget == 0 ? WaypointA : WaypointB;
            transform.position = Vector3.MoveTowards(
                transform.position, target, PatrolSpeed * Time.deltaTime);

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (Vector3.Distance(transform.position, target) < 0.1f)
                _currentTarget = _currentTarget == 0 ? 1 : 0;
        }

        private void UpdateInteraction()
        {
            if (HasInteracted) return;

            var explorer = DungeonExplorer.Instance;
            if (explorer == null) return;

            // Access player transform via the public Instance
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null) return;

            float dist = Vector3.Distance(transform.position, playerGO.transform.position);
            bool inRange = dist <= InteractionRadius;

            if (inRange && !_playerInRange)
                ShowPrompt();
            else if (!inRange && _playerInRange)
                HidePrompt();

            _playerInRange = inRange;

            if (_playerInRange && Input.GetKeyDown(KeyCode.E))
                Interact();
        }

        private void ShowPrompt()
        {
            if (_promptGO != null) return;

            string text = Role switch
            {
                DungeonNPCRole.Merchant => "[E] Trade",
                DungeonNPCRole.Prisoner => "[E] Rescue",
                DungeonNPCRole.QuestGiver => "[E] Talk",
                _ => "[E]",
            };

            _promptGO = new GameObject("NPCPrompt");
            _promptGO.transform.SetParent(transform);
            _promptGO.transform.localPosition = Vector3.up * 2.2f;

            _promptText = _promptGO.AddComponent<TextMesh>();
            _promptText.text = text;
            _promptText.characterSize = 0.12f;
            _promptText.fontSize = 48;
            _promptText.anchor = TextAnchor.MiddleCenter;
            _promptText.alignment = TextAlignment.Center;
            _promptText.color = Color.white;
        }

        private void HidePrompt()
        {
            if (_promptGO != null)
            {
                Destroy(_promptGO);
                _promptGO = null;
                _promptText = null;
            }
        }

        private void Interact()
        {
            switch (Role)
            {
                case DungeonNPCRole.Merchant:
                    InteractMerchant();
                    break;
                case DungeonNPCRole.Prisoner:
                    InteractPrisoner();
                    break;
                case DungeonNPCRole.QuestGiver:
                    InteractQuestGiver();
                    break;
            }
        }

        private void InteractMerchant()
        {
            // Stub — full trade UI deferred to Batch D (loot system)
            Debug.Log($"[DungeonNPC] Merchant '{NPCName}' trade opened (stub).");
            HasInteracted = true;
            HidePrompt();
            // Re-allow interaction for merchants (they can be used multiple times)
            HasInteracted = false;
        }

        private void InteractPrisoner()
        {
            HasInteracted = true;
            HidePrompt();
            Debug.Log($"[DungeonNPC] Rescued prisoner '{NPCName}' (+50 XP).");

            // Grant XP
            var gm = GameManager.Instance;
            if (gm?.Player != null) gm.Player.AddXP(50);
            else if (gm?.Character != null) gm.Character.AddXP(50);

            // Scale to zero over 0.5s then destroy
            StartCoroutine(RescueAnimation());
        }

        private System.Collections.IEnumerator RescueAnimation()
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / 0.5f);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void InteractQuestGiver()
        {
            HasInteracted = true;
            HidePrompt();

            var explorer = DungeonExplorer.Instance;
            string locationId = explorer != null ? "dungeon" : "unknown";

            Demo.AI.DirectorEvents.SendDialogue(
                $"The player approaches {NPCName} in dungeon room {RoomIndex}.",
                NPCName.ToLowerInvariant().Replace(" ", "_"),
                response => Debug.Log($"[DungeonNPC] Director response for '{NPCName}': {response}"),
                locationId: $"{locationId}_room{RoomIndex}");

            // Allow re-interaction with quest givers
            HasInteracted = false;
        }

        private void LateUpdate()
        {
            // Billboard the prompt text toward camera
            if (_promptGO != null)
            {
                var cam = Camera.main;
                if (cam != null)
                    _promptGO.transform.forward = cam.transform.forward;
            }
        }

        private void OnDestroy()
        {
            HidePrompt();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonNPC.cs && git commit -m "feat: add DungeonNPC MonoBehaviour with role-based interaction and patrol"
```

---

### Task 2: DungeonNPCConfig ScriptableObject

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonNPCConfig.cs`

- [ ] **Step 1: Create DungeonNPCConfig.cs**

```csharp
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    [System.Serializable]
    public class FriendlyNPCRule
    {
        public DungeonNPCRole Role;
        public int[] TierFilter = { 1 };
        [Range(0f, 1f)] public float SpawnChance = 0.5f;
        public int MaxPerDungeon = 1;
        public string[] ModelKeys = { "Default_Player" };
    }

    [System.Serializable]
    public class AmbientEnemyRule
    {
        public int[] TierFilter = { 2, 3 };
        public string[] EnemyNames = { "Skeleton" };
        public Vector2Int CountRange = new(1, 2);
        public float PatrolRadius = 3f;
    }

    [CreateAssetMenu(fileName = "DungeonNPCConfig", menuName = "Forever/Dungeon NPC Config")]
    public class DungeonNPCConfig : ScriptableObject
    {
        [Header("Friendly NPCs")]
        public FriendlyNPCRule[] FriendlyRules =
        {
            new() { Role = DungeonNPCRole.Merchant, TierFilter = new[] { 1 }, SpawnChance = 0.7f, MaxPerDungeon = 1 },
            new() { Role = DungeonNPCRole.Prisoner, TierFilter = new[] { 1, 2 }, SpawnChance = 0.4f, MaxPerDungeon = 2 },
            new() { Role = DungeonNPCRole.QuestGiver, TierFilter = new[] { 3 }, SpawnChance = 0.3f, MaxPerDungeon = 1 },
        };

        [Header("Ambient Enemies")]
        public AmbientEnemyRule[] EnemyRules =
        {
            new() { TierFilter = new[] { 2, 3 }, EnemyNames = new[] { "Skeleton", "Lizard Folk" }, CountRange = new(1, 2), PatrolRadius = 3f },
        };

        [Header("Director Hub")]
        public bool DirectorOverrides = true;
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonNPCConfig.cs && git commit -m "feat: add DungeonNPCConfig ScriptableObject for NPC spawn rules"
```

---

### Task 3: DungeonNPCSpawner

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonNPCSpawner.cs`

- [ ] **Step 1: Create DungeonNPCSpawner.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    public static class DungeonNPCSpawner
    {
        public static List<DungeonNPC> SpawnNPCs(DADungeonBuilder builder, DungeonNPCConfig config, int seed)
        {
            var spawned = new List<DungeonNPC>();
            if (builder?.Rooms == null || config == null) return spawned;

            var rng = new System.Random(seed);
            var friendlyCounts = new Dictionary<DungeonNPCRole, int>();

            foreach (var room in builder.Rooms)
            {
                // Skip entrance, boss, and corridors
                if (room.IsEntrance || room.IsBoss || room.IsCorridor) continue;

                // Friendly NPCs
                foreach (var rule in config.FriendlyRules)
                {
                    if (!TierMatches(rule.TierFilter, room.Tier)) continue;
                    if (rng.NextDouble() > rule.SpawnChance) continue;

                    friendlyCounts.TryGetValue(rule.Role, out int count);
                    if (count >= rule.MaxPerDungeon) continue;

                    var npc = SpawnNPC(
                        rule.ModelKeys[rng.Next(rule.ModelKeys.Length)],
                        rule.Role,
                        GetRoleName(rule.Role, rng),
                        room, rng);

                    if (npc != null)
                    {
                        spawned.Add(npc);
                        friendlyCounts[rule.Role] = count + 1;
                    }
                }

                // Ambient enemies
                foreach (var rule in config.EnemyRules)
                {
                    if (!TierMatches(rule.TierFilter, room.Tier)) continue;

                    int count = rng.Next(rule.CountRange.x, rule.CountRange.y + 1);
                    for (int i = 0; i < count; i++)
                    {
                        string enemyName = rule.EnemyNames[rng.Next(rule.EnemyNames.Length)];
                        var npc = SpawnEnemy(enemyName, room, rule.PatrolRadius, rng);
                        if (npc != null) spawned.Add(npc);
                    }
                }
            }

            // Director Hub override hook (fire-and-forget)
            if (config.DirectorOverrides && spawned.Count > 0)
            {
                string manifest = BuildManifest(spawned);
                Demo.AI.DirectorEvents.Send($"dungeon_npcs_placed: {manifest}");
            }

            Debug.Log($"[DungeonNPCSpawner] Placed {spawned.Count} NPCs across dungeon.");
            return spawned;
        }

        private static DungeonNPC SpawnNPC(string modelKey, DungeonNPCRole role,
            string npcName, DADungeonBuilder.RoomInfo room, System.Random rng)
        {
            GameObject go = TryLoadModel(modelKey);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.localScale = Vector3.one * 0.8f;

                // Color by role
                var mr = go.GetComponent<Renderer>();
                Color color = role switch
                {
                    DungeonNPCRole.Merchant => new Color(0.2f, 0.7f, 0.3f),
                    DungeonNPCRole.Prisoner => new Color(0.8f, 0.7f, 0.2f),
                    DungeonNPCRole.QuestGiver => new Color(0.3f, 0.5f, 0.9f),
                    _ => Color.gray,
                };
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else mat.color = color;
                mr.material = mat;

                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }

            go.name = $"NPC_{npcName}";

            // Position: random point inside room bounds, on the floor
            Vector3 pos = RandomFloorPosition(room, rng);
            go.transform.position = pos;

            // Parent to room for culling
            if (room.RoomObject != null)
                go.transform.SetParent(room.RoomObject.transform);

            var npc = go.AddComponent<DungeonNPC>();
            npc.Role = role;
            npc.NPCName = npcName;
            npc.RoomIndex = room.Index;

            return npc;
        }

        private static DungeonNPC SpawnEnemy(string enemyName,
            DADungeonBuilder.RoomInfo room, float patrolRadius, System.Random rng)
        {
            // Resolve model from ModelRegistry
            var (path, scale) = Demo.Battle.ModelRegistry.Resolve(enemyName);
            GameObject go = null;
            if (!string.IsNullOrEmpty(path))
            {
                var prefab = Resources.Load<GameObject>(path);
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab);
                    go.transform.localScale = Vector3.one * scale;
                }
            }

            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
                var mr = go.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.7f, 0.2f, 0.2f));
                else mat.color = new Color(0.7f, 0.2f, 0.2f);
                mr.material = mat;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }

            go.name = $"AmbientEnemy_{enemyName}";

            Vector3 center = RandomFloorPosition(room, rng);
            go.transform.position = center;

            if (room.RoomObject != null)
                go.transform.SetParent(room.RoomObject.transform);

            var npc = go.AddComponent<DungeonNPC>();
            npc.Role = DungeonNPCRole.AmbientEnemy;
            npc.NPCName = enemyName;
            npc.RoomIndex = room.Index;

            // Set patrol waypoints: center ± patrolRadius on a random axis
            bool patrolOnX = rng.NextDouble() < 0.5;
            Vector3 offset = patrolOnX
                ? new Vector3(patrolRadius, 0, 0)
                : new Vector3(0, 0, patrolRadius);
            npc.WaypointA = center - offset;
            npc.WaypointB = center + offset;

            return npc;
        }

        private static Vector3 RandomFloorPosition(DADungeonBuilder.RoomInfo room, System.Random rng)
        {
            var bounds = room.WorldBounds;
            // Stay within inner 60% of the room to avoid walls
            float marginX = bounds.size.x * 0.2f;
            float marginZ = bounds.size.z * 0.2f;
            float x = bounds.min.x + marginX + (float)rng.NextDouble() * (bounds.size.x - 2 * marginX);
            float z = bounds.min.z + marginZ + (float)rng.NextDouble() * (bounds.size.z - 2 * marginZ);

            // Raycast down to find floor
            float y = bounds.min.y + 1f;
            if (Physics.Raycast(new Vector3(x, bounds.center.y, z), Vector3.down, out var hit, bounds.size.y))
                y = hit.point.y + 0.1f;

            return new Vector3(x, y, z);
        }

        private static GameObject TryLoadModel(string modelKey)
        {
            var (path, scale) = Demo.Battle.ModelRegistry.Resolve(modelKey);
            if (string.IsNullOrEmpty(path)) return null;
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static bool TierMatches(int[] filter, int tier)
        {
            foreach (int t in filter)
                if (t == tier) return true;
            return false;
        }

        private static string GetRoleName(DungeonNPCRole role, System.Random rng)
        {
            return role switch
            {
                DungeonNPCRole.Merchant => new[] { "Grimweld", "Nessa", "Old Korbin", "Mira" }[rng.Next(4)],
                DungeonNPCRole.Prisoner => new[] { "Captive Soldier", "Lost Explorer", "Chained Scholar" }[rng.Next(3)],
                DungeonNPCRole.QuestGiver => new[] { "Whispering Shade", "Dying Knight", "Cursed Hermit" }[rng.Next(3)],
                _ => "Unknown",
            };
        }

        private static string BuildManifest(List<DungeonNPC> npcs)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var npc in npcs)
                sb.Append($"{npc.Role}:{npc.NPCName}@room{npc.RoomIndex},");
            return sb.ToString().TrimEnd(',');
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonNPCSpawner.cs && git commit -m "feat: add DungeonNPCSpawner with tier-based placement and Director Hub hook"
```

---

### Task 4: Wire NPC Spawner into DungeonSceneSetup

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs:68-73`

- [ ] **Step 1: Add NPC spawner call between DA build and explorer init**

In `DungeonSceneSetup.cs`, between `dungeon.Build()` (line 68) and the explorer creation (line 71), add the NPC spawner call. Replace lines 68-73:

```csharp
        dungeon.Config.Seed = (uint)Mathf.Abs(seed);
        dungeon.Build();

        // Spawn NPCs in dungeon rooms
        var npcConfig = Resources.Load<DungeonNPCConfig>("DungeonNPCConfig");
        if (npcConfig != null)
            DungeonNPCSpawner.SpawnNPCs(builder, npcConfig, seed);

        // Create explorer and initialize with DA builder
        var explorerObj = new GameObject("DungeonExplorer");
```

This replaces:
```csharp
        dungeon.Config.Seed = (uint)Mathf.Abs(seed);
        dungeon.Build();

        // Create explorer and initialize with DA builder
        var explorerObj = new GameObject("DungeonExplorer");
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonSceneSetup.cs && git commit -m "feat: wire NPC spawner into dungeon scene setup"
```

---

### Task 5: EncounterZone Ambient Enemy Cleanup

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/EncounterZone.cs:22-28`

- [ ] **Step 1: Destroy ambient enemies when encounter fires**

In `EncounterZone.OnTriggerEnter`, before calling `explorer.EnterBattle`, add cleanup logic. Replace the method:

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

            var explorer = FindFirstObjectByType<DungeonExplorer>();
            if (explorer != null)
                explorer.EnterBattle(EncounterId, ZoneIndex, IsBoss);
        }
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/EncounterZone.cs && git commit -m "feat: destroy ambient enemies in room when encounter fires"
```

---

### Task 6: DungeonMinimap

**Files:**
- Create: `Assets/Scripts/Demo/Dungeon/DungeonMinimap.cs`

- [ ] **Step 1: Create DungeonMinimap.cs with full IMGUI rendering**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    public class DungeonMinimap : UnityEngine.MonoBehaviour
    {
        public bool IsFullOpen { get; private set; }

        private DADungeonBuilder _builder;
        private Transform _playerTransform;

        // Coordinate mapping
        private Vector2 _worldMin;
        private Vector2 _worldMax;
        private bool _initialized;

        // Cached NPC positions (refreshed on room change)
        private int _lastKnownRoom = -1;
        private readonly List<(Vector3 pos, DungeonNPCRole role)> _npcMarkers = new();

        // IMGUI textures
        private Texture2D _solidTex;

        // Tier colors
        private static readonly Color Tier1Color = new(0.2f, 0.6f, 0.3f, 0.7f);
        private static readonly Color Tier2Color = new(0.7f, 0.65f, 0.2f, 0.7f);
        private static readonly Color Tier3Color = new(0.7f, 0.25f, 0.2f, 0.7f);
        private static readonly Color CorridorColor = new(0.4f, 0.4f, 0.4f, 0.5f);
        private static readonly Color BossOutline = new(0.9f, 0.2f, 0.2f, 0.9f);
        private static readonly Color EntranceOutline = new(0.2f, 0.8f, 0.3f, 0.9f);
        private static readonly Color UnexploredColor = new(0.3f, 0.3f, 0.3f, 0.4f);
        private static readonly Color EdgeColor = new(0.5f, 0.5f, 0.5f, 0.4f);
        private static readonly Color PlayerColor = new(1f, 0.9f, 0f, 1f);
        private static readonly Color FriendlyColor = new(0.3f, 0.6f, 1f, 0.9f);
        private static readonly Color EnemyColor = new(0.9f, 0.2f, 0.2f, 0.9f);

        public void Initialize(DADungeonBuilder builder, Transform playerTransform)
        {
            _builder = builder;
            _playerTransform = playerTransform;

            if (builder?.Rooms == null || builder.Rooms.Length == 0) return;

            // Compute world AABB from room centers
            _worldMin = new Vector2(float.MaxValue, float.MaxValue);
            _worldMax = new Vector2(float.MinValue, float.MinValue);

            foreach (var room in builder.Rooms)
            {
                var b = room.WorldBounds;
                _worldMin.x = Mathf.Min(_worldMin.x, b.min.x);
                _worldMin.y = Mathf.Min(_worldMin.y, b.min.z);
                _worldMax.x = Mathf.Max(_worldMax.x, b.max.x);
                _worldMax.y = Mathf.Max(_worldMax.y, b.max.z);
            }

            // Add margin
            float margin = 5f;
            _worldMin -= Vector2.one * margin;
            _worldMax += Vector2.one * margin;

            // Create solid texture for drawing
            _solidTex = new Texture2D(1, 1);
            _solidTex.SetPixel(0, 0, Color.white);
            _solidTex.Apply();

            _initialized = true;
        }

        private void OnGUI()
        {
            if (!_initialized || _builder?.Rooms == null) return;

            // Suppress when dialogue is open
            var dialoguePanel = Demo.UI.DialoguePanel.Instance;
            if (dialoguePanel != null && dialoguePanel.IsOpen) return;

            // Refresh NPC markers when room changes
            int currentRoom = _builder.GetRoomAtPosition(_playerTransform.position);
            if (currentRoom != _lastKnownRoom)
            {
                RefreshNPCMarkers();
                _lastKnownRoom = currentRoom;
            }

            if (IsFullOpen)
                DrawFullOverlay();
            else
                DrawCornerMinimap();
        }

        public void ToggleFullMap()
        {
            IsFullOpen = !IsFullOpen;
        }

        private void DrawCornerMinimap()
        {
            float size = 200f;
            float padding = 10f;
            Rect mapRect = new(Screen.width - size - padding, padding, size, size);

            // Background
            DrawRect(new Rect(mapRect.x - 2, mapRect.y - 2, mapRect.width + 4, mapRect.height + 4),
                new Color(0, 0, 0, 0.4f));

            DrawMapContent(mapRect);
        }

        private void DrawFullOverlay()
        {
            // Dim background
            DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));

            // Map centered, 60% of screen
            float size = Mathf.Min(Screen.width, Screen.height) * 0.6f;
            Rect mapRect = new((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size);

            // Background
            DrawRect(new Rect(mapRect.x - 4, mapRect.y - 4, mapRect.width + 8, mapRect.height + 8),
                new Color(0.1f, 0.1f, 0.15f, 0.9f));

            DrawMapContent(mapRect);

            // Label
            GUI.color = Color.white;
            GUI.Label(new Rect(mapRect.x, mapRect.y - 25, mapRect.width, 20),
                "DUNGEON MAP (Tab to close)",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 });
        }

        private void DrawMapContent(Rect mapRect)
        {
            var state = GameManager.Instance?.PendingDungeonState;
            var graph = _builder.RoomGraph;

            // Draw edges first (behind rooms)
            if (graph != null)
            {
                foreach (var kvp in graph)
                {
                    int fromIdx = kvp.Key;
                    if (fromIdx >= _builder.Rooms.Length) continue;
                    Vector2 fromPos = WorldToMinimap(_builder.Rooms[fromIdx].WorldBounds.center, mapRect);

                    foreach (int toIdx in kvp.Value)
                    {
                        if (toIdx <= fromIdx || toIdx >= _builder.Rooms.Length) continue; // avoid duplicate edges
                        Vector2 toPos = WorldToMinimap(_builder.Rooms[toIdx].WorldBounds.center, mapRect);
                        DrawLine(fromPos, toPos, EdgeColor, 1f);
                    }
                }
            }

            // Draw rooms
            foreach (var room in _builder.Rooms)
            {
                bool visited = state != null && state.HasVisited(room.Index);
                bool isAdjacent = IsAdjacentToVisited(room.Index, state, graph);

                if (visited)
                {
                    // Explored: filled rectangle proportional to bounds
                    Rect roomRect = WorldBoundsToMinimap(room.WorldBounds, mapRect);

                    Color fillColor = room.IsCorridor ? CorridorColor :
                        room.Tier switch { 1 => Tier1Color, 2 => Tier2Color, 3 => Tier3Color, _ => Tier1Color };

                    DrawRect(roomRect, fillColor);

                    // Special outlines
                    if (room.IsBoss) DrawRectOutline(roomRect, BossOutline, 2f);
                    if (room.IsEntrance) DrawRectOutline(roomRect, EntranceOutline, 2f);

                    // Room labels in full overlay
                    if (IsFullOpen && !room.IsCorridor)
                    {
                        GUI.color = Color.white;
                        var style = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 9, alignment = TextAnchor.MiddleCenter
                        };
                        string label = room.IsBoss ? "BOSS" : $"T{room.Tier}";
                        GUI.Label(roomRect, label, style);
                    }
                }
                else if (isAdjacent || room.IsBoss)
                {
                    // Unexplored but adjacent or boss: small circle with "?"
                    Vector2 center = WorldToMinimap(room.WorldBounds.center, mapRect);
                    float radius = room.IsBoss ? 8f : 5f;
                    DrawRect(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2),
                        UnexploredColor);
                    if (room.IsBoss) DrawRectOutline(
                        new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2),
                        BossOutline, 1f);

                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    GUI.Label(new Rect(center.x - 5, center.y - 6, 10, 12), "?",
                        new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 });
                }
            }

            // Draw NPC markers (only in explored rooms)
            foreach (var (pos, role) in _npcMarkers)
            {
                int roomIdx = _builder.GetRoomAtPosition(pos);
                if (state != null && !state.HasVisited(roomIdx)) continue;

                Vector2 markerPos = WorldToMinimap(pos, mapRect);
                Color markerColor = role == DungeonNPCRole.AmbientEnemy ? EnemyColor : FriendlyColor;
                float dotSize = 4f;
                DrawRect(new Rect(markerPos.x - dotSize / 2, markerPos.y - dotSize / 2, dotSize, dotSize),
                    markerColor);
            }

            // Draw player dot (always on top)
            if (_playerTransform != null)
            {
                Vector2 playerPos = WorldToMinimap(_playerTransform.position, mapRect);
                float dotSize = 6f;
                DrawRect(new Rect(playerPos.x - dotSize / 2, playerPos.y - dotSize / 2, dotSize, dotSize),
                    PlayerColor);
            }
        }

        private Vector2 WorldToMinimap(Vector3 worldPos, Rect mapRect)
        {
            float nx = Mathf.InverseLerp(_worldMin.x, _worldMax.x, worldPos.x);
            float nz = Mathf.InverseLerp(_worldMin.y, _worldMax.y, worldPos.z);
            return new Vector2(
                mapRect.x + nx * mapRect.width,
                mapRect.y + (1f - nz) * mapRect.height); // flip Z for screen Y
        }

        private Rect WorldBoundsToMinimap(Bounds bounds, Rect mapRect)
        {
            Vector2 min = WorldToMinimap(new Vector3(bounds.min.x, 0, bounds.max.z), mapRect);
            Vector2 max = WorldToMinimap(new Vector3(bounds.max.x, 0, bounds.min.z), mapRect);
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private bool IsAdjacentToVisited(int roomIndex, DungeonState state, IReadOnlyDictionary<int, List<int>> graph)
        {
            if (state == null || graph == null) return false;
            if (!graph.TryGetValue(roomIndex, out var neighbors)) return false;
            foreach (int neighbor in neighbors)
                if (state.HasVisited(neighbor)) return true;
            return false;
        }

        private void RefreshNPCMarkers()
        {
            _npcMarkers.Clear();
            var npcs = FindObjectsByType<DungeonNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
                _npcMarkers.Add((npc.transform.position, npc.Role));
        }

        private void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _solidTex);
        }

        private void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color); // top
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // bottom
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color); // left
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // right
        }

        private void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            // IMGUI line via rotated rect
            Vector2 diff = b - a;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            float length = diff.magnitude;

            var pivot = new Vector2(a.x, a.y);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pivot);
            DrawRect(new Rect(a.x, a.y - thickness / 2, length, thickness), color);
            GUI.matrix = matrix;
        }

        private void OnDestroy()
        {
            if (_solidTex != null) Destroy(_solidTex);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonMinimap.cs && git commit -m "feat: add DungeonMinimap with IMGUI corner overlay and full Tab toggle"
```

---

### Task 7: Wire Minimap into DungeonExplorer

**Files:**
- Modify: `Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs`

- [ ] **Step 1: Add minimap field and creation**

After the existing state fields (after line 38: `private Dictionary<int, int> _cachedRoomDepths;`), add:

```csharp
        private DungeonMinimap _minimap;
```

- [ ] **Step 2: Create minimap at end of InitializeWithDA**

In `InitializeWithDA()`, after `_initialized = true;` (line 94) and before the Debug.Log, add:

```csharp
            // Create minimap
            var minimapGO = new GameObject("DungeonMinimap");
            _minimap = minimapGO.AddComponent<DungeonMinimap>();
            _minimap.Initialize(_daBuilder, _playerTransform);
```

- [ ] **Step 3: Add Tab toggle and movement suppression in Update**

Replace the `Update()` method (lines 55-60) with:

```csharp
        private void Update()
        {
            if (!_initialized || _playerTransform == null) return;

            // Tab toggles full minimap overlay
            if (Input.GetKeyDown(KeyCode.Tab) && _minimap != null)
                _minimap.ToggleFullMap();

            // Suppress movement when full map is open
            if (_minimap != null && _minimap.IsFullOpen) return;

            HandleMovement();
            UpdateFogOfWar();
        }
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Dev/Forever engine" && git add Assets/Scripts/Demo/Dungeon/DungeonExplorer.cs && git commit -m "feat: wire minimap into DungeonExplorer with Tab toggle and movement suppression"
```

---

### Task 8: Final Compile Check

- [ ] **Step 1: Verify all files compile**

```bash
cd "C:/Dev/Forever engine" && git diff --stat $(git log --oneline -9 | tail -1 | cut -d' ' -f1)..HEAD
```

Review all 7 files changed.

- [ ] **Step 2: Check for any syntax issues**

```bash
cd "C:/Dev/Forever engine" && rsp=$(ls Library/Bee/*.rsp 2>/dev/null | head -1) && if [ -n "$rsp" ]; then "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe" "@$rsp" 2>&1 | tail -30; else echo "No rsp files — manual review"; fi
```

- [ ] **Step 3: Verify commit history**

```bash
cd "C:/Dev/Forever engine" && git log --oneline -10
```

All 7 commits from this plan should be present.

- [ ] **Step 4: Fix any issues and commit**

If compile errors found:
```bash
cd "C:/Dev/Forever engine" && git add -u && git commit -m "fix: resolve compile errors from dungeon life implementation"
```
