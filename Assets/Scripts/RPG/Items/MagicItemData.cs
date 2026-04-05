using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// ScriptableObject defining a magic item — rarity, attunement, stat modifiers, effects.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMagicItem", menuName = "ForeverEngine/RPG/Magic Item Data")]
    public class MagicItemData : ScriptableObject
    {
        public string Id;
        public string Name;
        public Rarity Rarity;
        public bool RequiresAttunement;

        [System.Serializable]
        public struct AbilityBonus
        {
            public Ability Ability;
            public int Bonus;
        }

        public AbilityBonus[] AbilityBonuses;
        public int ACBonus;
        public int SaveBonus;
        public string[] EffectTags; // Interpreted at runtime
    }
}
