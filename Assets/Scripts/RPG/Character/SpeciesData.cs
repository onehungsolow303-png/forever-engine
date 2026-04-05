using UnityEngine;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// ScriptableObject defining a playable species — ability bonuses, traits, darkvision, speed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "ForeverEngine/RPG/Species Data")]
    public class SpeciesData : ScriptableObject
    {
        public string Id;
        public string Name;

        [System.Serializable]
        public struct AbilityBonus
        {
            public Ability Ability;
            public int Bonus;
        }

        public AbilityBonus[] AbilityBonuses;
        public int Size; // 0 = Small, 1 = Medium
        public int Speed; // Feet (25-35 typical)
        public int DarkvisionRange; // 0, 60, or 120
        public SpeciesTrait Traits;

        [Header("Innate Spellcasting")]
        public SpellData[] InnateSpells;

        [Header("Proficiencies & Languages")]
        public string[] Languages;
        public string[] BonusProficiencies;

        [Header("Subraces")]
        public SpeciesData[] Subraces;
    }
}
