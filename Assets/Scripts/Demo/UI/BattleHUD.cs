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

        private void OnGUI()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;

            // Turn info
            var turnRect = new Rect(10, 10, 200, 80);
            UITheme.DrawPanel(turnRect);
            GUI.Label(new Rect(20, 15, 180, 20), $"Round {bm.RoundNumber}",
                UITheme.Bold(UITheme.FontNormal, UITheme.TextHeader));
            if (bm.CurrentTurn != null)
                GUI.Label(new Rect(20, 35, 180, 20), $"Turn: {bm.CurrentTurn.Name}",
                    UITheme.Label(UITheme.FontNormal, UITheme.TextPrimary));
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer)
                GUI.Label(new Rect(20, 55, 180, 20),
                    $"Moves: {bm.CurrentTurn.MovementRemaining} | {(bm.CurrentTurn.HasAction ? "Action ready" : "Action used")}",
                    UITheme.Label(UITheme.FontNormal, UITheme.TextPrimary));

            // Combatant list
            var combatantRect = new Rect(10, 100, 200, 20 + bm.Combatants.Count * 22);
            UITheme.DrawPanel(combatantRect);
            for (int i = 0; i < bm.Combatants.Count; i++)
            {
                var c = bm.Combatants[i];
                string marker = c == bm.CurrentTurn ? ">" : " ";
                Color col = c.IsPlayer ? UITheme.FriendlyBlue
                    : (c.IsAlive ? UITheme.EnemyRed : UITheme.DisabledGray);
                GUI.Label(new Rect(20, 105 + i * 22, 180, 20),
                    $"{marker}{c.Name} HP:{c.HP}/{c.MaxHP}",
                    UITheme.Label(UITheme.FontSmall, col));
            }

            // Combat log
            int logY = Screen.height - 160;
            var logRect = new Rect(10, logY, 350, 150);
            UITheme.DrawPanel(logRect);
            GUI.Label(new Rect(20, logY + 2, 330, 18), "Combat Log",
                UITheme.Header(UITheme.FontSmall));
            _logScroll = GUI.BeginScrollView(new Rect(10, logY + 20, 350, 130), _logScroll,
                new Rect(0, 0, 320, bm.Log.Count * 18));
            for (int i = 0; i < bm.Log.Count; i++)
                GUI.Label(new Rect(5, i * 18, 320, 18), bm.Log[i],
                    UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary));
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
                    DrawActionPanel(bm, pc);
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
                var overRect = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 40, 200, 80);
                UITheme.DrawPanel(overRect);
                GUI.Label(new Rect(Screen.width / 2 - 80, Screen.height / 2 - 30, 160, 20),
                    bm.PlayerWon ? "Victory!" : "Defeated...",
                    UITheme.Bold(UITheme.FontLarge, UITheme.TextHeader, TextAnchor.MiddleCenter));
                if (bm.PlayerWon)
                    GUI.Label(new Rect(Screen.width / 2 - 80, Screen.height / 2 - 10, 160, 20),
                        $"Gold: +{GameManager.Instance?.LastBattleGoldEarned ?? 0}",
                        UITheme.Label(UITheme.FontNormal, UITheme.XPGold, TextAnchor.MiddleCenter));
                if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2 + 15, 100, 25),
                    new GUIContent("Continue"), UITheme.Button()))
                    bm.EndBattle();
            }
        }

        /// <summary>
        /// Right-side panel listing every combat action as a labelled key
        /// binding, with disabled actions greyed out so the player knows
        /// at a glance what's available this turn.
        /// </summary>
        private void DrawActionPanel(BattleManager bm, BattleCombatant pc)
        {
            bool hasAction = pc.HasAction;
            bool canMove   = pc.MovementRemaining > 0;
            bool hasSpells = pc.Sheet != null && pc.Sheet.PreparedSpells.Count > 0;
            int potionCount = GetItemCount(GameManager.Instance?.Player?.Inventory,
                                           ItemIds.HealthPotion);
            bool canHeal = hasAction && potionCount > 0;

            bool adjacentEnemy = bm.Combatants.Any(c =>
                c != null && c.IsAlive && !c.IsPlayer
                && System.Math.Abs(c.X - pc.X) + System.Math.Abs(c.Y - pc.Y) == 1);
            bool canAttack = hasAction && adjacentEnemy;

            const float panelW = 200;
            const float rowH = 26;
            const int rowCount = 6;
            const float headerH = 28;
            float panelH = headerH + rowCount * rowH + 16;
            float px = Screen.width - panelW - 12;
            float py = 100;

            UITheme.DrawPanel(new Rect(px, py, panelW, panelH));
            GUI.Label(new Rect(px + 10, py + 6, panelW - 20, 22),
                      "ACTIONS", UITheme.Header(UITheme.FontMedium));

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
            GUI.Label(new Rect(px + 10, ry, panelW - 20, 20), hint,
                UITheme.Label(UITheme.FontNormal, UITheme.DisabledGray));
        }

        /// <summary>
        /// One row in the action panel: [Key] Name (status). Greyed out
        /// when disabled so the player can scan availability quickly.
        /// </summary>
        private static void DrawActionRow(float x, float y, float w, string key, string name, string status, bool enabled)
        {
            Color col = enabled ? UITheme.TextPrimary : UITheme.DisabledGray;
            var style = UITheme.Label(UITheme.FontNormal, col);
            GUI.Label(new Rect(x, y, 60, 22), key, style);
            string label = string.IsNullOrEmpty(status) ? name : $"{name} {status}";
            GUI.Label(new Rect(x + 55, y, w - 55, 22), label, style);
        }

        /// <summary>
        /// Top-center HP/AC summary using UITheme.DrawBar for the HP bar
        /// and UITheme.HPColor for color grading.
        /// </summary>
        private static void DrawPlayerStatusBar(BattleCombatant pc)
        {
            float barW = 280;
            float barH = 26;
            float bx = Screen.width / 2 - barW / 2;
            float by = 16;

            UITheme.DrawPanel(new Rect(bx - 4, by - 4, barW + 8, barH + 8));

            float pct = pc.MaxHP > 0 ? Mathf.Clamp01((float)pc.HP / pc.MaxHP) : 0;
            string label = $"HP {pc.HP} / {pc.MaxHP}    AC {pc.AC}";
            UITheme.DrawBar(new Rect(bx, by, barW, barH), pct, UITheme.HPColor(pct), label);
        }

        private static int GetItemCount(Inventory inv, int itemId)
        {
            if (inv == null) return 0;
            int total = 0;
            foreach (var slot in inv.GetAllSlots())
                if (slot.ItemId == itemId) total += slot.StackCount;
            return total;
        }

        private static void DrawDeathSavePips(DeathSaveTracker ds)
        {
            float cx = Screen.width / 2;
            float cy = Screen.height / 2 + 60;
            UITheme.DrawPanel(new Rect(cx - 120, cy, 240, 60));

            GUI.Label(new Rect(cx - 110, cy + 5, 220, 20), "DEATH SAVES",
                UITheme.Header(UITheme.FontMedium));

            // Success pips
            string successes = "";
            for (int i = 0; i < 3; i++)
                successes += i < ds.Successes ? "\u25CF " : "\u25CB ";
            GUI.Label(new Rect(cx - 110, cy + 25, 110, 20), $"Pass: {successes}",
                UITheme.Bold(UITheme.FontMedium, Color.green, TextAnchor.MiddleCenter));

            // Failure pips
            string failures = "";
            for (int i = 0; i < 3; i++)
                failures += i < ds.Failures ? "\u25CF " : "\u25CB ";
            GUI.Label(new Rect(cx, cy + 25, 110, 20), $"Fail: {failures}",
                UITheme.Bold(UITheme.FontMedium, UITheme.EnemyRed, TextAnchor.MiddleCenter));
        }

        private static void DrawSpellMenu(BattleManager bm, BattleCombatant pc)
        {
            var spells = bm.AvailableSpells;
            float menuW = 320, menuH = 30 + spells.Count * 22;
            float mx = Screen.width / 2 - menuW / 2;
            float my = Screen.height - 60 - menuH;
            UITheme.DrawPanel(new Rect(mx, my, menuW, menuH));

            GUI.Label(new Rect(mx + 10, my + 5, 300, 20),
                "Prepared Spells (press 1-9 to cast, Q to close)",
                UITheme.Bold(UITheme.FontNormal, UITheme.TextHeader));

            var slots = pc.Sheet?.SpellSlots;
            for (int i = 0; i < spells.Count && i < 9; i++)
            {
                var spell = spells[i];
                string lvlStr = spell.IsCantrip ? "Cantrip" : $"L{spell.Level}";
                string dmgStr = spell.DamageDiceCount > 0 ? $" {spell.GetDamage()} {spell.DamageType}" : "";
                string healStr = spell.HealingDiceCount > 0 ? $" Heal {spell.GetHealing()}" : "";
                string concStr = spell.Concentration ? " [C]" : "";

                bool canCast = spell.IsCantrip || (slots != null && slots.CanCast(spell, spell.Level));
                Color col = canCast ? UITheme.TextPrimary : UITheme.DisabledGray;

                GUI.Label(new Rect(mx + 10, my + 25 + i * 22, 300, 20),
                    $"{i + 1}. {spell.Name} ({lvlStr}){dmgStr}{healStr}{concStr}",
                    UITheme.Label(UITheme.FontSmall, col));
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
                        slotInfo += $"L{i + 1}: {slots.AvailableSlots[i]}/{slots.MaxSlots[i]}";
                    }
                }
                if (slotInfo.Length > 0)
                {
                    GUI.Label(new Rect(mx + 10, my + menuH - 18, 300, 16), slotInfo,
                        UITheme.Label(UITheme.FontTiny, UITheme.ManaBlue));
                }
            }
        }

        private static void DrawConditions(BattleManager bm)
        {
            // Show active conditions on player
            var player = bm.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player != null && player.Conditions != null && player.Conditions.ActiveFlags != Condition.None)
            {
                GUI.Label(new Rect(220, 15, 200, 18),
                    $"Conditions: {player.Conditions.ActiveFlags}",
                    UITheme.Label(UITheme.FontTiny, UITheme.XPGold));
            }

            // Show conditions on currently targeted enemy (current turn if enemy)
            if (bm.CurrentTurn != null && !bm.CurrentTurn.IsPlayer && bm.CurrentTurn.IsAlive)
            {
                var enemy = bm.CurrentTurn;
                if (enemy.Conditions != null && enemy.Conditions.ActiveFlags != Condition.None)
                {
                    GUI.Label(new Rect(220, 35, 200, 18),
                        $"{enemy.Name}: {enemy.Conditions.ActiveFlags}",
                        UITheme.Label(UITheme.FontTiny, UITheme.TextAccent));
                }
            }

            // Show player class/level in combatant list area
            if (player?.Sheet != null)
            {
                string cls = RPGBridge.GetClassName(player.Sheet);
                GUI.Label(new Rect(220, 55, 200, 18),
                    $"{player.Sheet.Species?.Name} {cls} Lv{player.Sheet.TotalLevel}",
                    UITheme.Label(UITheme.FontTiny, UITheme.FriendlyBlue));
            }
        }
    }
}
