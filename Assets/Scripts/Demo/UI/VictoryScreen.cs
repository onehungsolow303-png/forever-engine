using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Full-screen IMGUI overlay shown when the player defeats the final boss
    /// (the "castle_boss" encounter — "The Rot King").
    ///
    /// Usage:
    ///   VictoryScreen.Show()   — call from GameManager.OnBattleComplete when
    ///                            the defeated encounter ID is "castle_boss".
    ///   VictoryScreen.IsShowing — read-only guard flag polled by other systems.
    /// </summary>
    public class VictoryScreen : UnityEngine.MonoBehaviour
    {
        // ── Public interface ─────────────────────────────────────────────────

        /// <summary>True while the victory overlay is displayed.</summary>
        public static bool IsShowing { get; private set; }

        /// <summary>Display the victory screen.</summary>
        public static void Show() => IsShowing = true;

        // ── Layout constants ─────────────────────────────────────────────────

        private const float PanelW = 560f;
        private const float PanelH = 400f;
        private const float BtnW   = 200f;
        private const float BtnH   = 34f;
        private const float Pad    = 20f;

        // ── IMGUI rendering ──────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsShowing) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // Full-screen dark overlay
            UITheme.DrawRect(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.82f));

            // Center panel
            float px = (sw - PanelW) * 0.5f;
            float py = (sh - PanelH) * 0.5f;

            UITheme.DrawPanel(new Rect(px, py, PanelW, PanelH));

            // Inner gold accent border
            UITheme.DrawBorder(new Rect(px + 4f, py + 4f, PanelW - 8f, PanelH - 8f),
                UITheme.TextHeader, 1f);

            float cx = px + Pad;
            float cy = py + Pad;
            float cw = PanelW - Pad * 2f;

            // ── Title ─────────────────────────────────────────────────────────
            GUI.Label(new Rect(cx, cy, cw, 52f),
                "VICTORY",
                UITheme.Header(UITheme.FontHuge));
            cy += 56f;

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 10f;

            // ── Subtitle ──────────────────────────────────────────────────────
            GUI.Label(new Rect(cx, cy, cw, 28f),
                "You have defeated The Rot King",
                UITheme.Header(UITheme.FontTitle));
            cy += 30f;

            GUI.Label(new Rect(cx, cy, cw, 22f),
                "and saved the Shattered Kingdom!",
                UITheme.Label(UITheme.FontMedium, UITheme.TextPrimary, TextAnchor.MiddleCenter));
            cy += 30f;

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 12f;

            // ── Player stats summary ──────────────────────────────────────────
            var gm = GameManager.Instance;
            if (gm?.Player != null)
            {
                var p = gm.Player;

                GUI.Label(new Rect(cx, cy, cw, 22f),
                    "HERO'S JOURNEY",
                    UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent, TextAnchor.MiddleCenter));
                cy += 24f;

                float col = cw / 3f;
                DrawStatCell(cx,           cy, col, "LEVEL",          p.Level.ToString());
                DrawStatCell(cx + col,     cy, col, "GOLD",           $"{p.Gold}g");
                DrawStatCell(cx + col * 2f, cy, col, "DAYS SURVIVED", p.DayCount.ToString());
                cy += 52f;

                DrawStatCell(cx,           cy, col, "HEXES EXPLORED",  p.ExploredHexes.Count.ToString());
                DrawStatCell(cx + col,     cy, col, "LOCATIONS FOUND", p.DiscoveredLocations.Count.ToString());
                DrawStatCell(cx + col * 2f, cy, col, "FINAL HP",      $"{p.HP}/{p.MaxHP}");
                cy += 56f;
            }
            else
            {
                cy += 12f;
            }

            UITheme.DrawSeparator(cx, cy, cw);
            cy += 12f;

            // ── Buttons ───────────────────────────────────────────────────────
            float gap       = 12f;
            float totalBtns = BtnW * 2f + gap;
            float btnStartX = px + (PanelW - totalBtns) * 0.5f;

            if (GUI.Button(new Rect(btnStartX, cy, BtnW, BtnH), "Return to Main Menu", UITheme.Button()))
            {
                IsShowing = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.GameComplete();
                else
                    SceneManager.LoadScene("MainMenu");
            }

            if (GUI.Button(new Rect(btnStartX + BtnW + gap, cy, BtnW, BtnH), "Continue Playing", UITheme.Button()))
            {
                IsShowing = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void DrawStatCell(float x, float y, float w, string label, string value)
        {
            UITheme.DrawPanelLight(new Rect(x + 2f, y, w - 4f, 46f));

            GUI.Label(new Rect(x + 2f, y + 4f, w - 4f, 18f),
                label,
                UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary, TextAnchor.MiddleCenter));

            GUI.Label(new Rect(x + 2f, y + 22f, w - 4f, 20f),
                value,
                UITheme.Bold(UITheme.FontMedium, UITheme.TextHeader, TextAnchor.MiddleCenter));
        }
    }
}
