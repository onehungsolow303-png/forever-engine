using System.Collections.Generic;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;

namespace ForeverEngine.RPG.Combat
{
    /// <summary>
    /// All inputs needed to resolve a single attack roll.
    /// </summary>
    [System.Serializable]
    public struct AttackContext
    {
        public AbilityScores AttackerAbilities;
        public int AttackerProficiency;
        public WeaponData Weapon; // null for unarmed/spell attacks
        public int TargetAC;
        public Condition AttackerConditions;
        public Condition TargetConditions;
        public bool IsRanged;
        public bool IsMelee;
        public int CritRange; // Default 20, Champion Fighter = 19, Improved = 18
        public int MagicBonus; // Weapon/spell focus magic bonus
        public List<string> AdvantageReasons;
        public List<string> DisadvantageReasons;
    }
}
