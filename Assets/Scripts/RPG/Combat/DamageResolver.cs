using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Static damage resolver implementing D&D 5e damage rules:
    /// 1. Roll damage dice (if critical, double dice count but not bonus)
    /// 2. Add bonus damage (ability mod + magic + features)
    /// 3. Check immunity -> 0 damage
    /// 4. Check resistance -> halve (round down)
    /// 5. Check vulnerability -> double
    /// 6. Absorb temp HP first
    /// 7. Remainder applied to HP
    /// </summary>
    public static class DamageResolver
    {
        /// <summary>
        /// Apply damage to a target.
        /// </summary>
        /// <param name="ctx">Damage context with all inputs.</param>
        /// <param name="seed">RNG seed.</param>
        /// <returns>Damage result.</returns>
        public static DamageResult Apply(DamageContext ctx, ref uint seed)
        {
            var result = new DamageResult
            {
                TypeApplied = ctx.Type
            };

            // 1. Roll damage dice
            int diceCount = ctx.BaseDamage.Count;
            if (ctx.Critical)
            {
                diceCount *= 2; // Double dice on crit, not bonus
            }

            int diceTotal = DiceRoller.Roll(diceCount, (int)ctx.BaseDamage.Die, 0, ref seed);

            // 2. Add bonus damage (ability mod + magic bonus + features like Rage)
            // Bonus is NOT doubled on crit
            int totalRolled = diceTotal + ctx.BaseDamage.Bonus + ctx.BonusDamage;
            if (totalRolled < 0) totalRolled = 0;

            result.TotalRolled = totalRolled;

            // 3. Check immunity
            if ((ctx.Immunities & ctx.Type) != 0)
            {
                result.AfterResistance = 0;
                result.AbsorbedByTempHP = 0;
                result.HPDamage = 0;
                result.Killed = false;
                return result;
            }

            int afterResist = totalRolled;

            // 4. Check resistance -> halve (round down)
            if ((ctx.Resistances & ctx.Type) != 0)
            {
                afterResist /= 2;
            }

            // 5. Check vulnerability -> double
            if ((ctx.Vulnerabilities & ctx.Type) != 0)
            {
                afterResist *= 2;
            }

            result.AfterResistance = afterResist;

            // 6. Absorb temp HP first
            int remaining = afterResist;
            if (ctx.TargetTempHP > 0)
            {
                int absorbed = remaining <= ctx.TargetTempHP ? remaining : ctx.TargetTempHP;
                result.AbsorbedByTempHP = absorbed;
                remaining -= absorbed;
            }
            else
            {
                result.AbsorbedByTempHP = 0;
            }

            // 7. Remainder applied to HP
            result.HPDamage = remaining;
            result.Killed = (ctx.TargetHP - remaining) <= 0;

            return result;
        }
    }
}
