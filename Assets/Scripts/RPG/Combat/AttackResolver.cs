using System.Collections.Generic;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Static attack resolver implementing full D&D 5e attack resolution:
    /// 1. Determine ability modifier (STR for melee, DEX for ranged, Finesse = higher of STR/DEX)
    /// 2. Evaluate advantage/disadvantage from conditions + explicit sources
    /// 3. Roll d20 (or 2d20 with advantage/disadvantage)
    /// 4. Natural 1 = auto-miss, Natural 20 (or within CritRange) = auto-hit + critical
    /// 5. Total = roll + ability mod + proficiency + magic bonus
    /// 6. Compare to target AC
    /// </summary>
    public static class AttackResolver
    {
        /// <summary>
        /// Resolve a single attack.
        /// </summary>
        /// <param name="ctx">Attack context with all inputs.</param>
        /// <param name="seed">RNG seed.</param>
        /// <returns>Attack result.</returns>
        public static AttackResult Resolve(AttackContext ctx, ref uint seed)
        {
            // 1. Determine ability modifier
            int abilityMod = GetAbilityModifier(ctx);

            // 2. Evaluate advantage/disadvantage
            var advReasons = ctx.AdvantageReasons ?? new List<string>();
            var disadvReasons = ctx.DisadvantageReasons ?? new List<string>();

            // Add condition-based advantage/disadvantage
            ConditionManager.GetAdvantageModifiers(
                ctx.AttackerConditions,
                ctx.TargetConditions,
                ctx.IsMelee,
                ctx.IsRanged,
                advReasons,
                disadvReasons);

            // Determine final advantage state
            bool hasAdvantage = advReasons.Count > 0;
            bool hasDisadvantage = disadvReasons.Count > 0;
            AdvantageState state;

            if (hasAdvantage && hasDisadvantage)
                state = AdvantageState.Cancelled;
            else if (hasAdvantage)
                state = AdvantageState.Advantage;
            else if (hasDisadvantage)
                state = AdvantageState.Disadvantage;
            else
                state = AdvantageState.None;

            // 3. Roll d20
            int naturalRoll = RollD20(state, ref seed);

            // 4. Natural 1 = auto-miss
            if (naturalRoll == 1)
            {
                return AttackResult.Miss(1, 1 + abilityMod + ctx.AttackerProficiency + ctx.MagicBonus, state);
            }

            // Check for critical hit
            int critRange = ctx.CritRange > 0 ? ctx.CritRange : 20;
            bool isCritical = naturalRoll >= critRange;

            // Check for melee auto-crit (target paralyzed/unconscious within 5ft)
            if (ctx.IsMelee && ConditionManager.IsMeleeAutoCrit(ctx.TargetConditions))
            {
                isCritical = true;
            }

            // 5. Calculate total
            int total = naturalRoll + abilityMod + ctx.AttackerProficiency + ctx.MagicBonus;

            // Natural 20 (or within crit range) = auto-hit + critical
            if (isCritical)
            {
                return AttackResult.CriticalHit(naturalRoll, total, state);
            }

            // 6. Compare to target AC
            bool hit = total >= ctx.TargetAC;

            return new AttackResult
            {
                Hit = hit,
                Critical = false,
                NaturalRoll = naturalRoll,
                Total = total,
                State = state
            };
        }

        /// <summary>
        /// Determine the ability modifier for this attack.
        /// STR for melee, DEX for ranged, Finesse = higher of STR/DEX.
        /// </summary>
        private static int GetAbilityModifier(AttackContext ctx)
        {
            int strMod = ctx.AttackerAbilities.GetModifier(Ability.STR);
            int dexMod = ctx.AttackerAbilities.GetModifier(Ability.DEX);

            if (ctx.Weapon != null && ctx.Weapon.IsFinesse)
            {
                // Finesse: use higher of STR or DEX
                return strMod > dexMod ? strMod : dexMod;
            }

            if (ctx.IsRanged)
            {
                return dexMod;
            }

            // Default melee: use STR
            return strMod;
        }

        /// <summary>
        /// Roll a d20 respecting advantage/disadvantage state.
        /// </summary>
        private static int RollD20(AdvantageState state, ref uint seed)
        {
            int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);

            switch (state)
            {
                case AdvantageState.Advantage:
                {
                    int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
                    return roll1 > roll2 ? roll1 : roll2;
                }
                case AdvantageState.Disadvantage:
                {
                    int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
                    return roll1 < roll2 ? roll1 : roll2;
                }
                default:
                    return roll1;
            }
        }
    }
}
