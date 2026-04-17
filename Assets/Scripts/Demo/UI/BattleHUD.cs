using UnityEngine;
using System.Linq;
using ForeverEngine.Core.Enums;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Demo.Battle;
using ForeverEngine.Network;

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
            var br = BattleRenderer.Instance;
            if (br == null || !br.IsActive) return;
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) return;

            DrawTurnOrderStrip(br);
            DrawPlayerStatusBar(br);
            DrawCombatLog(br);
            DrawConditions(br);

            if (br.IsPlayerTurn && !br.BattleOver)
            {
                var pc = br.Combatants.FirstOrDefault(c => c.IsPlayer);
                if (pc != null)
                {
                    DrawResourcePips(br);
                    DrawActionBar(br, pc);
                    DrawHoverTooltip(br, pc);
                }
            }

            if (br.BattleOver) DrawBattleOver(br);
        }

        private void DrawTurnOrderStrip(BattleRenderer br)
        {
            int count = br.Combatants.Length;
            float totalW = count * (PortraitSize + PortraitGap) - PortraitGap;
            float startX = Screen.width / 2f - totalW / 2f;

            UITheme.DrawPanel(new Rect(startX - 8, 4, totalW + 16, StripH));

            for (int i = 0; i < count; i++)
            {
                var c = br.Combatants[i];
                float px = startX + i * (PortraitSize + PortraitGap);
                float py = 10;

                bool isCurrent = c.Id == br.ActiveCombatantId;
                Color bg = c.IsPlayer ? UITheme.FriendlyBlue : UITheme.EnemyRed;
                if (c.Hp <= 0) bg = UITheme.DisabledGray;

                if (isCurrent)
                    UITheme.DrawRect(new Rect(px - 2, py - 2, PortraitSize + 4, PortraitSize + 4), UITheme.TextHeader);

                UITheme.DrawRect(new Rect(px, py, PortraitSize, PortraitSize), bg * 0.7f);

                string initial = c.Name.Length > 0 ? c.Name.Substring(0, System.Math.Min(2, c.Name.Length)).ToUpper() : "?";
                GUI.Label(new Rect(px, py + 2, PortraitSize, 20), initial,
                    UITheme.Bold(UITheme.FontMedium, Color.white, TextAnchor.MiddleCenter));

                if (c.MaxHp > 0)
                {
                    float hpPct = Mathf.Clamp01((float)c.Hp / c.MaxHp);
                    float barW = PortraitSize - 4;
                    UITheme.DrawRect(new Rect(px + 2, py + PortraitSize - 8, barW, 6), new Color(0.2f, 0.1f, 0.1f));
                    UITheme.DrawRect(new Rect(px + 2, py + PortraitSize - 8, barW * hpPct, 6), UITheme.HPColor(hpPct));
                }
            }
        }

        private void DrawResourcePips(BattleRenderer br)
        {
            float barTotalW = 8 * (BtnW + BtnGap) - BtnGap;
            float bx = Screen.width / 2f - barTotalW / 2f;
            float py = Screen.height - BarH - PipH - 12;

            UITheme.DrawPanel(new Rect(bx - 4, py - 2, barTotalW + 8, PipH + 4));

            float cx = bx;

            float moveBarW = 120f;
            float movePct = br.MovementRemaining > 0 ? Mathf.Clamp01(1f) : 0f;
            GUI.Label(new Rect(cx, py, 40, PipH), "Move:", UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));
            UITheme.DrawBar(new Rect(cx + 42, py + 3, moveBarW, PipH - 6), movePct,
                new Color(0.9f, 0.6f, 0.1f), $"{br.MovementRemaining}");
            cx += moveBarW + 56;

            Color actionCol = br.HasAction ? new Color(0.2f, 0.75f, 0.3f) : UITheme.DisabledGray;
            UITheme.DrawRect(new Rect(cx, py + 3, PipH - 6, PipH - 6), actionCol);
            GUI.Label(new Rect(cx + PipH - 2, py, 50, PipH), "Action",
                UITheme.Label(UITheme.FontTiny, actionCol));
            cx += 65;

            Color bonusCol = br.HasBonusAction ? new Color(0.9f, 0.6f, 0.1f) : UITheme.DisabledGray;
            UITheme.DrawRect(new Rect(cx, py + 3, PipH - 6, PipH - 6), bonusCol);
            GUI.Label(new Rect(cx + PipH - 2, py, 50, PipH), "Bonus",
                UITheme.Label(UITheme.FontTiny, bonusCol));
        }

        private void DrawActionBar(BattleRenderer br, BattleCombatantDto pc)
        {
            bool hasAction = br.HasAction;

            var actions = new[]
            {
                ("Attack",    "[F]",     hasAction,            hasAction),
                ("Spell",     "[V]",     hasAction,            hasAction),
                ("Potion",    "[H]",     hasAction,            hasAction),
                ("Dodge",     "[G]",     hasAction,            hasAction),
                ("Disengage", "[R]",     hasAction,            hasAction),
                ("Dash",      "[T]",     hasAction,            hasAction),
                ("Shove",     "[B]",     br.HasBonusAction,    br.HasBonusAction),
                ("End Turn",  "[Space]", true,                 true),
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
                    SendAction(br, label);
                }
            }
        }

        private void SendAction(BattleRenderer br, string action)
        {
            var client = NetworkClient.Instance;
            if (client == null) return;

            switch (action)
            {
                case "Attack":
                    // Target first alive enemy
                    var enemy = br.Combatants.FirstOrDefault(c => !c.IsPlayer && c.Hp > 0);
                    if (enemy != null)
                    {
                        client.Send(new BattleActionMessage
                        {
                            BattleId = br.BattleId,
                            ActionType = BattleActionType.MeleeAttack,
                            TargetId = enemy.Id
                        });
                    }
                    break;

                case "Dodge":
                    client.Send(new BattleActionMessage
                    {
                        BattleId = br.BattleId,
                        ActionType = BattleActionType.Dodge
                    });
                    break;

                case "Dash":
                    client.Send(new BattleActionMessage
                    {
                        BattleId = br.BattleId,
                        ActionType = BattleActionType.Dash
                    });
                    break;

                case "Disengage":
                    client.Send(new BattleActionMessage
                    {
                        BattleId = br.BattleId,
                        ActionType = BattleActionType.Disengage
                    });
                    break;

                case "End Turn":
                    client.Send(new BattleActionMessage
                    {
                        BattleId = br.BattleId,
                        ActionType = BattleActionType.EndTurn
                    });
                    break;

                case "Spell":
                    client.Send(new BattleActionMessage
                    {
                        BattleId = br.BattleId,
                        ActionType = BattleActionType.CastSpell
                    });
                    break;

                case "Potion":
                    client.Send(new BattleActionMessage
                    {
                        BattleId = br.BattleId,
                        ActionType = BattleActionType.UseItem
                    });
                    break;
            }
        }

        private void DrawHoverTooltip(BattleRenderer br, BattleCombatantDto pc)
        {
            var input = Object.FindAnyObjectByType<BattleInputController>();
            if (input == null) return;

            var hovered = input.HoveredEnemy;
            if (hovered == null || hovered.IsPlayer || hovered.Hp <= 0) return;

            int dist = System.Math.Abs(pc.X - hovered.X) + System.Math.Abs(pc.Y - hovered.Y);

            float tx = Input.mousePosition.x + 16;
            float ty = Screen.height - Input.mousePosition.y - TooltipH - 16;
            if (tx + TooltipW > Screen.width) tx = Input.mousePosition.x - TooltipW - 8;
            if (ty < 0) ty = 8;

            UITheme.DrawPanel(new Rect(tx, ty, TooltipW, TooltipH));

            GUI.Label(new Rect(tx + 8, ty + 4, TooltipW - 16, 18), hovered.Name,
                UITheme.Bold(UITheme.FontSmall, UITheme.EnemyRed));
            GUI.Label(new Rect(tx + 8, ty + 22, TooltipW - 16, 14),
                $"AC {hovered.Ac}  HP {hovered.Hp}/{hovered.MaxHp}",
                UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));
            GUI.Label(new Rect(tx + 8, ty + 36, TooltipW - 16, 14),
                $"Dist: {dist}",
                UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));

            bool inRange = dist <= 1;
            GUI.Label(new Rect(tx + 8, ty + 50, TooltipW - 16, 14),
                inRange ? "In Range \u2014 click to attack" : "Out of range",
                UITheme.Label(UITheme.FontTiny, inRange ? new Color(0.3f, 0.9f, 0.3f) : UITheme.EnemyRed));
        }

        private void DrawPlayerStatusBar(BattleRenderer br)
        {
            var pc = br.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (pc == null) return;

            float barW = 260;
            float barH = 24;
            float bx = Screen.width / 2 - barW / 2;
            float by = StripH + 8;

            UITheme.DrawPanel(new Rect(bx - 4, by - 4, barW + 8, barH + 8));
            float pct = pc.MaxHp > 0 ? Mathf.Clamp01((float)pc.Hp / pc.MaxHp) : 0;
            UITheme.DrawBar(new Rect(bx, by, barW, barH), pct, UITheme.HPColor(pct),
                $"HP {pc.Hp}/{pc.MaxHp}    AC {pc.Ac}");
        }

        private void DrawCombatLog(BattleRenderer br)
        {
            int logY = Screen.height - 170;
            var logRect = new Rect(10, logY, 320, 150);
            UITheme.DrawPanel(logRect);
            GUI.Label(new Rect(20, logY + 2, 300, 18), "Combat Log",
                UITheme.Header(UITheme.FontSmall));
            _logScroll = GUI.BeginScrollView(new Rect(10, logY + 20, 320, 130), _logScroll,
                new Rect(0, 0, 300, br.BattleLog.Count * 16));
            for (int i = 0; i < br.BattleLog.Count; i++)
                GUI.Label(new Rect(5, i * 16, 300, 16), br.BattleLog[i],
                    UITheme.Label(UITheme.FontTiny, UITheme.TextPrimary));
            GUI.EndScrollView();
        }

        private void DrawConditions(BattleRenderer br)
        {
            var player = br.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player != null && player.Conditions != null && player.Conditions.Length > 0)
                GUI.Label(new Rect(Screen.width / 2 + 140, StripH + 12, 200, 18),
                    $"Conditions: {string.Join(", ", player.Conditions)}",
                    UITheme.Label(UITheme.FontTiny, UITheme.XPGold));
        }

        private void DrawBattleOver(BattleRenderer br)
        {
            var overRect = new Rect(Screen.width / 2 - 120, Screen.height / 2 - 50, 240, 100);
            UITheme.DrawPanel(overRect);
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 40, 200, 24),
                br.PlayerWon ? "VICTORY" : "DEFEATED",
                UITheme.Bold(UITheme.FontLarge, UITheme.TextHeader, TextAnchor.MiddleCenter));
            if (br.PlayerWon)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 16, 200, 18),
                    $"XP: +{br.XpEarned}  Gold: +{br.GoldEarned}",
                    UITheme.Label(UITheme.FontNormal, UITheme.XPGold, TextAnchor.MiddleCenter));
            }
            if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2 + 20, 100, 28),
                "Continue", UITheme.Button()))
            {
                br.Cleanup();
            }
        }
    }
}
