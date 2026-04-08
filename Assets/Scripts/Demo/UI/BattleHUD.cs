using UnityEngine;
using System.Linq;
using ForeverEngine.Demo.Battle;
using ForeverEngine.ECS.Data;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Demo.UI
{
    public class BattleHUD : UnityEngine.MonoBehaviour
    {
        private Vector2 _logScroll;
        // Cached styles — built once on first OnGUI then reused. GUIStyle
        // construction inside OnGUI per frame is the standard hot-loop
        // GC trap; this avoids it for the action panel.
        private GUIStyle _actionLabelEnabled;
        private GUIStyle _actionLabelDisabled;
        private GUIStyle _actionLabelHeader;
        private GUIStyle _hpBarLabel;

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
                    // Right-side action panel showing every combat option as
                    // a labelled key binding. Replaces the previous tiny
                    // bottom-of-screen hint that the user reported "I can't
                    // tell what I can do in combat".
                    DrawActionPanel(bm, pc);

                    // Player HP/AC overlay top-center, oversized so the
                    // player can read their current status mid-combat
                    // without squinting at the small left-side panel.
                    DrawPlayerStatusBar(pc);

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

        /// <summary>
        /// Right-side panel listing every combat action as a labelled key
        /// binding, with disabled actions greyed out so the player knows
        /// at a glance what's available this turn. Replaces the old
        /// 30-pixel-tall hint at the bottom of the screen.
        /// </summary>
        private void DrawActionPanel(BattleManager bm, BattleCombatant pc)
        {
            EnsureActionStyles();

            // Has-action gates: most options consume the player's action
            // for the turn, so they're disabled once HasAction is false.
            bool hasAction = pc.HasAction;
            bool canMove   = pc.MovementRemaining > 0;
            bool hasSpells = pc.Sheet != null && pc.Sheet.PreparedSpells.Count > 0;
            int potionCount = GetItemCount(GameManager.Instance?.Player?.Inventory,
                                           ItemIds.HealthPotion);
            bool canHeal = hasAction && potionCount > 0;

            // Adjacent enemy check — F only does something when there's
            // an enemy in melee range. Greyed out otherwise so the player
            // knows to move first.
            bool adjacentEnemy = bm.Combatants.Any(c =>
                c != null && c.IsAlive && !c.IsPlayer
                && System.Math.Abs(c.X - pc.X) + System.Math.Abs(c.Y - pc.Y) == 1);
            bool canAttack = hasAction && adjacentEnemy;

            // Layout: anchored to the right edge, vertical list of rows.
            const float panelW = 200;
            const float rowH = 26;
            const int rowCount = 6; // Move, Attack, Spell, Heal, End, hint
            const float headerH = 28;
            float panelH = headerH + rowCount * rowH + 16;
            float px = Screen.width - panelW - 12;
            float py = 100;

            GUI.Box(new Rect(px, py, panelW, panelH), "");
            GUI.Label(new Rect(px + 10, py + 6, panelW - 20, 22),
                      "ACTIONS", _actionLabelHeader);

            float ry = py + headerH;
            DrawActionRow(px + 10, ry, panelW - 20, "[WASD]", "Move",
                          canMove ? $"({pc.MovementRemaining} left)" : "(no moves)",
                          canMove);
            ry += rowH;

            DrawActionRow(px + 10, ry, panelW - 20, "[F]", "Attack",
                          canAttack ? "" : (adjacentEnemy ? "(action used)" : "(none adjacent)"),
                          canAttack);
            ry += rowH;

            DrawActionRow(px + 10, ry, panelW - 20, "[Q]", "Cast Spell",
                          hasSpells ? (hasAction ? "" : "(action used)") : "(no spells)",
                          hasSpells && hasAction);
            ry += rowH;

            DrawActionRow(px + 10, ry, panelW - 20, "[H]", "Heal Potion",
                          potionCount > 0 ? $"({potionCount} left)" : "(none)",
                          canHeal);
            ry += rowH;

            DrawActionRow(px + 10, ry, panelW - 20, "[Space]", "End Turn", "", true);
            ry += rowH;

            // Subtle hint row
            var hint = pc.HasAction
                ? "You have an action."
                : "Action used — end turn or move.";
            GUI.Label(new Rect(px + 10, ry, panelW - 20, 20), hint, _actionLabelDisabled);
        }

        /// <summary>
        /// One row in the action panel: [Key] Name (status). Greyed out
        /// when disabled so the player can scan availability quickly.
        /// </summary>
        private void DrawActionRow(float x, float y, float w, string key, string name, string status, bool enabled)
        {
            var style = enabled ? _actionLabelEnabled : _actionLabelDisabled;
            // Two-column layout: key tag on the left, name + status on the right
            GUI.Label(new Rect(x, y, 60, 22), key, style);
            string label = string.IsNullOrEmpty(status) ? name : $"{name} {status}";
            GUI.Label(new Rect(x + 55, y, w - 55, 22), label, style);
        }

        /// <summary>
        /// Top-center HP/AC summary, oversized for visibility during the
        /// chaos of combat. Color-graded HP bar so the player can spot
        /// "I'm in trouble" without doing math.
        /// </summary>
        private void DrawPlayerStatusBar(BattleCombatant pc)
        {
            float barW = 280;
            float barH = 26;
            float bx = Screen.width / 2 - barW / 2;
            float by = 16;

            GUI.Box(new Rect(bx - 4, by - 4, barW + 8, barH + 8), "");

            // HP bar fill
            float pct = pc.MaxHP > 0 ? Mathf.Clamp01((float)pc.HP / pc.MaxHP) : 0;
            Color fill = pct > 0.6f ? new Color(0.2f, 0.7f, 0.25f)
                       : pct > 0.3f ? new Color(0.85f, 0.7f, 0.15f)
                                    : new Color(0.85f, 0.2f, 0.2f);
            var bgColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(new Rect(bx, by, barW, barH), Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(bx, by, barW * pct, barH), Texture2D.whiteTexture);
            GUI.color = bgColor;

            // Label overlay
            EnsureActionStyles();
            string label = $"HP {pc.HP} / {pc.MaxHP}    AC {pc.AC}";
            GUI.Label(new Rect(bx, by + 3, barW, barH), label, _hpBarLabel);
        }

        private void EnsureActionStyles()
        {
            if (_actionLabelEnabled != null) return;
            _actionLabelEnabled = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.95f, 0.95f, 0.95f) }
            };
            _actionLabelDisabled = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.5f, 0.5f, 0.55f) }
            };
            _actionLabelHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) },
                alignment = TextAnchor.MiddleCenter
            };
            _hpBarLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static int GetItemCount(Inventory inv, int itemId)
        {
            if (inv == null) return 0;
            int total = 0;
            foreach (var slot in inv.GetAllSlots())
                if (slot.ItemId == itemId) total += slot.StackCount;
            return total;
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
