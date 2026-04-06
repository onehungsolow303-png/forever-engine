using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Threading.Tasks;
using ForeverEngine.MonoBehaviour.ContentLoader;
using ForeverEngine.Shared;

namespace ForeverEngine.Demo.Locations
{
    /// <summary>
    /// Manages generating and loading interior maps for overworld locations.
    ///
    /// Flow:
    ///   1. OverworldManager calls EnterLocation(locationData) when player presses Enter.
    ///   2. Checks for a cached MapData.json in persistentDataPath.
    ///   3. If absent: builds a GenerationRequest and calls AssetGeneratorBridge.
    ///   4. Saves the response JSON to persistentDataPath for future cache hits.
    ///   5. Shows an IMGUI popup summarising what was generated (entities / encounters / NPCs).
    ///
    /// Future: replace popup with MapImporter load + scene transition.
    /// </summary>
    public class LocationInteriorManager : UnityEngine.MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static LocationInteriorManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Python Bridge")]
        [Tooltip("Python executable on system PATH, or absolute path.")]
        [SerializeField] private string pythonPath = "python";

        [Tooltip("Absolute path to map_generator.py in the Map Generator project.")]
        [SerializeField] private string generatorScriptPath =
            "C:/Users/bp303/Documents/Map Generator/mapgen_agents/map_generator.py";

        [Tooltip("Seconds to wait for the Python process before cancelling.")]
        [SerializeField] private int timeoutSeconds = 60;

        // ── Private state ─────────────────────────────────────────────────────
        private AssetGeneratorBridge _bridge;

        private bool   _popupVisible;
        private string _popupTitle   = "";
        private string _popupBody    = "";
        private bool   _isGenerating;

        // ── Location → biome table ────────────────────────────────────────────
        // Survivor's Camp → camp / plains
        // Ashwick Ruins   → village / forest
        // The Hollow      → dungeon / cave
        // Ironhold        → fort / mountain
        // Throne of Rot   → castle / swamp
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

            // Attach and configure the bridge on the same GameObject
            _bridge = gameObject.AddComponent<AssetGeneratorBridge>();
            // Use reflection to set private serialized fields so the bridge works
            // without requiring a manual Inspector setup.
            SetBridgeField("pythonPath",    pythonPath);
            SetBridgeField("generatorPath", generatorScriptPath);
            SetBridgeField("timeoutSeconds", timeoutSeconds);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Entry point called by OverworldManager when the player presses Enter on a location.
        /// </summary>
        public void EnterLocation(LocationData loc)
        {
            if (_isGenerating)
            {
                Debug.Log("[LocationInterior] Generation already in progress.");
                return;
            }

            if (loc == null)
            {
                Debug.LogWarning("[LocationInterior] EnterLocation called with null LocationData.");
                return;
            }

            Debug.Log($"[LocationInterior] Entering {loc.Name} ({loc.Type})");

            // Handle immediate effects that don't require generation
            ApplyLocationEffects(loc);

            // If no map type, skip generation
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
                string cachedJson = File.ReadAllText(cachePath);
                ShowGenerationPopup(loc, cachedJson, fromCache: true);
                return;
            }

