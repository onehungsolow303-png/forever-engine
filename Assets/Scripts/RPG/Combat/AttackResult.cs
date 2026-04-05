using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Output of attack resolution.
    /// </summary>
    [System.Serializable]
    public struct AttackResult
    {
        public bool Hit;
        public bool Critical;
        public int NaturalRoll;
        public int Total;
        public AdvantageState State;

        public static AttackResult Miss(int naturalRoll, int total, AdvantageState state)
        {
            return new AttackResult
            {
                Hit = false,
                Critical = false,
                NaturalRoll = naturalRoll,
                Total = total,
                State = state
            };
        }

        public static AttackResult CriticalHit(int naturalRoll, int total, AdvantageState state)
        {
            return new AttackResult
            {
                Hit = true,
                Critical = true,
                NaturalRoll = naturalRoll,
                Total = total,
                State = state
            };
        }
    }
}
