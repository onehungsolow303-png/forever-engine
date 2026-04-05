using UnityEngine;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// ScriptableObject defining a spell — level, school, range, components, damage, saves, AoE, upcast.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpell", menuName = "ForeverEngine/RPG/Spell Data")]
    public class SpellData : ScriptableObject
    {
        public string Id;
        public string Name;
        public int Level; // 0 = cantrip, 1-9
        public SpellSchool School;

        [Header("Casting")]
        public string CastingTime; // "action", "bonus_action", "reaction", "1_minute", "10_minutes"
        public int Range; // Feet; 0 = self, 5 = touch, 30/60/90/120/150
        public bool Verbal;
        public bool Somatic;
        public bool Material;
        public string MaterialDescription;

        [Header("Duration")]
        public string Duration; // "instantaneous", "1_round", "1_minute", "concentration_1_minute", etc.
        public bool Concentration;
        public bool Ritual;

        [Header("Damage")]
        public int DamageDiceCount;
        public DieType DamageDie;
        public int DamageBonus;
        public DamageType DamageType;

        [Header("Save / Attack")]
        public Ability SaveType; // Which ability the target saves with (ignored if SpellAttack)
        public bool SpellAttack; // true = uses attack roll instead of save
        public bool HasSave; // true = requires a saving throw

        [Header("Area of Effect")]
        public AoEShape AreaShape;
        public int AreaSize; // Radius or length in feet

        [Header("Upcast")]
        public int UpcastDamageDiceCount;
        public DieType UpcastDamageDie;
        public int UpcastDamageBonus;

        [Header("Conditions")]
        public Condition AppliesCondition;
        public int ConditionDuration; // Turns

        [Header("Healing")]
        public int HealingDiceCount;
        public DieType HealingDie;
        public int HealingBonus;

        [Header("Class Access")]
        public ClassFlag Classes; // Which classes can learn this spell

        /// <summary>
        /// Get the base damage as a DiceExpression.
        /// </summary>
        public DiceExpression GetDamage()
        {
            if (DamageDiceCount <= 0) return DiceExpression.None;
            return new DiceExpression(DamageDiceCount, DamageDie, DamageBonus);
        }

        /// <summary>
        /// Get the upcast bonus damage per spell level above base.
        /// </summary>
        public DiceExpression GetUpcastDamage()
        {
            if (UpcastDamageDiceCount <= 0) return DiceExpression.None;
            return new DiceExpression(UpcastDamageDiceCount, UpcastDamageDie, UpcastDamageBonus);
        }

        /// <summary>
        /// Get the healing dice as a DiceExpression.
        /// </summary>
        public DiceExpression GetHealing()
        {
            if (HealingDiceCount <= 0) return DiceExpression.None;
            return new DiceExpression(HealingDiceCount, HealingDie, HealingBonus);
        }

        /// <summary>
        /// Whether this is a cantrip (level 0).
        /// </summary>
        public bool IsCantrip => Level == 0;
    }
}
