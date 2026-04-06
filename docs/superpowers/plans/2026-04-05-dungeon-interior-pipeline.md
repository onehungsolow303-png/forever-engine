# Dungeon Interior Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the existing C# PipelineCoordinator to produce schema v1.1.0 JSON that MapImporter consumes, replacing the incomplete Python bridge in LocationInteriorManager.

**Architecture:** New `MapSerializer` converts `PipelineCoordinator.GenerationResult` (terrain, rooms, spawns) to on-disk JSON + terrain PNG. `LocationInteriorManager` calls Pipeline -> Serializer -> sets `GameManager.PendingMapDataPath` -> loads Game scene. Downstream (`GameBootstrap`, `MapImporter`) unchanged.

**Tech Stack:** Unity 6 C# (JsonUtility, Texture2D, ImageConversion), DOTS ECS (read-only — MapImporter creates entities), Shared schemas at `C:\Dev\.shared\schemas\map_schema.json` v1.1.0.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Generation/MapSerializer.cs` | **Create** | Serialize `GenerationResult` to v1.1.0 JSON + terrain PNG |
| `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs` | **Modify** | Replace Python bridge with C# pipeline call |

**Unchanged files (contract consumers):**
- `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs` — reads JSON
- `Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs` — reads `PendingMapDataPath`
- `Assets/Scripts/Generation/PipelineCoordinator.cs` — returns `GenerationResult`
- `Assets/Scripts/Demo/GameManager.cs` — holds `PendingMapDataPath`

---

### Task 1: Create MapSerializer.cs

**Files:**
- Create: `Assets/Scripts/Generation/MapSerializer.cs`

**Context:** `PipelineCoordinator.Generate()` returns a `GenerationResult` struct containing:
- `Terrain` (`TerrainGenerator.TerrainResult`): `Walkability` (bool[]), `TerrainColor` (byte[] RGB, 3 bytes/pixel), `Elevation` (float[]), `Width`, `Height`
- `Layout` (`RoomGraph`): `Nodes` (List\<RoomNode\> with Id, X, Y, W, H, Purpose), `Edges` (List\<RoomEdge\> with FromId, ToId, Type), `EntranceNodeId`
- `Population` (`PopulationGenerator.PopulationResult`): `PlayerSpawn`, `Encounters`, `Traps`, `Loot`, `Dressing` (all `EntitySpawn` with X, Y, Type, Variant, Value)
- `Request` (`MapGenerationRequest`): `MapType`, `Biome`, `Width`, `Height`, `Seed`, `PartyLevel`, `PartySize`

`MapImporter.Import()` reads JSON via `JsonUtility.FromJson<MapData>()`. The DTO field names must match exactly (snake_case). `MapImporter.MapData` does NOT include `room_graph` — JsonUtility ignores unknown fields on deserialization, so we can include it for schema completeness.

The schema's `spawns[].stats` uses field names: `hp`, `ac`, `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `charisma`, `speed`, `atk_dice`.

`GameTables` does NOT have a `GetCreatureStats()` method. Use the spec's scaled defaults: `HP = partyLevel * 4 + 4`, `AC = 10 + partyLevel / 2`, all abilities 10, speed 6, `atk_dice = "1d6+{partyLevel/2}"`.

`ConnectionType` enum values: `Corridor`, `Door`, `Secret`, `Open` — map to schema strings: `"corridor"`, `"door"`, `"secret"`, `"open"`.

- [ ] **Step 1: Create MapSerializer.cs with serialization DTOs**

Create `Assets/Scripts/Generation/MapSerializer.cs`:

