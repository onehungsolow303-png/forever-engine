using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// A single active condition instance with source tracking and duration.
    /// </summary>
    [System.Serializable]
    public struct ActiveCondition
    {
        public Condition Type;
        public int RemainingTurns; // -1 = indefinite (until removed)
        public string Source; // What caused this condition (spell name, ability, etc.)

        public ActiveCondition(Condition type, int durationTurns, string source)
        {
            Type = type;
            RemainingTurns = durationTurns;
            Source = source;
        }

        public bool IsExpired => RemainingTurns == 0;
        public bool IsIndefinite => RemainingTurns < 0;
    }
}
