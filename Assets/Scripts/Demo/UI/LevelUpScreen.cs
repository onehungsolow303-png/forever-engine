using UnityEngine;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Centered IMGUI panel that appears when the player gains enough XP to level up.
    ///
    /// The server handles all stat mutations for level-ups. This screen is display-only:
    /// it shows the server-applied new stats and lets the player acknowledge the level-up.
    /// The server applies the ability score improvements; we merely show the result.
    ///
    /// Usage:
    ///   LevelUpScreen.Show()  — call after the server's level-up StatUpdate is received.
    ///   LevelUpScreen.ShouldShow — guard for OnGUI if you ever need to poll.
    /// </summary>
    public class LevelUpScreen : UnityEngine.MonoBehaviour
    {
        // ── Public interface ─────────────────────────────────────────────────

        /// <summary>True while the level-up panel is awaiting player acknowledgement.</summary>
        public static bool ShouldShow { get; private set; }

        /// <summary>Call after the server has applied the level-up and pushed a StatUpdate.</summary>
        public static void Show()
        {
            ShouldShow = true;
            var cache  = ServerStateCache.Instance;
            _newLevel  = cache != null ? cache.Level : 0;
        }

        // ── Layout constants ─────────────────────────────────────────────────

        private const float PanelW = 500f;
        private const float PanelH = 420f;
        private const float BtnW   = 180f;
        private const float BtnH   = 28f;
        private const float RowH   = 32f;
        private const float Pad    = 16f;

        // ── Stat-button labels ───────────────────────────────────────────────

        private static readonly string[] StatNames = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };

        // ── State ────────────────────────────────────────────────────────────

        private static int _newLevel;

        // ── IMGUI rendering ──────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!ShouldShow) return;

            var cache = ServerStateCache.Instance;
            if (cache == null) return;

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
                $"+5 Maximum HP gained  (now {cache.HP}/{cache.MaxHP})",
                UITheme.Label(UITheme.FontMedium, UITheme.HPHigh, TextAnchor.MiddleCenter));
            cy += 28f;

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 10f;

            // ── Ability score display ─────────────────────────────────────────
            GUI.Label(new Rect(cx, cy, cw, 22f),
                "Current Ability Scores:",
                UITheme.Bold(UITheme.FontMedium, UITheme.TextAccent, TextAnchor.MiddleCenter));
            cy += 28f;

            // Two-column 3-per-row grid (display-only)
            float colW    = (cw - Pad) * 0.5f;
            float gridTop = cy;

            for (int i = 0; i < 6; i++)
            {
                int col = i % 2;
                int row = i / 2;

                float rx = cx + col * (colW + Pad);
                float ry = gridTop + row * RowH;

                DrawStatRow(rx, ry, colW, i, cache);
            }

            cy = gridTop + 3 * RowH + 10f;

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 10f;

            // ── Acknowledge button ────────────────────────────────────────────
            if (GUI.Button(new Rect(px + (PanelW - BtnW) * 0.5f, cy, BtnW, BtnH + 4f),
                "Continue", UITheme.Button()))
            {
                ShouldShow = false;
                Debug.Log($"[LevelUpScreen] Player acknowledged level up to {_newLevel}.");
            }
        }

        // ── Stat row (display-only) ───────────────────────────────────────────

        private static void DrawStatRow(float x, float y, float w, int statIndex,
                                        ServerStateCache cache)
        {
            int currentVal = GetStatFromCache(statIndex, cache);

            // Background row
            UITheme.DrawRect(new Rect(x, y + 2f, w, RowH - 4f),
                new Color(1f, 1f, 1f, 0.04f));

            float labelW = w - 60f - 8f;

            // Stat name
            GUI.Label(new Rect(x + 4f, y, labelW, RowH),
                StatNames[statIndex],
                UITheme.Bold(UITheme.FontMedium, UITheme.TextAccent));

            // Current score
            GUI.Label(new Rect(x + 4f + labelW, y, 56f, RowH),
                currentVal.ToString(),
                UITheme.Bold(UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleRight));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int GetStatFromCache(int index, ServerStateCache cache)
        {
            if (cache?.CharacterSheet == null) return 10;

            var ab = cache.CharacterSheet;
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
    }
}
