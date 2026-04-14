using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class LootScreen : UnityEngine.MonoBehaviour
    {
        public static bool Show;
        public static int GoldEarned;
        public static int XPEarned;
        public static string[] ItemsFound;

        private void OnGUI()
        {
            if (!Show) return;

            float panelW = 260, panelH = 200;
            float px = Screen.width / 2 - panelW / 2;
            float py = Screen.height / 2 - panelH / 2;
            var panelRect = new Rect(px, py, panelW, panelH);

            UITheme.DrawPanel(panelRect);

            // Header
            GUI.Label(new Rect(px, py + 8, panelW, 25), "Loot", UITheme.Header(UITheme.FontLarge));

            // Separator between header and content
            UITheme.DrawSeparator(px + UITheme.PanelPad, py + 38, panelW - UITheme.PanelPad * 2);

            // Gold and XP
            float contentX = px + 20;
            float contentY = py + 48;
            GUI.Label(new Rect(contentX, contentY, 200, 20), $"Gold: +{GoldEarned}",
                UITheme.Label(UITheme.FontNormal, UITheme.TextAccent));
            GUI.Label(new Rect(contentX, contentY + 22, 200, 20), $"XP: +{XPEarned}",
                UITheme.Label(UITheme.FontNormal, UITheme.XPGold));

            // Item list
            if (ItemsFound != null)
            {
                for (int i = 0; i < ItemsFound.Length && i < 4; i++)
                    GUI.Label(new Rect(contentX, contentY + 48 + i * 20, 200, 20),
                        $"  {ItemsFound[i]}", UITheme.Label(UITheme.FontNormal, UITheme.TextPrimary));
            }

            // Continue button
            if (GUI.Button(new Rect(px + panelW / 2 - 50, py + panelH - 40, 100, 30),
                    "Continue", UITheme.Button()))
            {
                Show = false;
            }
        }
    }
}
