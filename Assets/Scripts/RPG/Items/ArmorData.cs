using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// ScriptableObject defining armor — base AC, type, stealth penalty, STR requirement.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArmor", menuName = "ForeverEngine/RPG/Armor Data")]
    public class ArmorData : ScriptableObject
    {
        public string Id;
        public string Name;
        public int BaseAC;
        public ArmorType Type;
        public bool StealthDisadvantage;
        public int StrengthRequirement;
        public int MagicBonus; // 0-3
        public Rarity Rarity;
    }
}
