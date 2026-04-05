using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class OverworldHUD : UnityEngine.MonoBehaviour
    {
        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            var p = gm.Player;
            var ow = Overworld.OverworldManager.Instance;

            // Top-left: Stats
            GUI.Box(new Rect(10, 10, 200, 130), "");
            GUI.Label(new Rect(20, 15, 180, 20), $"<b>The Wanderer</b> (Lv.{p.Level})");
            DrawBar(new Rect(20, 40, 170, 16), p.HPPercent, Color.red, $"HP: {p.HP}/{p.MaxHP}");
            DrawBar(new Rect(20, 60, 170, 16), p.HungerPercent, new Color(0.8f, 0.5f, 0.2f), $"Hunger: {p.Hunger:F0}/{p.MaxHunger}");
            DrawBar(new Rect(20, 80, 170, 16), p.ThirstPercent, Color.cyan, $"Thirst: {p.Thirst:F0}/{p.MaxThirst}");
            GUI.Label(new Rect(20, 100, 180, 20), $"Gold: {p.Gold}  |  AC: {p.AC}");
            GUI.Label(new Rect(20, 118, 180, 20), $"Day {p.DayCount}  |  {(ow != null && ow.IsNight ? "Night" : "Day")}");

            // Top-right: Quest tracker
            var qs = ForeverEngine.ECS.Systems.QuestSystem.Instance;
            if (qs != null)
            {
                var active = qs.GetActiveQuests();
                if (active.Count > 0)
                {
                    var quest = active[0];
                    GUI.Box(new Rect(Screen.width - 250, 10, 240, 60), "");
                    GUI.Label(new Rect(Screen.width - 240, 15, 220, 20), $"<b>{quest.Definition.Title}</b>");
                    foreach (var obj in quest.Definition.Objectives)
                    {
                        int prog = quest.GetObjectiveProgress(obj.Id);
                        GUI.Label(new Rect(Screen.width - 240, 35, 220, 20), $"  {obj.Description}: {prog}/{obj.RequiredCount}");
                    }
                }
            }

            // Bottom: Controls
            GUI.Label(new Rect(10, Screen.height - 30, 500, 20), "WASD/QE: Move | F: Forage | I: Inventory | Enter: Interact with location | Esc: Pause");

            // Center: Location prompt
            if (ow != null)
            {
                foreach (var loc in LocationData.GetAll())
                {
                    if (loc.HexQ == p.HexQ && loc.HexR == p.HexR)
                    {
                        GUI.Box(new Rect(Screen.width/2 - 120, Screen.height/2 - 40, 240, 50), "");
                        GUI.Label(new Rect(Screen.width/2 - 110, Screen.height/2 - 35, 220, 20), $"<b>{loc.Name}</b>");
                        GUI.Label(new Rect(Screen.width/2 - 110, Screen.height/2 - 15, 220, 20), "Press Enter to enter");
                        break;
                    }
                }
            }
        }

        private void DrawBar(Rect rect, float percent, Color color, string label)
        {
            GUI.Box(rect, "");
            var fillRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * Mathf.Clamp01(percent), rect.height - 2);
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            GUI.Label(rect, label, style);
        }
    }
}
