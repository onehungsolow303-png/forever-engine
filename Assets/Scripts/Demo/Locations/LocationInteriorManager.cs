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

            // Dungeon interior renderer not yet functional — fall back to battle encounter
            Debug.Log($"[LocationInterior] Entering battle at {loc.Name}");
            GameManager.Instance.EnterBattle($"dungeon_{loc.Type}_{loc.Id}");
        }

        // ── Interior generation ───────────────────────────────────────────────

        private void GenerateInterior(LocationData loc)
        {
            // TODO: Game scene dungeon rendering has a black screen bug (tracked in memory).
            // Skip map generation and fall back to battle encounter until fixed.
            Debug.Log($"[LocationInterior] Entering dungeon battle at {loc.Name}");
            GameManager.Instance.EnterBattle($"dungeon_{loc.Type}_{loc.Id}");
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

                SaveManager.Save();
            }
        }
    }
}
