using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static metamagic engine for Sorcerer metamagic options.
    ///
    /// Metamagic types and costs:
    /// - Twinned (cost = spell level, min 1): duplicate target for single-target spells
    /// - Quickened (cost 2): cast as bonus action instead of action
    /// - Subtle (cost 1): no verbal/somatic components
    /// - Empowered (cost 1): reroll damage dice (take higher)
    /// - Heightened (cost 3): target has disadvantage on first save
    /// - Careful (cost 1): chosen allies auto-succeed on AoE saves
    /// - Distant (cost 1): double range (or touch -> 30ft)
    /// </summary>
    public static class MetamagicEngine
    {
        /// <summary>
        /// Apply metamagic to a cast context. Spends sorcery points and modifies the context.
        /// Returns the modified context (or original if metamagic can't be applied).
        /// </summary>
        /// <param name="ctx">Original cast context.</param>
        /// <param name="type">Metamagic type to apply.</param>
        /// <param name="sorceryPoints">Sorcery point pool (modified in place).</param>
        /// <returns>Modified cast context.</returns>
        public static CastContext ApplyMetamagic(CastContext ctx, MetamagicType type, ref ResourcePool sorceryPoints)
        {
            var result = ctx;

            // Apply each selected metamagic
            if ((type & MetamagicType.Twinned) != 0)
            {
                int cost = ctx.Spell.Level < 1 ? 1 : ctx.Spell.Level;
                if (sorceryPoints.Spend(cost))
                {
                    // Twinned: duplicate target list (pipeline handles applying to both)
                    if (result.Targets != null && result.Targets.Length == 1)
                    {
                        // Marker: pipeline should apply spell to 2 targets
                        // Implementation detail handled by SpellCastingPipeline
                    }
                }
            }

            if ((type & MetamagicType.Quickened) != 0)
            {
                if (sorceryPoints.Spend(2))
                {
                    // Quickened: changes casting time to bonus action
                    // Handled by action economy system, not spell pipeline
                }
            }

            if ((type & MetamagicType.Subtle) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Subtle: no verbal/somatic — can't be counterspelled
                    // Marker for pipeline: skip counterspell check
                }
            }

            if ((type & MetamagicType.Empowered) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Empowered: marker for DamageResolver to reroll low dice
                    // Handled during damage roll phase
                }
            }

            if ((type & MetamagicType.Heightened) != 0)
            {
                if (sorceryPoints.Spend(3))
                {
                    // Heightened: target has disadvantage on first save
                    // Marker for save resolution
                }
            }

            if ((type & MetamagicType.Careful) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Careful: chosen allies auto-succeed on AoE saves
                    // Marker for AoE save resolution
                }
            }

            if ((type & MetamagicType.Distant) != 0)
            {
                if (sorceryPoints.Spend(1))
                {
                    // Distant: double range (touch becomes 30ft)
                    // Processed during validation — range check uses doubled value
                }
            }

            // Store the metamagic in the modified context so pipeline can reference it
            result.Metamagic = type;
            return result;
        }

        /// <summary>
        /// Get the sorcery point cost for a specific metamagic type.
        /// </summary>
        /// <param name="type">Single metamagic type.</param>
        /// <param name="spellLevel">Level of the spell (for Twinned).</param>
        /// <returns>Cost in sorcery points.</returns>
        public static int GetCost(MetamagicType type, int spellLevel = 0)
        {
            switch (type)
            {
                case MetamagicType.Twinned:   return spellLevel < 1 ? 1 : spellLevel;
                case MetamagicType.Quickened:  return 2;
                case MetamagicType.Subtle:     return 1;
                case MetamagicType.Empowered:  return 1;
                case MetamagicType.Heightened: return 3;
                case MetamagicType.Careful:    return 1;
                case MetamagicType.Distant:    return 1;
                default:                       return 0;
            }
        }

        /// <summary>
        /// Check if a metamagic can be applied to a spell.
        /// </summary>
        /// <param name="type">Metamagic type.</param>
        /// <param name="spell">The spell to check.</param>
        /// <returns>True if metamagic is valid for this spell.</returns>
        public static bool CanApply(MetamagicType type, SpellData spell)
        {
            if (spell == null) return false;

            switch (type)
            {
                case MetamagicType.Twinned:
                    // Can only twin single-target spells
                    return spell.AreaShape == AoEShape.None;
                case MetamagicType.Empowered:
                    // Only for spells that deal damage
                    return spell.DamageDiceCount > 0;
                case MetamagicType.Heightened:
                    // Only for spells that require a save
                    return spell.HasSave;
                case MetamagicType.Careful:
                    // Only for AoE spells that require a save
                    return spell.AreaShape != AoEShape.None && spell.HasSave;
                case MetamagicType.Distant:
                    // Any spell with a range
                    return spell.Range > 0;
                default:
                    return true; // Quickened and Subtle work on any spell
            }
        }
    }
}
