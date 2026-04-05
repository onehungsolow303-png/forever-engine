using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Output of spell casting resolution.
    /// </summary>
    [System.Serializable]
    public struct CastResult
    {
        public bool Success;
        public int DamageDealt;
        public int HealingDone;
        public Condition ConditionsApplied;
        public int SlotExpended;
        public bool ConcentrationStarted;
        public string FailureReason;

        public static CastResult Failure(string reason)
        {
            return new CastResult
            {
                Success = false,
                FailureReason = reason
            };
        }
    }
}
