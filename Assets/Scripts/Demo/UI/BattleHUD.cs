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

        private const float BarH = 56f;
        private const float BtnW = 70f;
        private const float BtnH = 44f;
        private const float BtnGap = 4f;
        private const float PipH = 20f;
        private const float PortraitSize = 42f;
        private const float PortraitGap = 6f;
        private const float StripH = 56f;
        private const float TooltipW = 180f;
        private const float TooltipH = 70f;

        private void OnGUI()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) return;

            DrawTurnOrderStrip(bm);
            DrawPlayerStatusBar(bm);
            DrawCombatLog(bm);
            DrawConditions(bm);

            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer && !bm.BattleOver)
            {
                var pc = bm.CurrentTurn;
                if (pc.HP <= 0 && pc.DeathSaves != null && pc.DeathSaves.IsActive)
                {
                    DrawDeathSavePips(pc.DeathSaves);
                }
                else
                {
                    DrawResourcePips(pc);
                    DrawActionBar(bm, pc);
                    DrawHoverTooltip(bm, pc);
                }
            }

            if (bm.BattleOver) DrawBattleOver(bm);
        }

        private void DrawTurnOrderStrip(BattleManager bm)
        {
            int count = bm.Combatants.Count;
            float totalW = count * (PortraitSize + PortraitGap) - PortraitGap;
            float startX = Screen.width / 2f - totalW / 2f;

            UITheme.DrawPanel(new Rect(startX - 8, 4, totalW + 16, StripH));

            for (int i = 0; i < count; i++)
            {
                var c = bm.Combatants[i];
                float px = startX + i * (PortraitSize + PortraitGap);
                float py = 10;

                bool isCurrent = c == bm.CurrentTurn;
                Color bg = c.IsPlayer ? UITheme.FriendlyBlue : UITheme.EnemyRed;
                if (!c.IsAlive) bg = UITheme.DisabledGray;

                if (isCurrent)
                    UITheme.DrawRect(new Rect(px - 2, py - 2, PortraitSize + 4, PortraitSize + 4), UITheme.TextHeader);

                UITheme.DrawRect(new Rect(px, py, PortraitSize, PortraitSize), bg * 0.7f);

                string initial = c.Name.Length > 0 ? c.Name.Substring(0, System.Math.Min(2, c.Name.Length)).ToUpper() : "?";
                GUI.Label(new Rect(px, py + 2, PortraitSize, 20), initial,
                    UITheme.Bold(UITheme.FontMedium, Color.white, TextAnchor.MiddleCenter));

                if (c.MaxHP > 0)
                {
                    float hpPct = Mathf.Clamp01((float)c.HP / c.MaxHP);
                    float barW = PortraitSize - 4;
                    UITheme.DrawRect(new Rect(px + 2, py + PortraitSize - 8, barW, 6), new Color(0.2f, 0.1f, 0.1f));
                    UITheme.DrawRect(new Rect(px + 2, py + PortraitSize - 8, barW * hpPct, 6), UITheme.HPColor(hpPct));
                }

                GUI.Label(new Rect(px, py + PortraitSize - 2, PortraitSize, 14),
                    $"{c.InitiativeRoll}",
                    UITheme.Label(UITheme.FontTiny, UITheme.DisabledGray, TextAnchor.MiddleCenter));
            }
        }

        private void DrawResourcePips(BattleCombatant pc)
        {
            float barTotalW = 8 * (BtnW + BtnGap) - BtnGap;
            float bx = Screen.width / 2f - barTotalW / 2f;
            float py = Screen.height - BarH - PipH - 12;

            UITheme.DrawPanel(new Rect(bx - 4, py - 2, barTotalW + 8, PipH + 4));

            float cx = bx;

            float moveBarW = 120f;
            float movePct = pc.Speed > 0 ? Mathf.Clamp01((float)pc.MovementRemaining / pc.Speed) : 0;
            GUI.Label(new Rect(cx, py, 40, PipH), "Move:", UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));
            UITheme.DrawBar(new Rect(cx + 42, py + 3, moveBarW, PipH - 6), movePct,
                new Color(0.9f, 0.6f, 0.1f), $"{pc.MovementRemaining}/{pc.Speed}");
            cx += moveBarW + 56;

            Color actionCol = pc.HasAction ? new Color(0.2f, 0.75f, 0.3f) : UITheme.DisabledGray;
            UITheme.DrawRect(new Rect(cx, py + 3, PipH - 6, PipH - 6), actionCol);
            GUI.Label(new Rect(cx + PipH - 2, py, 50, PipH), "Action",
                UITheme.Label(UITheme.FontTiny, actionCol));
            cx += 65;

            Color bonusCol = pc.HasBonusAction ? new Color(0.9f, 0.6f, 0.1f) : UITheme.DisabledGray;
            UITheme.DrawRect(new Rect(cx, py + 3, PipH - 6, PipH - 6), bonusCol);
            GUI.Label(new Rect(cx + PipH - 2, py, 50, PipH), "Bonus",
                UITheme.Label(UITheme.FontTiny, bonusCol));
            cx += 65;

            Color reactCol = pc.HasReaction ? UITheme.ManaBlue : UITheme.DisabledGray;
            UITheme.DrawRect(new Rect(cx, py + 3, PipH - 6, PipH - 6), reactCol);
            GUI.Label(new Rect(cx + PipH - 2, py, 60, PipH), "Reaction",
                UITheme.Label(UITheme.FontTiny, reactCol));
        }

        private void DrawActionBar(BattleManager bm, BattleCombatant pc)
        {
            bool hasAction = pc.HasAction;
            bool hasSpells = pc.Sheet != null && pc.Sheet.PreparedSpells.Count > 0;
            int potionCount = GetItemCount(GameManager.Instance?.Player?.Inventory, ItemIds.HealthPotion);

            bool adjacentEnemy = bm.Combatants.Any(c =>
                c != null && c.IsAlive && !c.IsPlayer
                && System.Math.Abs(c.X - pc.X) + System.Math.Abs(c.Y - pc.Y) == 1);

            bool hasTarget = bm.SelectedTarget != null && bm.SelectedTarget.IsAlive;
            int targetDist = hasTarget
                ? System.Math.Abs(pc.X - bm.SelectedTarget.X) + System.Math.Abs(pc.Y - bm.SelectedTarget.Y)
                : 999;
            int weaponRange = bm.GetPlayerWeaponRange();
            bool canAttack = hasAction && (adjacentEnemy || (hasTarget && targetDist <= weaponRange));

            var actions = new[]
            {
                ("Attack",    "[F]",     canAttack,                    hasAction),
                ("Spell",     "[V]",     hasSpells && hasAction,       hasAction),
                ("Potion",    "[H]",     potionCount > 0 && hasAction, hasAction),
                ("Dodge",     "[G]",     hasAction,                    hasAction),
                ("Disengage", "[R]",     hasAction,                    hasAction),
                ("Dash",      "[T]",     hasAction,                    hasAction),
                ("Shove",     "[B]",     pc.HasBonusAction,            pc.HasBonusAction),
                ("End Turn",  "[Space]", true,                         true),
            };

            float totalW = actions.Length * (BtnW + BtnGap) - BtnGap;
            float startX = Screen.width / 2f - totalW / 2f;
            float by = Screen.height - BarH - 8;

            UITheme.DrawPanel(new Rect(startX - 8, by - 4, totalW + 16, BarH + 8));

            for (int i = 0; i < actions.Length; i++)
            {
                var (label, key, enabled, resourceAvailable) = actions[i];
                float btnX = startX + i * (BtnW + BtnGap);

                Color btnBg = enabled ? new Color(0.15f, 0.14f, 0.18f, 0.95f) : new Color(0.1f, 0.1f, 0.1f, 0.8f);
                if (!resourceAvailable) btnBg = new Color(0.05f, 0.05f, 0.05f, 0.6f);
                UITheme.DrawRect(new Rect(btnX, by, BtnW, BtnH), btnBg);

                if (enabled)
                    UITheme.DrawBorder(new Rect(btnX, by, BtnW, BtnH), UITheme.PanelBorder, 1f);

                Color textCol = enabled ? UITheme.TextPrimary : UITheme.DisabledGray;
                GUI.Label(new Rect(btnX, by + 4, BtnW, 18), label,
                    UITheme.Bold(UITheme.FontSmall, textCol, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(btnX, by + 22, BtnW, 14), key,
                    UITheme.Label(UITheme.FontTiny, UITheme.DisabledGray, TextAnchor.MiddleCenter));

                if (enabled && GUI.Button(new Rect(btnX, by, BtnW, BtnH), GUIContent.none, GUIStyle.none))
                {
                    switch (label)
                    {
                        case "Attack":    if (bm.SelectedTarget != null) bm.AttackSelectedTarget(); else bm.AttackNearestEnemy(); break;
                        case "Spell":     bm.ToggleSpellMenu(); break;
                        case "Potion":    bm.UseHealthPotion(); break;
                        case "Dodge":     bm.PlayerDodge(); break;
                        case "Disengage": bm.PlayerDisengage(); break;
                        case "Dash":      bm.PlayerDash(); break;
                        case "Shove":     break;
                        case "End Turn":  bm.PlayerEndTurn(); break;
                    }
                }
            }

            if (bm.IsSpellMenuOpen && bm.AvailableSpells != null)
                DrawSpellMenu(bm, pc);
        }

        private void DrawSpellMenu(BattleManager bm, BattleCombatant pc)
        {
            var spells = bm.AvailableSpells;
            float menuW = 340f;
            float rowH = 24f;
            float menuH = 32 + spells.Count * rowH + 24;
            float mx = Screen.width / 2 - menuW / 2;
            float my = Screen.height - BarH - PipH - menuH - 24;

            UITheme.DrawPanel(new Rect(mx, my, menuW, menuH));
            GUI.Label(new Rect(mx + 10, my + 6, 320, 20),
                "SPELLS (1-9 to cast, Esc to close)",
                UITheme.Bold(UITheme.FontSmall, UITheme.TextHeader));

            var slots = pc.Sheet?.SpellSlots;
            for (int i = 0; i < spells.Count && i < 9; i++)
            {
                var spell = spells[i];
                string lvl = spell.IsCantrip ? "cantrip" : $"L{spell.Level}";
                string dmg = spell.DamageDiceCount > 0 ? $" {spell.GetDamage()} {spell.DamageType}" : "";
                string heal = spell.HealingDiceCount > 0 ? $" Heal {spell.GetHealing()}" : "";
                string conc = spell.Concentration ? " [C]" : "";

                bool canCast = spell.IsCantrip || (slots != null && slots.CanCast(spell, spell.Level));
                Color col = canCast ? UITheme.TextPrimary : UITheme.DisabledGray;

                float ry = my + 28 + i * rowH;
                GUI.Label(new Rect(mx + 10, ry, 320, rowH),
                    $"[{i + 1}] {spell.Name} ({lvl}){dmg}{heal}{conc}",
                    UITheme.Label(UITheme.FontSmall, col));
            }

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
                GUI.Label(new Rect(mx + 10, my + menuH - 20, 320, 16), slotInfo,
                    UITheme.Label(UITheme.FontTiny, UITheme.ManaBlue));
            }
        }

        private void DrawHoverTooltip(BattleManager bm, BattleCombatant pc)
        {
            var input = Object.FindAnyObjectByType<BattleInputController>();
            if (input == null) return;

            var hovered = input.HoveredEnemy;
            if (hovered == null || !hovered.IsAlive || hovered.IsPlayer) return;

            int hitChance = bm.GetHitChance(hovered);
            string dmgRange = bm.GetDamageRange();
            int dist = System.Math.Abs(pc.X - hovered.X) + System.Math.Abs(pc.Y - hovered.Y);

            float tx = Input.mousePosition.x + 16;
            float ty = Screen.height - Input.mousePosition.y - TooltipH - 16;
            if (tx + TooltipW > Screen.width) tx = Input.mousePosition.x - TooltipW - 8;
            if (ty < 0) ty = 8;

            UITheme.DrawPanel(new Rect(tx, ty, TooltipW, TooltipH));

            GUI.Label(new Rect(tx + 8, ty + 4, TooltipW - 16, 18), hovered.Name,
                UITheme.Bold(UITheme.FontSmall, UITheme.EnemyRed));
            GUI.Label(new Rect(tx + 8, ty + 22, TooltipW - 16, 14),
                $"AC {hovered.AC}  HP {hovered.HP}/{hovered.MaxHP}",
                UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));
            GUI.Label(new Rect(tx + 8, ty + 36, TooltipW - 16, 14),
                $"Hit: {hitChance}%  Dmg: {dmgRange}  Dist: {dist}",
                UITheme.Label(UITheme.FontTiny, hitChance >= 50 ? UITheme.XPGold : UITheme.DisabledGray));

            int range = bm.GetPlayerWeaponRange();
            bool inRange = dist <= range;
            GUI.Label(new Rect(tx + 8, ty + 50, TooltipW - 16, 14),
                inRange ? "In Range \u2014 click to attack" : "Out of range",
                UITheme.Label(UITheme.FontTiny, inRange ? new Color(0.3f, 0.9f, 0.3f) : UITheme.EnemyRed));
        }

        private void DrawPlayerStatusBar(BattleManager bm)
        {
            var pc = bm.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (pc == null) return;

            float barW = 260;
            float barH = 24;
            float bx = Screen.width / 2 - barW / 2;
            float by = StripH + 8;

            UITheme.DrawPanel(new Rect(bx - 4, by - 4, barW + 8, barH + 8));
            float pct = pc.MaxHP > 0 ? Mathf.Clamp01((float)pc.HP / pc.MaxHP) : 0;
            UITheme.DrawBar(new Rect(bx, by, barW, barH), pct, UITheme.HPColor(pct),
                $"HP {pc.HP}/{pc.MaxHP}    AC {pc.AC}");
        }

        private void DrawCombatLog(BattleManager bm)
        {
            int logY = Screen.height - 170;
            var logRect = new Rect(10, logY, 320, 150);
            UITheme.DrawPanel(logRect);
            GUI.Label(new Rect(20, logY + 2, 300, 18), "Combat Log",
                UITheme.Header(UITheme.FontSmall));
            _logScroll = GUI.BeginScrollView(new Rect(10, logY + 20, 320, 130), _logScroll,
                new Rect(0, 0, 300, bm.Log.Count * 16));
            for (int i = 0; i < bm.Log.Count; i++)
                GUI.Label(new Rect(5, i * 16, 300, 16), bm.Log[i],
                    UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));
            GUI.EndScrollView();
        }

        private void DrawConditions(BattleManager bm)
        {
            var player = bm.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player != null && player.Conditions != null && player.Conditions.ActiveFlags != Condition.None)
                GUI.Label(new Rect(Screen.width / 2 + 140, StripH + 12, 200, 18),
                    $"Conditions: {player.Conditions.ActiveFlags}",
                    UITheme.Label(UITheme.FontTiny, UITheme.XPGold));

            if (bm.SelectedTarget != null && bm.SelectedTarget.IsAlive)
                GUI.Label(new Rect(Screen.width / 2 + 140, StripH + 28, 200, 18),
                    $"Target: {bm.SelectedTarget.Name}",
                    UITheme.Label(UITheme.FontTiny, UITheme.EnemyRed));
        }

        private void DrawDeathSavePips(DeathSaveTracker ds)
        {
            float cx = Screen.width / 2;
            float cy = Screen.height / 2 + 60;
            UITheme.DrawPanel(new Rect(cx - 120, cy, 240, 60));
            GUI.Label(new Rect(cx - 110, cy + 5, 220, 20), "DEATH SAVES",
                UITheme.Header(UITheme.FontMedium));

            string successes = "";
            for (int i = 0; i < 3; i++) successes += i < ds.Successes ? "\u25CF " : "\u25CB ";
            GUI.Label(new Rect(cx - 110, cy + 25, 110, 20), $"Pass: {successes}",
                UITheme.Bold(UITheme.FontMedium, Color.green, TextAnchor.MiddleCenter));

            string failures = "";
            for (int i = 0; i < 3; i++) failures += i < ds.Failures ? "\u25CF " : "\u25CB ";
            GUI.Label(new Rect(cx, cy + 25, 110, 20), $"Fail: {failures}",
                UITheme.Bold(UITheme.FontMedium, UITheme.EnemyRed, TextAnchor.MiddleCenter));
        }

        private void DrawBattleOver(BattleManager bm)
        {
            var overRect = new Rect(Screen.width / 2 - 120, Screen.height / 2 - 50, 240, 100);
            UITheme.DrawPanel(overRect);
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 40, 200, 24),
                bm.PlayerWon ? "VICTORY" : "DEFEATED",
                UITheme.Bold(UITheme.FontLarge, UITheme.TextHeader, TextAnchor.MiddleCenter));
            if (bm.PlayerWon)
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 16, 200, 18),
                    $"Gold: +{GameManager.Instance?.LastBattleGoldEarned ?? 0}",
                    UITheme.Label(UITheme.FontNormal, UITheme.XPGold, TextAnchor.MiddleCenter));
            if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2 + 20, 100, 28),
                "Continue", UITheme.Button()))
                bm.EndBattle();
        }

        private static int GetItemCount(Inventory inv, int itemId)
        {
            if (inv == null) return 0;
            int total = 0;
            foreach (var slot in inv.GetAllSlots())
                if (slot.ItemId == itemId) total += slot.StackCount;
            return total;
        }
    }
}
