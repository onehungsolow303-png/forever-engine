using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// Output of damage resolution.
    /// </summary>
    [System.Serializable]
    public struct DamageResult
    {
        public int TotalRolled;
        public int AfterResistance;
        public int AbsorbedByTempHP;
        public int HPDamage;
        public DamageType TypeApplied;
        public bool Killed;
    }
}