```csharp
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using ForeverEngine.Generation.Agents;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Generation
{
    public static class MapSerializer
    {
        /// <summary>
        /// Serializes a GenerationResult to map_schema v1.1.0 JSON + terrain PNG.
        /// Returns the path to the written MapData.json.
        /// </summary>
        public static string Serialize(PipelineCoordinator.GenerationResult result, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            // Build terrain PNG
            string pngRelative = "terrain_z0.png";
            string pngPath = Path.Combine(outputDir, pngRelative);
            WriteTerrainPng(result.Terrain, pngPath);

            // Build serializable structure
            var mapData = new SMapData
            {
                config = BuildConfig(result.Request),
                z_levels = new[] { BuildZLevel(result, pngRelative) },
                transitions = new STransition[0],
                spawns = BuildSpawns(result.Population, result.Request.PartyLevel),
                labels = new SLabel[0]
            };

            string jsonPath = Path.Combine(outputDir, "MapData.json");
            string json = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(jsonPath, json);

            Debug.Log($"[MapSerializer] Wrote {jsonPath} ({json.Length} bytes) + {pngPath}");
            return jsonPath;
        }

        // ── Builders ──────────────────────────────────────────────────────

        private static SConfig BuildConfig(MapGenerationRequest req)
        {
            return new SConfig
            {
                width = req.Width,
                height = req.Height,
                map_type = req.MapType,
                biome = req.Biome,
                seed = req.Seed,
                schema_version = "1.1.0",
                generator_version = "forever-engine-cs-1.0",
                created_at = System.DateTime.UtcNow.ToString("o")
            };
        }

        private static SZLevel BuildZLevel(PipelineCoordinator.GenerationResult result, string pngRelative)
        {
            int len = result.Terrain.Width * result.Terrain.Height;

            // bool[] -> int[]
            var walk = new int[len];
            for (int i = 0; i < len; i++)
                walk[i] = result.Terrain.Walkability[i] ? 1 : 0;

            // Entities from traps, loot, dressing
            var entities = new List<SEntity>();
            int eid = 0;
            if (result.Population.Traps != null)
                foreach (var t in result.Population.Traps)
                    entities.Add(new SEntity { id = $"trap_{eid++}", type = "trap", x = t.X, y = t.Y, variant = t.Variant });
            if (result.Population.Loot != null)
                foreach (var l in result.Population.Loot)
                    entities.Add(new SEntity { id = $"loot_{eid++}", type = "loot", x = l.X, y = l.Y, variant = l.Variant });
            if (result.Population.Dressing != null)
                foreach (var d in result.Population.Dressing)
                    entities.Add(new SEntity { id = $"dressing_{eid++}", type = "dressing", x = d.X, y = d.Y, variant = d.Variant });

            // Room graph
            var rooms = new List<SRoom>();
            var connections = new List<SConnection>();
            if (result.Layout != null)
            {
                foreach (var node in result.Layout.Nodes)
                    rooms.Add(new SRoom { id = node.Id, x = node.X, y = node.Y, w = node.W, h = node.H, purpose = node.Purpose ?? "" });
                foreach (var edge in result.Layout.Edges)
                    connections.Add(new SConnection
                    {
                        from_room = edge.FromId,
                        to_room = edge.ToId,
                        type = edge.Type switch
                        {
                            ConnectionType.Door => "door",
                            ConnectionType.Secret => "secret",
                            ConnectionType.Open => "open",
                            _ => "corridor"
                        }
                    });
            }

            return new SZLevel
            {
                z = 0,
                terrain_png = pngRelative,
                walkability = walk,
                entities = entities.ToArray(),
                room_graph = new SRoomGraph { rooms = rooms.ToArray(), connections = connections.ToArray() }
            };
        }

        private static SSpawn[] BuildSpawns(PopulationGenerator.PopulationResult pop, int partyLevel)
        {
            var spawns = new List<SSpawn>();

            // Player spawn
            if (pop.PlayerSpawn.Type != null)
            {
                spawns.Add(new SSpawn
                {
                    name = "Player",
                    x = pop.PlayerSpawn.X,
                    y = pop.PlayerSpawn.Y,
                    z = 0,
                    token_type = "player",
                    ai_behavior = "scripted",
                    stats = new SStats { hp = 20, ac = 14, strength = 14, dexterity = 14, constitution = 14, intelligence = 10, wisdom = 10, charisma = 10, speed = 6, atk_dice = "1d8+2" }
                });
            }

            // Enemy encounters — scaled defaults per spec
            if (pop.Encounters != null)
            {
                foreach (var enc in pop.Encounters)
                {
                    spawns.Add(new SSpawn
                    {
                        name = enc.Variant ?? "creature",
                        x = enc.X,
                        y = enc.Y,
                        z = 0,
                        token_type = "enemy",
                        ai_behavior = "chase",
                        stats = new SStats
                        {
                            hp = partyLevel * 4 + 4,
                            ac = 10 + partyLevel / 2,
                            strength = 10, dexterity = 10, constitution = 10,
                            intelligence = 10, wisdom = 10, charisma = 10,
                            speed = 6,
                            atk_dice = $"1d6+{partyLevel / 2}"
                        }
                    });
                }
            }

            return spawns.ToArray();
        }

        // ── Terrain PNG ───────────────────────────────────────────────────

        private static void WriteTerrainPng(TerrainGenerator.TerrainResult terrain, string path)
        {
            int w = terrain.Width, h = terrain.Height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;

            // TerrainColor is byte[w*h*3] in row-major RGB
            // Texture2D.SetPixels32 expects bottom-to-top, so flip Y
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w; // flip Y for Unity texture coords
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    int ci = (srcRow + x) * 3;
                    pixels[dstRow + x] = new Color32(
                        terrain.TerrainColor[ci],
                        terrain.TerrainColor[ci + 1],
                        terrain.TerrainColor[ci + 2],
                        255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(tex);
        }

        // ── Serialization DTOs (snake_case field names match map_schema v1.1.0) ──

        [System.Serializable]
        public class SMapData
        {
            public SConfig config;
            public SZLevel[] z_levels;
            public STransition[] transitions;
            public SSpawn[] spawns;
            public SLabel[] labels;
        }

        [System.Serializable]
        public class SConfig
        {
            public int width, height;
            public string map_type, biome;
            public int seed;
            public string schema_version, generator_version, created_at;
        }

        [System.Serializable]
        public class SZLevel
        {
            public int z;
            public string terrain_png;
            public int[] walkability;
            public SEntity[] entities;
            public SRoomGraph room_graph;
        }

        [System.Serializable]
        public class SEntity
        {
            public string id, type;
            public int x, y;
            public string variant;
        }

        [System.Serializable]
        public class SRoomGraph
        {
            public SRoom[] rooms;
            public SConnection[] connections;
        }

        [System.Serializable]
        public class SRoom
        {
            public int id, x, y, w, h;
            public string purpose;
        }

        [System.Serializable]
        public class SConnection
        {
            public int from_room, to_room;
            public string type;
        }

        [System.Serializable]
        public class STransition
        {
            public int x, y, from_z, to_z;
            public string type;
        }

        [System.Serializable]
        public class SSpawn
        {
            public string name;
            public int x, y, z;
            public string token_type, ai_behavior;
            public SStats stats;
        }

        [System.Serializable]
        public class SStats
        {
            public int hp, ac;
            public int strength, dexterity, constitution, intelligence, wisdom, charisma;
            public int speed;
            public string atk_dice;
        }

        [System.Serializable]
        public class SLabel
        {
            public int x, y, z;
            public string text, category;
        }
    }
}
```

