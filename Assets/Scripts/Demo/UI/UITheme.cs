using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Shared dark-fantasy UI theme for all IMGUI panels. Provides consistent
    /// colors, styles, and drawing helpers across BattleHUD,
    /// LootScreen, DemoMainMenu, and DungeonMinimap.
    /// </summary>
    public static class UITheme
    {
        // ── Palette ──────────────────────────────────────────────────────────

        // Panel backgrounds
        public static readonly Color PanelBg       = new(0.08f, 0.07f, 0.10f, 0.92f);
        public static readonly Color PanelBgLight   = new(0.12f, 0.11f, 0.14f, 0.88f);
        public static readonly Color PanelBorder    = new(0.55f, 0.42f, 0.18f, 0.6f);

        // Text
        public static readonly Color TextPrimary    = new(0.90f, 0.87f, 0.78f); // Parchment
        public static readonly Color TextSecondary   = new(0.60f, 0.58f, 0.52f);
        public static readonly Color TextHeader      = new(1f, 0.82f, 0.35f);    // Gold
        public static readonly Color TextAccent      = new(0.75f, 0.55f, 0.20f); // Amber

        // Health bar gradient
        public static readonly Color HPHigh         = new(0.18f, 0.65f, 0.22f);
        public static readonly Color HPMid          = new(0.85f, 0.68f, 0.12f);
        public static readonly Color HPLow          = new(0.78f, 0.18f, 0.15f);
        public static readonly Color HPBackground   = new(0.15f, 0.12f, 0.12f, 0.9f);

        // Functional
        public static readonly Color FriendlyBlue   = new(0.35f, 0.60f, 0.95f);
        public static readonly Color EnemyRed       = new(0.85f, 0.22f, 0.18f);
        public static readonly Color DisabledGray   = new(0.40f, 0.38f, 0.38f);
        public static readonly Color ManaBlue       = new(0.30f, 0.45f, 0.85f);
        public static readonly Color XPGold         = new(0.95f, 0.85f, 0.30f);

        // ── Sizes ────────────────────────────────────────────────────────────

        public const int FontTiny     = 10;
        public const int FontSmall    = 11;
        public const int FontNormal   = 12;
        public const int FontMedium   = 14;
        public const int FontLarge    = 16;
        public const int FontTitle    = 22;
        public const int FontHuge     = 36;

        public const float BorderWidth = 1.5f;
        public const float PanelPad    = 10f;

        // ── Cached texture ───────────────────────────────────────────────────

        private static Texture2D _whiteTex;
        public static Texture2D WhiteTex
        {
            get
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                }
                return _whiteTex;
            }
        }

        // ── Style builders ───────────────────────────────────────────────────

        private static GUIStyle _cachedLabel;
        private static GUIStyle _cachedBold;
        private static GUIStyle _cachedHeader;
        private static GUIStyle _cachedButton;

        public static GUIStyle Label(int size = FontNormal, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            // Reuse cached base style, override per-call
            if (_cachedLabel == null)
                _cachedLabel = new GUIStyle(GUI.skin.label);

            _cachedLabel.fontSize = size;
            _cachedLabel.fontStyle = style;
            _cachedLabel.alignment = align;
            _cachedLabel.normal.textColor = color ?? TextPrimary;
            return _cachedLabel;
        }

        public static GUIStyle Header(int size = FontMedium)
        {
            if (_cachedHeader == null)
                _cachedHeader = new GUIStyle(GUI.skin.label);

            _cachedHeader.fontSize = size;
            _cachedHeader.fontStyle = FontStyle.Bold;
            _cachedHeader.alignment = TextAnchor.MiddleCenter;
            _cachedHeader.normal.textColor = TextHeader;
            return _cachedHeader;
        }

        public static GUIStyle Bold(int size = FontNormal, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (_cachedBold == null)
                _cachedBold = new GUIStyle(GUI.skin.label);

            _cachedBold.fontSize = size;
            _cachedBold.fontStyle = FontStyle.Bold;
            _cachedBold.alignment = align;
            _cachedBold.normal.textColor = color ?? TextPrimary;
            return _cachedBold;
        }

        public static GUIStyle Button()
        {
            if (_cachedButton == null)
            {
                _cachedButton = new GUIStyle(GUI.skin.button)
                {
                    fontSize = FontNormal,
                    fontStyle = FontStyle.Bold,
                };
                _cachedButton.normal.textColor = TextPrimary;
                _cachedButton.hover.textColor = TextHeader;
            }
            return _cachedButton;
        }

        // ── Drawing helpers ──────────────────────────────────────────────────

        /// <summary>Draw a filled rectangle.</summary>
        public static void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, WhiteTex);
            GUI.color = prev;
        }

        /// <summary>Draw a panel background with border.</summary>
        public static void DrawPanel(Rect rect, bool showBorder = true)
        {
            DrawRect(rect, PanelBg);
            if (showBorder)
                DrawBorder(rect, PanelBorder, BorderWidth);
        }

        /// <summary>Draw a lighter panel variant for nested elements.</summary>
        public static void DrawPanelLight(Rect rect)
        {
            DrawRect(rect, PanelBgLight);
            DrawBorder(rect, PanelBorder, BorderWidth);
        }

        /// <summary>Draw a rectangular border (outline only).</summary>
        public static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>Draw a horizontal bar (HP, XP, hunger, etc.) with label overlay.</summary>
        public static void DrawBar(Rect rect, float percent, Color fillColor, string label)
        {
            DrawRect(rect, HPBackground);
            float fill = Mathf.Clamp01(percent);
            DrawRect(new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * fill, rect.height - 2), fillColor);
            DrawBorder(rect, new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, 0.3f), 1f);
            GUI.Label(rect, label, Label(FontSmall, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold));
        }

        /// <summary>Get HP bar color based on percentage.</summary>
        public static Color HPColor(float percent)
        {
            return percent > 0.6f ? HPHigh : percent > 0.3f ? HPMid : HPLow;
        }

        /// <summary>Draw a separator line.</summary>
        public static void DrawSeparator(float x, float y, float width)
        {
            DrawRect(new Rect(x, y, width, 1f), new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, 0.3f));
        }
    }
}
