using System.Collections.Generic;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Per-character condition tracker. Manages active conditions with duration, composites, and advantage queries.
    /// </summary>
    [System.Serializable]
    public class ConditionManager
    {
        /// <summary>
        /// Bitfield of all currently active conditions for fast query.
        /// </summary>
        public Condition ActiveFlags { get; private set; }

        /// <summary>
        /// List of all active condition instances (with source and duration).
        /// </summary>
        private readonly List<ActiveCondition> _conditions = new List<ActiveCondition>();

        /// <summary>
        /// Apply a condition with duration and source tracking.
        /// </summary>
        /// <param name="condition">The condition to apply.</param>
        /// <param name="durationTurns">Duration in turns. -1 for indefinite.</param>
        /// <param name="source">What caused this condition.</param>
        public void Apply(Condition condition, int durationTurns, string source)
        {
            _conditions.Add(new ActiveCondition(condition, durationTurns, source));
            ActiveFlags |= condition;
        }

        /// <summary>
        /// Remove all instances of a condition.
        /// </summary>
        public void Remove(Condition condition)
        {
            _conditions.RemoveAll(c => c.Type == condition);
            RecalculateFlags();
        }

        /// <summary>
        /// Remove a specific condition instance by source.
        /// </summary>
        public void RemoveBySource(string source)
        {
            _conditions.RemoveAll(c => c.Source == source);
            RecalculateFlags();
        }

        /// <summary>
        /// Check if a condition (or any condition in a composite mask) is active.
        /// </summary>
        public bool Has(Condition condition)
        {
            return (ActiveFlags & condition) != 0;
        }

        /// <summary>
        /// Check if the character can act (not incapacitated/stunned/paralyzed/petrified/unconscious).
        /// </summary>
        public bool CanAct => !Has(Condition.CantAct);

        /// <summary>
        /// Tick all condition durations by 1 turn. Returns list of conditions that expired.
        /// </summary>
        public List<Condition> TickDurations()
        {
            var expired = new List<Condition>();

            for (int i = _conditions.Count - 1; i >= 0; i--)
            {
                var c = _conditions[i];
                if (c.IsIndefinite) continue;

                c.RemainingTurns--;
                if (c.RemainingTurns <= 0)
                {
                    expired.Add(c.Type);
                    _conditions.RemoveAt(i);
                }
                else
                {
                    _conditions[i] = c;
                }
            }

            if (expired.Count > 0) RecalculateFlags();
            return expired;
        }

        /// <summary>
        /// Determine advantage/disadvantage sources from conditions.
        /// </summary>
        /// <param name="attackerConditions">Conditions on the attacker.</param>
        /// <param name="targetConditions">Conditions on the target.</param>
        /// <param name="isMelee">Whether the attack is melee.</param>
        /// <param name="isRanged">Whether the attack is ranged.</param>
        /// <param name="advantageReasons">List to populate with advantage reasons.</param>
        /// <param name="disadvantageReasons">List to populate with disadvantage reasons.</param>
        public static void GetAdvantageModifiers(
            Condition attackerConditions,
            Condition targetConditions,
            bool isMelee,
            bool isRanged,
            List<string> advantageReasons,
            List<string> disadvantageReasons)
        {
            // Target conditions that grant advantage to attacker
            if ((targetConditions & Condition.Blinded) != 0)
                advantageReasons.Add("Target is Blinded");
            if ((targetConditions & Condition.Restrained) != 0)
                advantageReasons.Add("Target is Restrained");
            if ((targetConditions & Condition.Stunned) != 0)
                advantageReasons.Add("Target is Stunned");
            if ((targetConditions & Condition.Paralyzed) != 0)
                advantageReasons.Add("Target is Paralyzed");
            if ((targetConditions & Condition.Unconscious) != 0)
                advantageReasons.Add("Target is Unconscious");

            // Attacker conditions that grant advantage
            if ((attackerConditions & Condition.Invisible) != 0)
                advantageReasons.Add("Attacker is Invisible");

            // Attacker conditions that impose disadvantage
            if ((attackerConditions & Condition.Blinded) != 0)
                disadvantageReasons.Add("Attacker is Blinded");
            if ((attackerConditions & Condition.Frightened) != 0)
                disadvantageReasons.Add("Attacker is Frightened");
            if ((attackerConditions & Condition.Poisoned) != 0)
                disadvantageReasons.Add("Attacker is Poisoned");
            if ((attackerConditions & Condition.Restrained) != 0)
                disadvantageReasons.Add("Attacker is Restrained");

            // Prone: disadvantage on ranged, advantage on melee (from attacker being prone)
            if (isRanged && (attackerConditions & Condition.Prone) != 0)
                disadvantageReasons.Add("Attacker is Prone (ranged)");

            // Target prone: advantage on melee within 5ft, disadvantage on ranged
            if (isMelee && (targetConditions & Condition.Prone) != 0)
                advantageReasons.Add("Target is Prone (melee)");
            if (isRanged && (targetConditions & Condition.Prone) != 0)
                disadvantageReasons.Add("Target is Prone (ranged)");

            // Target invisible: disadvantage for attacker
            if ((targetConditions & Condition.Invisible) != 0)
                disadvantageReasons.Add("Target is Invisible");
        }

        /// <summary>
        /// Check if melee attacks auto-crit (target is paralyzed or unconscious within 5ft).
        /// </summary>
        public static bool IsMeleeAutoCrit(Condition targetConditions)
        {
            return (targetConditions & Condition.MeleeAutoCrit) != 0;
        }

        /// <summary>
        /// Get a copy of all active conditions.
        /// </summary>
        public List<ActiveCondition> GetAll()
        {
            return new List<ActiveCondition>(_conditions);
        }

        /// <summary>
        /// Remove all conditions.
        /// </summary>
        public void Clear()
        {
            _conditions.Clear();
            ActiveFlags = Condition.None;
        }

        private void RecalculateFlags()
        {
            ActiveFlags = Condition.None;
            for (int i = 0; i < _conditions.Count; i++)
            {
                ActiveFlags |= _conditions[i].Type;
            }
        }
    }
}