- [ ] **Step 2: Commit MapSerializer**

```bash
cd "C:/Dev/Forever engin"
git add Assets/Scripts/Generation/MapSerializer.cs
git commit -m "feat: add MapSerializer for C# dungeon generation pipeline

Converts PipelineCoordinator.GenerationResult to map_schema v1.1.0 JSON
plus terrain PNG. Serialization DTOs match the shared schema at
.shared/schemas/map_schema.json exactly."
```

---

### Task 2: Rewire LocationInteriorManager to C# Pipeline

**Files:**
- Modify: `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs`

**Context:** Currently this file:
- Has `using ForeverEngine.MonoBehaviour.ContentLoader;` (for AssetGeneratorBridge)
- Has `using System.Threading.Tasks;` (for async Python bridge)
- Creates `AssetGeneratorBridge` on `_bridge` field in Awake()
- Has `SetBridgeField()` reflection helper
- Has `async Task GenerateAndShowAsync()` calling `_bridge.GenerateContentAsync()`
- Has inspector fields: `pythonPath`, `generatorScriptPath`, `timeoutSeconds`
- Calls `TryParseResponse()` which parses `GenerationResponse` (Python bridge response type)

All of these get removed. The replacement calls `PipelineCoordinator.Generate()` + `MapSerializer.Serialize()` synchronously.

Keep: `EnterLocation()` (modified), `GetCachePath()`, `GetLocationProfile()`, `ApplyLocationEffects()`, `ShowPopup()`, `OnGUI()`, singleton pattern.

- [ ] **Step 1: Replace LocationInteriorManager.cs**

Replace the entire file contents of `Assets/Scripts/Demo/Locations/LocationInteriorManager.cs` with:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using ForeverEngine.Generation;
using ForeverEngine.Generation.Data;
using ForeverEngine.Shared;

namespace ForeverEngine.Demo.Locations
{
    /// <summary>
    /// Manages generating and loading interior maps for overworld locations.
    ///
    /// Flow:
    ///   1. OverworldManager calls EnterLocation(locationData) when player presses Enter.
    ///   2. Checks for a cached MapData.json in persistentDataPath.
    ///   3. If absent: runs PipelineCoordinator + MapSerializer to generate and write map files.
    ///   4. Validates the JSON via SchemaValidator.
    ///   5. Sets GameManager.PendingMapDataPath and loads the Game scene.
    /// </summary>
    public class LocationInteriorManager : UnityEngine.MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static LocationInteriorManager Instance { get; private set; }

