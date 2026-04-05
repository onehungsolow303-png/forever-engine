using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Per-character concentration tracker. Manages a single active concentration spell
    /// and handles concentration saves when the character takes damage.
    /// War Caster feat grants advantage on concentration saves.
    /// </summary>
    [System.Serializable]
    public class ConcentrationTracker
    {
        public SpellData ActiveSpell { get; private set; }
        public bool IsConcentrating => ActiveSpell != null;

        /// <summary>
        /// Begin concentrating on a spell. Ends previous concentration if any.
        /// </summary>
        /// <param name="spell">The spell requiring concentration.</param>
        /// <returns>The previously concentrated spell (null if none).</returns>
        public SpellData Begin(SpellData spell)
        {
            var previous = ActiveSpell;
            ActiveSpell = spell;
            return previous;
        }

        /// <summary>
        /// Check concentration after taking damage.
        /// DC = max(10, damageTaken / 2). CON save. War Caster = advantage.
        /// </summary>
        /// <param name="damageTaken">Amount of damage taken.</param>
        /// <param name="abilities">Character's ability scores.</param>
        /// <param name="proficiency">Character's proficiency bonus (added if proficient in CON saves).</param>
        /// <param name="isProficientConSave">Whether the character is proficient in CON saves.</param>
        /// <param name="hasWarCaster">Whether the character has the War Caster feat (advantage).</param>
        /// <param name="seed">RNG seed for dice rolls.</param>
        /// <returns>True if concentration is maintained, false if broken.</returns>
        public bool CheckConcentration(
            int damageTaken,
            AbilityScores abilities,
            int proficiency,
            bool isProficientConSave,
            bool hasWarCaster,
            ref uint seed)
        {
            if (!IsConcentrating) return true;

            int dc = damageTaken / 2;
            if (dc < 10) dc = 10;

            int conMod = abilities.GetModifier(Ability.CON);
            int saveBonus = conMod + (isProficientConSave ? proficiency : 0);

            int roll;
            if (hasWarCaster)
            {
                // Advantage: roll twice, take higher
                int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);
                int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
                roll = roll1 > roll2 ? roll1 : roll2;
            }
            else
            {
                roll = DiceRoller.Roll(1, 20, 0, ref seed);
            }

            int total = roll + saveBonus;
            bool passed = total >= dc;

            if (!passed)
            {
                End();
            }

            return passed;
        }

        /// <summary>
        /// End concentration, clearing the active spell.
        /// </summary>
        public void End()
        {
            ActiveSpell = null;
        }
    }
}
