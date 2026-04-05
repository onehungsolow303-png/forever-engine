using UnityEngine;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// ScriptableObject defining a weapon — damage, properties, range, magic bonus.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "ForeverEngine/RPG/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string Id;
        public string Name;

        [Header("Damage")]
        public int DamageDiceCount;
        public DieType DamageDie;
        public int DamageBonus;
        public DamageType Type; // Slashing, Piercing, or Bludgeoning

        [Header("Properties")]
        public WeaponProperty Properties;
        public string ProficiencyGroup; // "Simple" or "Martial"

        [Header("Range")]
        public int NormalRange;  // 0 if melee-only
        public int LongRange;   // 0 if melee-only

        [Header("Versatile")]
        public int VersatileDiceCount;
        public DieType VersatileDie;

        [Header("Magic")]
        public int MagicBonus; // 0-3
        public Rarity Rarity;

        /// <summary>
        /// Get the primary damage as a DiceExpression.
        /// </summary>
        public DiceExpression GetDamage()
        {
            return new DiceExpression(DamageDiceCount, DamageDie, DamageBonus);
        }

        /// <summary>
        /// Get versatile (two-handed) damage as a DiceExpression.
        /// </summary>
        public DiceExpression GetVersatileDamage()
        {
            if (!Properties.HasFlag(WeaponProperty.Versatile))
                return GetDamage();
            return new DiceExpression(VersatileDiceCount, VersatileDie, DamageBonus);
        }

        /// <summary>
        /// Whether this weapon can use DEX instead of STR.
        /// </summary>
        public bool IsFinesse => Properties.HasFlag(WeaponProperty.Finesse);

        /// <summary>
        /// Whether this weapon is ranged.
        /// </summary>
        public bool IsRanged => NormalRange > 0;
    }
}