        // ── Private state ─────────────────────────────────────────────────────
        private bool   _popupVisible;
        private string _popupTitle   = "";
        private string _popupBody    = "";

        // ── Location → biome table ────────────────────────────────────────────
        private static readonly System.Collections.Generic.Dictionary<string, (string mapType, string biome)>
            s_LocationProfile = new()
            {
                ["camp"]     = ("camp",    "plains"),
                ["town"]     = ("village", "forest"),
                ["dungeon"]  = ("dungeon", "cave"),
                ["fortress"] = ("fort",    "mountain"),
                ["castle"]   = ("castle",  "swamp"),
            };

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void EnterLocation(LocationData loc)
        {
            if (loc == null)
            {
                Debug.LogWarning("[LocationInterior] EnterLocation called with null LocationData.");
                return;
            }

            Debug.Log($"[LocationInterior] Entering {loc.Name} ({loc.Type})");
            ApplyLocationEffects(loc);

            string mapType = loc.MapType;
            if (string.IsNullOrEmpty(mapType))
            {
                ShowPopup(loc.Name, $"You enter {loc.Name}.\n\n(No interior map defined for this location type.)");
                return;
            }

            // Check cache
            string cachePath = GetCachePath(loc);
            if (File.Exists(cachePath))
            {
                Debug.Log($"[LocationInterior] Cache hit: {cachePath}");
                LoadAndTransition(loc, cachePath);
                return;
            }

            // Generate via C# pipeline
            GenerateInterior(loc);
        }

        // ── Interior generation ───────────────────────────────────────────────

        private void GenerateInterior(LocationData loc)
        {
            var (mapType, biome) = GetLocationProfile(loc);
            int partyLevel = GameManager.Instance?.Player?.Level ?? 3;

            var request = new MapGenerationRequest
            {
                MapType = mapType,
                Biome = biome,
                Width = 128,
                Height = 128,
                Seed = GameManager.Instance?.CurrentSeed ?? 42,
                PartyLevel = partyLevel,
                PartySize = 1
            };

            Debug.Log($"[LocationInterior] Generating {mapType}/{biome} 128x128 level:{partyLevel}");

            var result = PipelineCoordinator.Generate(request);
            if (!result.Success)
            {
                ShowPopup(loc.Name, $"Generation failed:\n{result.Error}");
                return;
            }

            string outputDir = Path.GetDirectoryName(GetCachePath(loc));
            string mapPath = MapSerializer.Serialize(result, outputDir);

            LoadAndTransition(loc, mapPath);
        }

        private void LoadAndTransition(LocationData loc, string mapPath)
        {
            string mapJson = File.ReadAllText(mapPath);
            if (!SchemaValidator.ValidateMapData(mapJson))
                Debug.LogWarning("[LocationInterior] Map data failed validation — loading anyway.");

            GameManager.Instance.PendingMapDataPath = mapPath;
            GameManager.Instance.PendingLocationId = loc.Id;
            SceneManager.LoadScene("Game");
        }

        // ── Popup helpers ─────────────────────────────────────────────────────

        private void ShowPopup(string title, string body)
        {
            _popupTitle   = title;
            _popupBody    = body;
            _popupVisible = true;
        }

        private void OnGUI()
        {
            if (!_popupVisible) return;

            const float W = 480f;
            const float H = 320f;
            float x = (Screen.width  - W) / 2f;
            float y = (Screen.height - H) / 2f;

            GUI.Box(new Rect(x, y, W, H), "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(x + 10, y + 10, W - 20, 28), _popupTitle, titleStyle);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                wordWrap  = true,
                alignment = TextAnchor.UpperLeft,
            };
            GUI.Label(new Rect(x + 16, y + 46, W - 32, H - 80), _popupBody, bodyStyle);

            if (GUI.Button(new Rect(x + W / 2f - 60, y + H - 42, 120, 30), "Close"))
                _popupVisible = false;
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private static string GetCachePath(LocationData loc)
        {
            return Path.Combine(
                Application.persistentDataPath,
                "generated_maps",
                loc.Name.Replace(" ", "_").Replace("'", ""),
                "MapData.json");
        }

        private static (string mapType, string biome) GetLocationProfile(LocationData loc)
        {
            if (s_LocationProfile.TryGetValue(loc.Id, out var profile))
                return profile;
            return (loc.MapType ?? "dungeon", "generic");
        }

        private static void ApplyLocationEffects(LocationData loc)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.Player.DiscoveredLocations.Add(loc.Id);

            if (loc.IsSafe)
            {
                gm.Player.LastSafeLocation = loc.Id;

                if (loc.Type is "camp" or "fortress")
                {
                    gm.Player.FullRest();
                    Debug.Log($"[LocationInterior] Rested at {loc.Name}");
                }
            }
        }
    }
}
```

- [ ] **Step 2: Commit LocationInteriorManager changes**

```bash
cd "C:/Dev/Forever engin"
git add Assets/Scripts/Demo/Locations/LocationInteriorManager.cs
git commit -m "feat: rewire LocationInteriorManager to C# generation pipeline

