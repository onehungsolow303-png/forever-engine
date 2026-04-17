using UnityEngine;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.UI
{
    public class OverworldHUD : UnityEngine.MonoBehaviour
    {
        private void OnGUI()
        {
            // Suppress HUD while the pause menu is open so it doesn't draw underneath.
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) return;

            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            var gm = GameManager.Instance;
            var ow = Overworld.OverworldManager.Instance;

            // Suppress the entire IMGUI overworld HUD while a modal dialogue
            // is open. IMGUI renders LAST in the frame so it draws on top of
            // every UI Toolkit panel including DialoguePanel - the location
            // prompt was overlapping Old Garth's response text on first
            // playtest, blocking conversation readability.
            if (DialoguePanel.Instance != null && DialoguePanel.Instance.IsOpen)
                return;

            // Top-left: Stats
            var sheet = gm?.Character;
            string charTitle;
            if (sheet != null)
            {
                string species = sheet.Species != null ? sheet.Species.Name : "";
                string cls = RPGBridge.GetClassName(sheet);
                charTitle = $"{species} {cls} Lv{sheet.TotalLevel}";
            }
            else
            {
                charTitle = $"The Wanderer (Lv.{cache.Level})";
            }

            // Calculate spell slot display
            string spellSlotStr = "";
            if (sheet != null)
            {
                var slots = sheet.SpellSlots;
                for (int i = 0; i < 9; i++)
                {
                    if (slots.MaxSlots[i] > 0)
                    {
                        if (spellSlotStr.Length > 0) spellSlotStr += " | ";
                        spellSlotStr += $"L{i+1}: {slots.AvailableSlots[i]}/{slots.MaxSlots[i]}";
                    }
                }
            }

            int boxHeight = string.IsNullOrEmpty(spellSlotStr) ? 130 : 150;
            var statsRect = new Rect(10, 10, 240, boxHeight);
            UITheme.DrawPanel(statsRect);
            GUI.Label(new Rect(20, 15, 220, 20), charTitle, UITheme.Bold(UITheme.FontNormal, UITheme.TextHeader));
            UITheme.DrawBar(new Rect(20, 40, 210, 16), cache.HPPercent, UITheme.HPColor(cache.HPPercent), $"HP: {cache.HP}/{cache.MaxHP}");
            UITheme.DrawBar(new Rect(20, 60, 210, 16), cache.HungerPercent, new Color(0.8f, 0.5f, 0.2f), $"Hunger: {cache.Hunger:F0}/100");
            UITheme.DrawBar(new Rect(20, 80, 210, 16), cache.ThirstPercent, UITheme.FriendlyBlue, $"Thirst: {cache.Thirst:F0}/100");
            GUI.Label(new Rect(20, 100, 220, 20), $"Gold: {cache.Gold}  |  AC: {cache.AC}", UITheme.Label(UITheme.FontNormal, UITheme.TextAccent));

            // DayCount and day/night: not in ServerStateCache, fall back to local GameManager/OverworldManager
            int dayCount = gm?.Player?.DayCount ?? 0;
            GUI.Label(new Rect(20, 118, 220, 20), $"Day {dayCount}  |  {(ow != null && ow.IsNight ? "Night" : "Day")}", UITheme.Label(UITheme.FontNormal, UITheme.TextAccent));
            if (!string.IsNullOrEmpty(spellSlotStr))
            {
                GUI.Label(new Rect(20, 136, 220, 18), $"Slots: {spellSlotStr}", UITheme.Label(UITheme.FontTiny, UITheme.ManaBlue));
            }

            // Top-right: Quest tracker (from ServerStateCache.ActiveQuests)
            var quests = cache.ActiveQuests;
            if (quests != null && quests.Length > 0)
            {
                var q = quests[0];
                var questRect = new Rect(Screen.width - 250, 10, 240, 60);
                UITheme.DrawPanel(questRect);
                GUI.Label(new Rect(Screen.width - 240, 15, 220, 20), q.Title, UITheme.Bold(UITheme.FontNormal, UITheme.TextHeader));
                if (q.Objectives != null && q.Objectives.Length > 0)
                {
                    var obj = q.Objectives[0];
                    GUI.Label(new Rect(Screen.width - 240, 35, 220, 20), $"  {obj.Description}: {obj.CurrentCount}/{obj.RequiredCount}", UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary));
                }
            }

            // Phase 3 pivot: Demo.AI.DemoAIIntegration archived. The AI status
            // panel will be reintroduced via DirectorClient in a follow-up.

            // Bottom: Controls
            GUI.Label(new Rect(10, Screen.height - 30, 600, 20), "WASD/QE: Move | F: Forage | I: Inventory | Enter: Interact | Esc: Pause | F5: Save | F9: Load", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));

            // Center: Location prompt (uses local player position — not yet in ServerStateCache)
            if (ow != null && gm?.Player != null)
            {
                var p = gm.Player;
                foreach (var loc in LocationData.GetAll())
                {
                    if (loc.HexQ == p.HexQ && loc.HexR == p.HexR)
                    {
                        var promptRect = new Rect(Screen.width / 2 - 120, Screen.height / 2 - 40, 240, 50);
                        UITheme.DrawPanel(promptRect);
                        GUI.Label(new Rect(Screen.width / 2 - 110, Screen.height / 2 - 35, 220, 20), loc.Name, UITheme.Header(UITheme.FontMedium));
                        GUI.Label(new Rect(Screen.width / 2 - 110, Screen.height / 2 - 15, 220, 20), "Press Enter to enter", UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleLeft));
                        break;
                    }
                }
            }
        }
    }
}
