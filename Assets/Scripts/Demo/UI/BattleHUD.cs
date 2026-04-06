using UnityEngine;
using System.Linq;
using ForeverEngine.Demo.Battle;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Demo.UI
{
    public class BattleHUD : UnityEngine.MonoBehaviour
    {
        private Vector2 _logScroll;

        private void OnGUI()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;

            // Turn info
            GUI.Box(new Rect(10, 10, 200, 80), "");
            GUI.Label(new Rect(20, 15, 180, 20), $"<b>Round {bm.RoundNumber}</b>");
            if (bm.CurrentTurn != null)
                GUI.Label(new Rect(20, 35, 180, 20), $"Turn: {bm.CurrentTurn.Name}");
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer)
                GUI.Label(new Rect(20, 55, 180, 20), $"Moves: {bm.CurrentTurn.MovementRemaining} | {(bm.CurrentTurn.HasAction ? "Action ready" : "Action used")}");

            // Combatant list
            GUI.Box(new Rect(10, 100, 200, 20 + bm.Combatants.Count * 22), "");
            for (int i = 0; i < bm.Combatants.Count; i++)
            {
                var c = bm.Combatants[i];
                string marker = c == bm.CurrentTurn ? ">" : " ";
                Color col = c.IsPlayer ? Color.cyan : (c.IsAlive ? Color.red : Color.gray);
                var style = new GUIStyle(GUI.skin.label) { normal = { textColor = col }, fontSize = 11 };
                GUI.Label(new Rect(20, 105 + i * 22, 180, 20), $"{marker}{c.Name} HP:{c.HP}/{c.MaxHP}", style);
            }

            // Combat log
            int logY = Screen.height - 160;
            GUI.Box(new Rect(10, logY, 350, 150), "Combat Log");
            _logScroll = GUI.BeginScrollView(new Rect(10, logY + 20, 350, 130), _logScroll, new Rect(0, 0, 320, bm.Log.Count * 18));
            for (int i = 0; i < bm.Log.Count; i++)
                GUI.Label(new Rect(5, i * 18, 320, 18), bm.Log[i]);
            GUI.EndScrollView();

            // Player controls
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer && !bm.BattleOver)
            {
                var pc = bm.CurrentTurn;

                // Death save display (overrides normal controls)
                if (pc.HP <= 0 && pc.DeathSaves != null && pc.DeathSaves.IsActive)
                {
                    DrawDeathSavePips(pc.DeathSaves);
                }
                else
                {
                    // Controls hint
                    bool hasSpells = pc.Sheet != null && pc.Sheet.PreparedSpells.Count > 0;
                    string controls = hasSpells
                        ? "WASD: Move | F: Attack | Q: Spells | Space: End Turn"
                        : "WASD: Move | F: Attack | Space: End Turn";
                    GUI.Label(new Rect(Screen.width/2 - 220, Screen.height - 30, 440, 20), controls);

                    // Spell menu overlay
                    if (bm.IsSpellMenuOpen && bm.AvailableSpells != null)
                    {
                        DrawSpellMenu(bm, pc);
                    }
                }
            }

            // Active conditions on player
            DrawConditions(bm);

            // Battle over
            if (bm.BattleOver)
            {
                GUI.Box(new Rect(Screen.width/2 - 100, Screen.height/2 - 40, 200, 80), "");
                GUI.Label(new Rect(Screen.width/2 - 80, Screen.height/2 - 30, 160, 20), bm.PlayerWon ? "<b>Victory!</b>" : "<b>Defeated...</b>");
                if (bm.PlayerWon)
                    GUI.Label(new Rect(Screen.width/2 - 80, Screen.height/2 - 10, 160, 20), $"Gold: +{GameManager.Instance?.LastBattleGoldEarned ?? 0}");
                if (GUI.Button(new Rect(Screen.width/2 - 50, Screen.height/2 + 15, 100, 25), "Continue"))
                    bm.EndBattle();
            }
        }

        private void DrawDeathSavePips(DeathSaveTracker ds)
        {
            float cx = Screen.width / 2;
            float cy = Screen.height / 2 + 60;
            GUI.Box(new Rect(cx - 120, cy, 240, 60), "");

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(cx - 110, cy + 5, 220, 20), "DEATH SAVES", headerStyle);

            // Success pips
            string successes = "";
            for (int i = 0; i < 3; i++)
                successes += i < ds.Successes ? "\u25CF " : "\u25CB "; // filled / empty circles
            var successStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.green }, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(cx - 110, cy + 25, 110, 20), $"Pass: {successes}", successStyle);

            // Failure pips
            string failures = "";
            for (int i = 0; i < 3; i++)
                failures += i < ds.Failures ? "\u25CF " : "\u25CB ";
            var failStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.red }, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(cx, cy + 25, 110, 20), $"Fail: {failures}", failStyle);
        }

        private void DrawSpellMenu(BattleManager bm, BattleCombatant pc)
        {
            var spells = bm.AvailableSpells;
            float menuW = 320, menuH = 30 + spells.Count * 22;
            float mx = Screen.width / 2 - menuW / 2;
            float my = Screen.height - 60 - menuH;
            GUI.Box(new Rect(mx, my, menuW, menuH), "");

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(mx + 10, my + 5, 300, 20), "Prepared Spells (press 1-9 to cast, Q to close)", headerStyle);

            var slots = pc.Sheet?.SpellSlots;
            for (int i = 0; i < spells.Count && i < 9; i++)
            {
                var spell = spells[i];
                string lvlStr = spell.IsCantrip ? "Cantrip" : $"L{spell.Level}";
                string dmgStr = spell.DamageDiceCount > 0 ? $" {spell.GetDamage()} {spell.DamageType}" : "";
                string healStr = spell.HealingDiceCount > 0 ? $" Heal {spell.GetHealing()}" : "";
                string concStr = spell.Concentration ? " [C]" : "";

                bool canCast = spell.IsCantrip || (slots != null && slots.CanCast(spell, spell.Level));
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = canCast ? Color.white : Color.gray }
                };

                GUI.Label(new Rect(mx + 10, my + 25 + i * 22, 300, 20),
                    $"{i + 1}. {spell.Name} ({lvlStr}){dmgStr}{healStr}{concStr}", style);
            }

            // Spell slot summary
            if (slots != null)
            {
                string slotInfo = "";
                for (int i = 0; i < 9; i++)
                {
                    if (slots.MaxSlots[i] > 0)
                    {
                        if (slotInfo.Length > 0) slotInfo += " | ";
                        slotInfo += $"L{i+1}: {slots.AvailableSlots[i]}/{slots.MaxSlots[i]}";
                    }
                }
                if (slotInfo.Length > 0)
                {
                    var slotStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.5f, 0.7f, 1f) } };
                    GUI.Label(new Rect(mx + 10, my + menuH - 18, 300, 16), slotInfo, slotStyle);
                }
            }
        }

        private void DrawConditions(BattleManager bm)
        {
            // Show active conditions on player and current target
            var player = bm.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player != null && player.Conditions != null && player.Conditions.ActiveFlags != Condition.None)
            {
                var condStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.yellow } };
                GUI.Label(new Rect(220, 15, 200, 18), $"Conditions: {player.Conditions.ActiveFlags}", condStyle);
            }

            // Show conditions on currently targeted enemy (current turn if enemy)
            if (bm.CurrentTurn != null && !bm.CurrentTurn.IsPlayer && bm.CurrentTurn.IsAlive)
            {
                var enemy = bm.CurrentTurn;
                if (enemy.Conditions != null && enemy.Conditions.ActiveFlags != Condition.None)
                {
                    var condStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(1f, 0.6f, 0.3f) } };
                    GUI.Label(new Rect(220, 35, 200, 18), $"{enemy.Name}: {enemy.Conditions.ActiveFlags}", condStyle);
                }
            }

            // Show player class/level in combatant list area
            if (player?.Sheet != null)
            {
                var clsStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.7f, 0.9f, 1f) } };
                string cls = RPGBridge.GetClassName(player.Sheet);
                GUI.Label(new Rect(220, 55, 200, 18), $"{player.Sheet.Species?.Name} {cls} Lv{player.Sheet.TotalLevel}", clsStyle);
            }
        }
    }
}