Replace Python AssetGeneratorBridge with PipelineCoordinator + MapSerializer.
Generation is now synchronous C# — no external process dependency.
Cache check remains: cached MapData.json is reused on subsequent visits."
```

---

### Task 3: Verify Compilation

**Files:** None (verification only)

- [ ] **Step 1: Run Unity batch mode compilation check**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -quit -projectPath "C:/Dev/Forever engin" \
  -logFile - 2>&1 | tail -50
```

Expected: exit code 0, no `error CS` lines in output.

If compilation fails, fix the reported errors before proceeding.

- [ ] **Step 2: Rebuild demo scenes**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -quit -projectPath "C:/Dev/Forever engin" \
  -executeMethod ForeverEngine.Editor.DemoSceneBuilder.BuildAll \
  -logFile - 2>&1 | tail -30
```

Expected: "MainMenu scene created", "Overworld scene created", "BattleMap scene created" in output.

---

### Task 4: Set ANTHROPIC_API_KEY Environment Variable

**Files:** None (system configuration)

**Context:** `ClaudeAPIClient.cs` (line 25-26) reads `System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")` at Awake. If the env var is not set, `IsConfigured` returns false and all dialogue calls return empty strings with a warning.

- [ ] **Step 1: Set the Windows user environment variable**

The user needs to provide their actual API key. Prompt them:

> "I need your Anthropic API key to set the `ANTHROPIC_API_KEY` environment variable. You can either:
> 1. Run `! setx ANTHROPIC_API_KEY sk-ant-...your-key...` in the prompt (persists across sessions)
> 2. Or tell me the key and I'll set it
>
> The key is used by `ClaudeAPIClient.cs` for NPC dialogue at runtime."

After the key is set, verify:

```bash
echo $ANTHROPIC_API_KEY
```

Expected: The key value (non-empty).

- [ ] **Step 2: Verify Unity will see it**

Unity reads environment variables at process start. After setting `ANTHROPIC_API_KEY` via `setx`, Unity must be restarted (or launched fresh) to pick it up. The auto-launch in Task 5 handles this naturally since it opens a new Unity process.

---

### Task 5: Auto-Launch and Play-Test Full Loop

**Files:** None (verification only)

**Context:** `AutoLaunchDemo.cs` is an `[InitializeOnLoad]` editor script that checks for `tests/launch-demo.flag` at editor startup. If the flag exists, it deletes it, opens `MainMenu.unity`, and enters Play mode.

- [ ] **Step 1: Create the auto-launch flag file**

```bash
mkdir -p "C:/Dev/Forever engin/tests"
touch "C:/Dev/Forever engin/tests/launch-demo.flag"
```

- [ ] **Step 2: Launch Unity Editor**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -projectPath "C:/Dev/Forever engin" &
```

This opens the Unity Editor (not batch mode). AutoLaunchDemo detects the flag, opens MainMenu, and enters Play mode automatically.

- [ ] **Step 3: Manual play-test checklist**

The user verifies the full loop in the Unity Editor:

1. **MainMenu** loads and UI is visible
2. **Character creation** — select or create a character, press Start
3. **Overworld** — player token appears, can move with arrow keys
4. **Enter dungeon** — move to a dungeon location, press Enter
   - Console should show: `[LocationInterior] Generating dungeon/cave 128x128`
   - Console should show: `[MapSerializer] Wrote ... MapData.json`
   - Console should show: `[ForeverEngine] Loading: ...`
   - Console should show: `[MapImporter] Loaded dungeon (128x128)`
5. **Game scene** — dungeon tiles visible, player token present, enemies visible
6. **Combat** — move toward an enemy, verify combat triggers
7. **Return** — after combat, verify return to overworld works

If Claude NPC dialogue is wired in the overworld (NPCs at safe locations), verify:
- Console shows `[ClaudeAPI]` log lines (not "No API key configured")
- NPC responds with generated dialogue

- [ ] **Step 4: Commit any fixes from play-testing**

If play-testing reveals issues, fix them and commit:

```bash
cd "C:/Dev/Forever engin"
git add -A
git commit -m "fix: address issues found during dungeon interior play-test"
```
