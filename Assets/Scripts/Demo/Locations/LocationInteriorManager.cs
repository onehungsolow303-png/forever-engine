using UnityEngine;

namespace ForeverEngine.Demo.Locations
{
    /// <summary>
    /// Routes overworld location entry to the appropriate game-state handler.
    ///
    /// Originally this was the entry point for a walkable interior pipeline
    /// (PipelineCoordinator + MapSerializer + Game scene with TileRenderer
    /// and FogRenderer). That pipeline is currently disabled — see commit
    /// 81773b1: "dungeon locations now trigger battle encounters instead
    /// of loading broken Game scene". The next-day commit bff4e3b
    /// playtest-verified the full loop with the battle fallback in place,
    /// so this is the SHIPPED design, not a temporary placeholder.
    ///
    /// Current flow:
    ///   1. OverworldManager calls EnterLocation(loc) when player presses Enter
    ///   2. ApplyLocationEffects updates discovered locations + auto-rests
    ///      at safe camp/fortress locations
    ///   3. Locations with no MapType show a popup ("you enter X")
    ///   4. Locations with a MapType (dungeon/etc) trigger a battle encounter
    /// </summary>
    public class LocationInteriorManager : UnityEngine.MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static LocationInteriorManager Instance { get; private set; }

        // ── Private state ─────────────────────────────────────────────────────
        private bool   _popupVisible;
        private string _popupTitle   = "";
        private string _popupBody    = "";

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
                    // Mutate CharacterSheet first when one exists, then sync
                    // to PlayerData. Without the sheet path, combat would
                    // pull stale HP from CharacterSheet (which combat reads
                    // via BattleCombatant.FromCharacterSheet) and the auto-
                    // rest would be invisible to the next encounter. Same
                    // bug we fixed for the dialogue rest path in fc8237f.
                    if (gm.Character != null)
                    {
                        RPGBridge.ApplyLongRestToSheet(gm.Character);
                        gm.SyncPlayerFromCharacter();
                    }
                    gm.Player.FullRest();
                    Debug.Log($"[LocationInterior] Rested at {loc.Name}");
                }

                SaveManager.Save();
            }
        }
    }
}
