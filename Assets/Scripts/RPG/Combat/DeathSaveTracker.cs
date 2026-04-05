using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Per-character death save tracker. Implements D&D 5e death saving throw rules:
    /// - 3 successes = stabilized
    /// - 3 failures = dead
    /// - Natural 1 = 2 failures
    /// - Natural 20 = revived with 1 HP
    /// - 10+ = success, less than 10 = failure
    /// </summary>
    [System.Serializable]
    public class DeathSaveTracker
    {
        public int Successes { get; private set; }
        public int Failures { get; private set; }
        public bool IsStabilized { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsActive { get; private set; } // True when at 0 HP and making saves

        /// <summary>
        /// Begin death saves (called when character drops to 0 HP).
        /// </summary>
        public void Begin()
        {
            Successes = 0;
            Failures = 0;
            IsStabilized = false;
            IsDead = false;
            IsActive = true;
        }

        /// <summary>
        /// Roll a death saving throw.
        /// </summary>
        /// <param name="d20Roll">The natural d20 result (1-20).</param>
        /// <returns>The result of the death save.</returns>
        public DeathSaveResult RollDeathSave(int d20Roll)
        {
            if (!IsActive || IsStabilized || IsDead) return DeathSaveResult.Success;

            // Natural 20: revived with 1 HP
            if (d20Roll >= 20)
            {
                Reset();
                return DeathSaveResult.Revived;
            }

            // Natural 1: 2 failures
            if (d20Roll <= 1)
            {
                Failures += 2;
                if (Failures >= 3)
                {
                    IsDead = true;
                    IsActive = false;
                    return DeathSaveResult.Dead;
                }
                return DeathSaveResult.Failure;
            }

            // 10+ = success
            if (d20Roll >= 10)
            {
                Successes++;
                if (Successes >= 3)
                {
                    IsStabilized = true;
                    IsActive = false;
                    return DeathSaveResult.Stabilized;
                }
                return DeathSaveResult.Success;
            }

            // Less than 10 = failure
            Failures++;
            if (Failures >= 3)
            {
                IsDead = true;
                IsActive = false;
                return DeathSaveResult.Dead;
            }
            return DeathSaveResult.Failure;
        }

        /// <summary>
        /// Take damage while at 0 HP. Adds death save failures.
        /// </summary>
        /// <param name="isCritical">If true, adds 2 failures instead of 1.</param>
        /// <returns>The result — could be Dead if failures reach 3.</returns>
        public DeathSaveResult TakeDamageAtZero(bool isCritical)
        {
            if (!IsActive || IsDead) return IsDead ? DeathSaveResult.Dead : DeathSaveResult.Success;

            Failures += isCritical ? 2 : 1;
            if (Failures >= 3)
            {
                IsDead = true;
                IsActive = false;
                return DeathSaveResult.Dead;
            }
            return DeathSaveResult.Failure;
        }

        /// <summary>
        /// Reset death saves (called when healed above 0 HP).
        /// </summary>
        public void Reset()
        {
            Successes = 0;
            Failures = 0;
            IsStabilized = false;
            IsDead = false;
            IsActive = false;
        }
    }
}
