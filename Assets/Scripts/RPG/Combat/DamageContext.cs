using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// All inputs needed to resolve damage application.
    /// </summary>
    [System.Serializable]
    public struct DamageContext
    {
        public DiceExpression BaseDamage;
        public DamageType Type;
        public bool Critical; // Double dice count
        public int BonusDamage; // Ability mod + magic + features (e.g., Rage +2)
        public DamageType Resistances; // Target's damage resistances (flagged)
        public DamageType Vulnerabilities; // Target's damage vulnerabilities (flagged)
        public DamageType Immunities; // Target's damage immunities (flagged)
        public int TargetTempHP;
        public int TargetHP;
    }
}
