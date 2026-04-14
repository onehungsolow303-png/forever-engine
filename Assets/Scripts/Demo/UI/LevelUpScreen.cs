using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Centered IMGUI panel that appears when the player gains enough XP to level up.
    ///
    /// The screen blocks play while visible so the player must allocate 2 ability
    /// score points before returning to the world. Points are written to both
    /// PlayerData (STR/DEX/CON mirror) and the CharacterSheet BaseAbilities when
    /// a full sheet is present.
    ///
    /// Usage:
    ///   LevelUpScreen.Show()  — call after XP gain when level-up is detected.
    ///   LevelUpScreen.ShouldShow — guard for OnGUI if you ever need to poll.
    /// </summary>
    public class LevelUpScreen : UnityEngine.MonoBehaviour
    {
        // ── Public interface ─────────────────────────────────────────────────

        /// <summary>True while the level-up panel is awaiting player input.</summary>
        public static bool ShouldShow { get; private set; }

        /// <summary>Call immediately after XP is awarded and level-up is detected.</summary>
        public static void Show()
        {
            ShouldShow     = true;
            _pointsLeft    = 2;
            _allocated     = new int[6]; // STR DEX CON INT WIS CHA
            _newLevel      = (GameManager.Instance?.Player?.Level ?? 0) + 1;
        }

        // ── Layout constants ─────────────────────────────────────────────────

        private const float PanelW  = 500f;
        private const float PanelH  = 420f;
        private const float BtnW    = 180f;
        private const float BtnH    = 28f;
        private const float SmBtnW  = 36f;
        private const float RowH    = 32f;
        private const float Pad     = 16f;

        // ── Stat-button labels ───────────────────────────────────────────────

        private static readonly string[] StatNames  = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };

        // ── State ────────────────────────────────────────────────────────────

        private static int   _pointsLeft;
        private static int[] _allocated;   // pending additions per ability
        private static int   _newLevel;

        // ── IMGUI rendering ──────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!ShouldShow) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // Dark dim overlay
            UITheme.DrawRect(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.70f));

            // Center panel
            float px = (sw - PanelW) * 0.5f;
            float py = (sh - PanelH) * 0.5f;

            UITheme.DrawPanel(new Rect(px, py, PanelW, PanelH));

            float cx = px + Pad;
            float cy = py + Pad;
            float cw = PanelW - Pad * 2f;

            // ── Header ───────────────────────────────────────────────────────
            GUI.Label(new Rect(cx, cy, cw, 36f),
                "LEVEL UP!",
                UITheme.Header(UITheme.FontHuge));
            cy += 40f;

            GUI.Label(new Rect(cx, cy, cw, 24f),
                $"You have reached Level {_newLevel}",
                UITheme.Header(UITheme.FontLarge));
            cy += 28f;

            // ── MaxHP gain notice ─────────────────────────────────────────────
            UITheme.DrawSeparator(cx, cy, cw);
            cy += 8f;

            GUI.Label(new Rect(cx, cy, cw, 22f),
                "+5 Maximum HP gained",
                UITheme.Label(UITheme.FontMedium, UITheme.HPHigh, TextAnchor.MiddleCenter));
            cy += 28f;

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 10f;

            // ── Ability score section ─────────────────────────────────────────
            string pointsLabel = _pointsLeft > 0
                ? $"Choose {_pointsLeft} ability improvement{(_pointsLeft > 1 ? "s" : "")}:"
                : "All points allocated.";

            GUI.Label(new Rect(cx, cy, cw, 22f),
                pointsLabel,
                UITheme.Bold(UITheme.FontMedium, UITheme.TextAccent, TextAnchor.MiddleCenter));
            cy += 28f;

            // Two-column 3-per-row grid
            float colW    = (cw - Pad) * 0.5f;
            float gridTop = cy;

            for (int i = 0; i < 6; i++)
            {
                int col = i % 2;
                int row = i / 2;

                float rx = cx + col * (colW + Pad);
                float ry = gridTop + row * RowH;

                DrawStatButton(rx, ry, colW, i);
            }

            cy = gridTop + 3 * RowH + 10f;

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 10f;

            // ── Confirm button ────────────────────────────────────────────────
            bool canConfirm = _pointsLeft == 0;

            GUI.enabled = canConfirm;

            if (GUI.Button(new Rect(px + (PanelW - BtnW) * 0.5f, cy, BtnW, BtnH + 4f),
                "Confirm Level Up", UITheme.Button()))
            {
                ApplyLevelUp();
            }

            GUI.enabled = true;

            if (!canConfirm)
            {
                cy += BtnH + 10f;
                GUI.Label(new Rect(cx, cy, cw, 20f),
                    $"Allocate {_pointsLeft} more point{(_pointsLeft > 1 ? "s" : "")} to continue.",
                    UITheme.Label(UITheme.FontSmall, UITheme.DisabledGray, TextAnchor.MiddleCenter));
            }
        }

        // ── Stat button row ───────────────────────────────────────────────────

        private static void DrawStatButton(float x, float y, float w, int statIndex)
        {
            // Current value display
            int currentVal = GetCurrentStat(statIndex);
            int pending    = _allocated != null ? _allocated[statIndex] : 0;

            // Background row
            UITheme.DrawRect(new Rect(x, y + 2f, w, RowH - 4f),
                new Color(1f, 1f, 1f, 0.04f));

            float labelW = w - SmBtnW - 60f - 8f;

            // Stat name
            GUI.Label(new Rect(x + 4f, y, labelW, RowH),
                StatNames[statIndex],
                UITheme.Bold(UITheme.FontMedium, UITheme.TextAccent));

            // Current score + pending
            string valText = pending > 0
                ? $"{currentVal} (+{pending})"
                : currentVal.ToString();
            Color valColor = pending > 0 ? UITheme.HPHigh : UITheme.TextPrimary;

            GUI.Label(new Rect(x + 4f + labelW, y, 56f, RowH),
                valText,
                UITheme.Bold(UITheme.FontSmall, valColor, TextAnchor.MiddleRight));

            // +1 button — only when points remain
            bool canAdd = _pointsLeft > 0;
            GUI.enabled = canAdd;

            if (GUI.Button(new Rect(x + w - SmBtnW, y + 4f, SmBtnW, RowH - 8f), "+1", UITheme.Button()))
            {
                if (_allocated != null && _pointsLeft > 0)
                {
                    _allocated[statIndex]++;
                    _pointsLeft--;
                }
            }

            GUI.enabled = true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int GetCurrentStat(int index)
        {
            var gm = GameManager.Instance;
            if (gm == null) return 10;

            // Prefer CharacterSheet when available — it has all six scores.
            if (gm.Character != null)
            {
                var ab = gm.Character.BaseAbilities;
                return index switch
                {
                    0 => ab.Strength,
                    1 => ab.Dexterity,
                    2 => ab.Constitution,
                    3 => ab.Intelligence,
                    4 => ab.Wisdom,
                    5 => ab.Charisma,
                    _ => 10
                };
            }

            // PlayerData only carries STR/DEX/CON; INT/WIS/CHA show as 10.
            if (gm.Player != null)
            {
                return index switch
                {
                    0 => gm.Player.Strength,
                    1 => gm.Player.Dexterity,
                    2 => gm.Player.Constitution,
                    _ => 10
                };
            }

            return 10;
        }

        private static void ApplyLevelUp()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Apply to CharacterSheet if present
            if (gm.Character != null && _allocated != null)
            {
                var ab = gm.Character.BaseAbilities;
                if (_allocated[0] > 0) ab.Strength     += _allocated[0];
                if (_allocated[1] > 0) ab.Dexterity    += _allocated[1];
                if (_allocated[2] > 0) ab.Constitution += _allocated[2];
                if (_allocated[3] > 0) ab.Intelligence += _allocated[3];
                if (_allocated[4] > 0) ab.Wisdom       += _allocated[4];
                if (_allocated[5] > 0) ab.Charisma     += _allocated[5];
                gm.Character.BaseAbilities = ab;
                gm.Character.RecalculateEffectiveAbilities();
                gm.Character.RecalculateAC();
            }

            // Apply STR/DEX/CON mirror to PlayerData (INT/WIS/CHA don't exist there)
            if (gm.Player != null && _allocated != null)
            {
                if (_allocated[0] > 0) gm.Player.Strength     += _allocated[0];
                if (_allocated[1] > 0) gm.Player.Dexterity    += _allocated[1];
                if (_allocated[2] > 0) gm.Player.Constitution += _allocated[2];

                // PlayerData.LevelUp() increments Level and adds +5 MaxHP
                gm.Player.LevelUp();
            }

            // Sync CharacterSheet → PlayerData for AC, HP, etc.
            gm.SyncPlayerFromCharacter();

            Debug.Log($"[LevelUpScreen] Level up to {gm.Player?.Level}. Stats applied.");

            // Reset and close
            _allocated  = null;
            _pointsLeft = 0;
            ShouldShow  = false;
        }
    }
}
