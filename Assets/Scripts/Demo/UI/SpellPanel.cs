using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Demo.Battle;
using ForeverEngine.RPG.Spells;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// IMGUI panel rendered on the left side of the screen while the spell menu
    /// is open during combat. Shows prepared spell list with hotkeys, school
    /// icons, damage/healing dice, range, concentration indicator, and a slot
    /// availability summary per spell level at the bottom.
    ///
    /// Companion to BattleHUD.DrawSpellMenu (which draws the older compact
    /// bottom-center overlay). This panel is the primary full-detail view.
    /// </summary>
    public class SpellPanel : UnityEngine.MonoBehaviour
    {
        // ── Layout constants ──────────────────────────────────────────────────

        private const float PanelX      = 10f;
        private const float PanelY      = 10f;
        private const float PanelW      = 300f;
        private const float HeaderH     = 28f;
        private const float SpellRowH   = 44f;   // Two lines per spell row
        private const float SlotSectionH = 28f;
        private const float Pad         = 8f;

        // Maximum spells rendered (keys 1-9).
        private const int MaxSpells = 9;

        // ── School icon lookup ────────────────────────────────────────────────

        /// <summary>Short ASCII symbol used in the panel for each school.</summary>
        private static string SchoolIcon(SpellSchool school) => school switch
        {
            SpellSchool.Abjuration    => "[Abj]",
            SpellSchool.Conjuration   => "[Con]",
            SpellSchool.Divination    => "[Div]",
            SpellSchool.Enchantment   => "[Enc]",
            SpellSchool.Evocation     => "[Evo]",
            SpellSchool.Illusion      => "[Ill]",
            SpellSchool.Necromancy    => "[Nec]",
            SpellSchool.Transmutation => "[Trn]",
            _                         => "[???]",
        };

        // ── OnGUI ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;
            if (!bm.IsSpellMenuOpen) return;

            var spells = bm.AvailableSpells;
            if (spells == null || spells.Count == 0) return;

            int count = Mathf.Min(spells.Count, MaxSpells);

            // Get spell slot data — prefer CharacterSheet slots, fall back to
            // the current combatant's sheet via BattleManager's current turn.
            SpellSlotManager slots = null;
            var character = GameManager.Instance?.Character;
            if (character != null)
                slots = character.SpellSlots;
            else if (bm.CurrentTurn?.Sheet?.SpellSlots != null)
                slots = bm.CurrentTurn.Sheet.SpellSlots;

            bool hasSlotSection = slots != null && HasAnySlots(slots);
            float panelH = HeaderH + count * SpellRowH + Pad
                         + (hasSlotSection ? SlotSectionH + Pad : 0f) + Pad;

            var panelRect = new Rect(PanelX, PanelY, PanelW, panelH);
            UITheme.DrawPanel(panelRect);

            DrawHeader(panelRect);
            DrawSpellRows(spells, count, slots, panelRect);
            if (hasSlotSection)
                DrawSlotSection(slots, panelRect, panelH);
        }

        // ── Drawing helpers ───────────────────────────────────────────────────

        private static void DrawHeader(Rect panel)
        {
            var headerRect = new Rect(panel.x + Pad, panel.y + 4f, panel.width - Pad * 2, 20f);
            GUI.Label(headerRect, "SPELLS  (1-9 cast  |  Q close)",
                UITheme.Header(UITheme.FontSmall));

            UITheme.DrawSeparator(panel.x + Pad, panel.y + HeaderH - 2f, panel.width - Pad * 2);
        }

        private static void DrawSpellRows(List<SpellData> spells, int count,
                                          SpellSlotManager slots, Rect panel)
        {
            for (int i = 0; i < count; i++)
            {
                var spell = spells[i];
                float rowY = panel.y + HeaderH + Pad + i * SpellRowH;
                DrawSpellRow(spell, i + 1, slots, panel.x + Pad, rowY, panel.width - Pad * 2);
            }
        }

        /// <summary>
        /// Renders a two-line entry for one spell:
        ///   Line 1: [N]  Name (Level / Cantrip)  [School]
        ///   Line 2:      damage-or-healing | range | concentration
        /// </summary>
        private static void DrawSpellRow(SpellData spell, int hotkey,
                                         SpellSlotManager slots,
                                         float x, float y, float w)
        {
            bool canCast = spell.IsCantrip
                || (slots != null && slots.CanCast(spell, spell.Level));

            Color nameCol  = canCast ? UITheme.TextPrimary : UITheme.DisabledGray;
            Color detailCol = canCast ? UITheme.TextSecondary : UITheme.DisabledGray;

            // ── Line 1: hotkey + name + level + school ──
            string lvl    = spell.IsCantrip ? "Cantrip" : $"L{spell.Level}";
            string school  = SchoolIcon(spell.School);
            string keyStr  = $"[{hotkey}]";

            float keyW    = 28f;
            float schoolW = 40f;
            float nameW   = w - keyW - schoolW - 4f;

            GUI.Label(new Rect(x, y, keyW, 20f), keyStr,
                UITheme.Bold(UITheme.FontSmall, canCast ? UITheme.TextAccent : UITheme.DisabledGray));

            GUI.Label(new Rect(x + keyW, y, nameW, 20f),
                $"{spell.Name}  ({lvl})",
                UITheme.Bold(UITheme.FontSmall, nameCol));

            GUI.Label(new Rect(x + keyW + nameW, y, schoolW, 20f), school,
                UITheme.Label(UITheme.FontTiny, UITheme.ManaBlue, TextAnchor.MiddleRight));

            // ── Line 2: damage / healing / range / concentration ──
            string detail = BuildDetailLine(spell);

            GUI.Label(new Rect(x + keyW, y + 20f, nameW + schoolW, 18f), detail,
                UITheme.Label(UITheme.FontTiny, detailCol));

            // Subtle separator between rows
            UITheme.DrawSeparator(x, y + SpellRowH - 2f, w);
        }

        private static string BuildDetailLine(SpellData spell)
        {
            var parts = new System.Text.StringBuilder();

            // Damage dice
            if (spell.DamageDiceCount > 0)
            {
                var dmg = spell.GetDamage();
                parts.Append($"{dmg} {spell.DamageType}");
            }

            // Healing dice
            if (spell.HealingDiceCount > 0)
            {
                if (parts.Length > 0) parts.Append("  ");
                parts.Append($"Heal {spell.GetHealing()}");
            }

            // Range
            if (spell.Range > 0)
            {
                if (parts.Length > 0) parts.Append("  |  ");
                parts.Append(spell.Range == 5 ? "Touch" : $"{spell.Range} ft");
            }
            else if (spell.Range == 0)
            {
                if (parts.Length > 0) parts.Append("  |  ");
                parts.Append("Self");
            }

            // Concentration marker
            if (spell.Concentration)
            {
                if (parts.Length > 0) parts.Append("  ");
                parts.Append("[Conc]");
            }

            // Save / attack type
            if (spell.HasSave)
            {
                if (parts.Length > 0) parts.Append("  |  ");
                parts.Append($"{spell.SaveType} save");
            }
            else if (spell.SpellAttack)
            {
                if (parts.Length > 0) parts.Append("  |  ");
                parts.Append("Spell atk");
            }

            return parts.Length > 0 ? parts.ToString() : "Utility";
        }

        /// <summary>
        /// Footer section: one compact row of available/max slots per level.
        /// Only levels with MaxSlots > 0 are shown.
        /// </summary>
        private static void DrawSlotSection(SpellSlotManager slots, Rect panel, float panelH)
        {
            float sectionY = panel.y + panelH - SlotSectionH - Pad;
            UITheme.DrawSeparator(panel.x + Pad, sectionY, panel.width - Pad * 2);

            // Build the slot summary string, e.g. "L1: 3/4  L2: 1/2"
            var sb = new System.Text.StringBuilder("Slots: ");
            bool any = false;
            for (int i = 0; i < 9; i++)
            {
                if (slots.MaxSlots[i] <= 0) continue;
                if (any) sb.Append("  ");
                sb.Append($"L{i + 1}:{slots.AvailableSlots[i]}/{slots.MaxSlots[i]}");
                any = true;
            }

            // Warlock pact slots
            if (slots.PactSlotCount > 0)
            {
                if (any) sb.Append("  ");
                sb.Append($"Pact:L{slots.PactSlotLevel}×{slots.PactSlotCount}");
            }

            GUI.Label(new Rect(panel.x + Pad, sectionY + 4f, panel.width - Pad * 2, 20f),
                sb.ToString(),
                UITheme.Label(UITheme.FontTiny, UITheme.ManaBlue));
        }

        private static bool HasAnySlots(SpellSlotManager slots)
        {
            if (slots.PactSlotCount > 0) return true;
            for (int i = 0; i < 9; i++)
                if (slots.MaxSlots[i] > 0) return true;
            return false;
        }
    }
}