            // Generate async
            _ = GenerateAndShowAsync(loc);
        }

        // ── Interior generation ───────────────────────────────────────────────

        private async Task GenerateAndShowAsync(LocationData loc)
        {
            _isGenerating = true;
            ShowPopup(loc.Name, $"Generating {loc.Name} interior...\n\nPlease wait.");

            var (mapType, biome) = GetLocationProfile(loc);
            int partyLevel = GameManager.Instance?.Player?.Level ?? 3;

            var request = new GenerationRequest
            {
                id               = $"{loc.Id}_{System.DateTime.UtcNow.Ticks}",
                type             = mapType,
                biome            = biome,
                size             = "medium",
                partyLevel       = partyLevel,
                partySize        = 1,
                difficulty       = loc.IsSafe ? "easy" : "medium",
                narrativeContext = $"Location: {loc.Name}. Type: {loc.Type}. Biome: {biome}.",
            };

            Debug.Log($"[LocationInterior] Requesting generation — type:{mapType} biome:{biome} level:{partyLevel}");

            string responseJson = await _bridge.GenerateContentAsync(request);

            _isGenerating = false;

            if (string.IsNullOrEmpty(responseJson))
            {
                ShowPopup(loc.Name,
                    $"Failed to generate {loc.Name} interior.\n\n" +
                    "Check that the Python generator is installed and that\n" +
                    $"'{generatorScriptPath}' exists.\n\n" +
                    "See Console for details.");
                return;
            }

            // Cache the result
            string cachePath = GetCachePath(loc);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                File.WriteAllText(cachePath, responseJson);
                Debug.Log($"[LocationInterior] Cached to {cachePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LocationInterior] Could not cache result: {ex.Message}");
            }

            ShowGenerationPopup(loc, responseJson, fromCache: false);
        }

        // ── Popup helpers ─────────────────────────────────────────────────────

        private void ShowGenerationPopup(LocationData loc, string responseJson, bool fromCache)
        {
            var resp = TryParseResponse(responseJson);

            string source = fromCache ? "(cached)" : "(freshly generated)";

            if (resp != null && resp.success)
            {
                // Resolve the map data path: prefer response dataPath, fall back to cache
                string mapPath = !string.IsNullOrEmpty(resp.dataPath) ? resp.dataPath : GetCachePath(loc);

                if (!File.Exists(mapPath))
                {
                    ShowPopup(loc.Name, $"Map data file not found:\n{mapPath}");
                    return;
                }

                // Validate before loading
                string mapJson = File.ReadAllText(mapPath);
                if (!SchemaValidator.ValidateMapData(mapJson))
                    Debug.LogWarning($"[LocationInterior] Map data at {mapPath} failed validation — loading anyway.");

                // Hand off to GameManager and transition to the Game scene
                GameManager.Instance.PendingMapDataPath = mapPath;
                GameManager.Instance.PendingLocationId = loc.Id;
                SceneManager.LoadScene("Game");
            }
            else
            {
                // Response JSON exists but either parse failed or success==false
                string errMsg = resp?.errorMessage ?? "Unknown error";
                string preview = responseJson.Length > 200 ? responseJson.Substring(0, 200) + "..." : responseJson;
                ShowPopup(loc.Name,
                    $"Generation returned but reported failure {source}.\n\n" +
                    $"Error: {errMsg}\n\nRaw preview:\n{preview}");
            }
        }

        private void ShowPopup(string title, string body)
        {
            _popupTitle   = title;
            _popupBody    = body;
            _popupVisible = true;
        }

        // ── IMGUI popup ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_popupVisible) return;

            const float W = 480f;
            const float H = 320f;
            float x = (Screen.width  - W) / 2f;
            float y = (Screen.height - H) / 2f;

            GUI.Box(new Rect(x, y, W, H), "");

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(x + 10, y + 10, W - 20, 28), _popupTitle, titleStyle);

            // Body
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                wordWrap  = true,
                alignment = TextAnchor.UpperLeft,
            };
            GUI.Label(new Rect(x + 16, y + 46, W - 32, H - 80), _popupBody, bodyStyle);

            // Close button
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

            // Fallback: use MapType field, generic biome
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

        private static GenerationResponse TryParseResponse(string json)
        {
            try { return JsonUtility.FromJson<GenerationResponse>(json); }
            catch { return null; }
        }

        /// <summary>
        /// Sets a private serialised field on AssetGeneratorBridge via reflection.
        /// This avoids requiring a prefab/Inspector to configure the bridge at runtime.
        /// </summary>
        private void SetBridgeField(string fieldName, object value)
        {
            var field = typeof(AssetGeneratorBridge)
                .GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(_bridge, value);
            else
                Debug.LogWarning($"[LocationInterior] Could not find field '{fieldName}' on AssetGeneratorBridge.");
        }
    }
}
