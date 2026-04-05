using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// ScriptableObject defining a character class — hit die, proficiencies, casting, progression.
    /// </summary>
    [CreateAssetMenu(fileName = "NewClass", menuName = "ForeverEngine/RPG/Class Data")]
    public class ClassData : ScriptableObject
    {
        public string Id;
        public string Name;
        public DieType HitDie;
        public Ability[] PrimaryAbilities;
        public Ability SpellcastingAbility;
        public SpellcastingType CastingType;
        public string[] ArmorProficiencies;
        public string[] WeaponProficiencies;
        public string[] ToolProficiencies;
        public Ability[] SaveProficiencies;
        public string[] SkillChoices;
        public int SkillChoiceCount;
        public ClassLevelData[] Progression;
        public Ability[] MulticlassPrereqs;
    }
}
