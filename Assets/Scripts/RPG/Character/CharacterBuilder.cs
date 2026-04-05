using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Factory class for creating new characters from species + class + ability scores.
    /// Handles first-level character creation including:
    /// - Setting base ability scores + species bonuses
    /// - First-level HP (max hit die + CON mod)
    /// - Class proficiencies
    /// - Starting spell slots
    /// - Starting equipment slots
    /// </summary>
    public static class CharacterBuilder
    {
        /// <summary>
        /// Create a new level 1 character.
        /// </summary>
        /// <param name="name">Character name.</param>
        /// <param name="species">Character species.</param>
        /// <param name="startingClass">Starting class.</param>
        /// <param name="baseAbilities">Base ability scores (before species bonuses).</param>
        /// <returns>A fully initialized level 1 CharacterSheet.</returns>
        public static CharacterSheet Create(
            string name,
            SpeciesData species,
            ClassData startingClass,
            AbilityScores baseAbilities)
        {
            var sheet = new CharacterSheet
            {
                Name = name,
                Species = species,
                BaseAbilities = baseAbilities,
                XP = 0
            };

            // Initialize class at level 1
            sheet.ClassLevels.Add(new ClassLevel(startingClass, 1));

            // Calculate effective abilities (base + species)
            sheet.RecalculateEffectiveAbilities();

            // First-level HP: max hit die + CON mod
            int hitDieSides = (int)startingClass.HitDie;
            int conMod = sheet.EffectiveAbilities.GetModifier(Ability.CON);
            int firstLevelHP = hitDieSides + conMod;
            if (firstLevelHP < 1) firstLevelHP = 1;
            sheet.MaxHP = firstLevelHP;
            sheet.HP = firstLevelHP;

            // Add class proficiencies
            if (startingClass.ArmorProficiencies != null)
            {
                foreach (var p in startingClass.ArmorProficiencies)
                    sheet.Proficiencies.Add(p);
            }
            if (startingClass.WeaponProficiencies != null)
            {
                foreach (var p in startingClass.WeaponProficiencies)
                    sheet.Proficiencies.Add(p);
            }
            if (startingClass.ToolProficiencies != null)
            {
                foreach (var p in startingClass.ToolProficiencies)
                    sheet.Proficiencies.Add(p);
            }
            if (startingClass.SaveProficiencies != null)
            {
                foreach (var save in startingClass.SaveProficiencies)
                    sheet.Proficiencies.Add($"Save:{save}");
            }

            // Add species proficiencies
            if (species != null && species.BonusProficiencies != null)
            {
                foreach (var p in species.BonusProficiencies)
                    sheet.Proficiencies.Add(p);
            }

            // Add species languages
            if (species != null && species.Languages != null)
            {
                foreach (var lang in species.Languages)
                    sheet.Proficiencies.Add($"Language:{lang}");
            }

            // Mark special unarmored defense features
            if (startingClass.Id == "monk")
                sheet.Proficiencies.Add("MonkUnarmoredDefense");
            if (startingClass.Id == "barbarian")
                sheet.Proficiencies.Add("BarbarianUnarmoredDefense");

            // Initialize spell slots
            sheet.SpellSlots.RecalculateSlots(sheet.ClassLevels);

            // Initialize class-specific resources
            InitializeClassResources(sheet, startingClass, 1);

            // Calculate initial AC (unarmored)
            sheet.RecalculateAC();

            return sheet;
        }

        /// <summary>
        /// Initialize class-specific resource pools (Rage, Ki, Sorcery Points, etc.).
        /// </summary>
        private static void InitializeClassResources(CharacterSheet sheet, ClassData classData, int level)
        {
            if (classData == null) return;

            switch (classData.Id)
            {
                case "barbarian":
                    sheet.Resources["Rage"] = new ResourcePool(level < 3 ? 2 : level < 6 ? 3 : level < 12 ? 4 : level < 17 ? 5 : 6);
                    break;
                case "monk":
                    if (level >= 2)
                        sheet.Resources["Ki"] = new ResourcePool(level);
                    break;
                case "sorcerer":
                    if (level >= 2)
                        sheet.Resources["SorceryPoints"] = new ResourcePool(level);
                    break;
                case "paladin":
                    sheet.Resources["LayOnHands"] = new ResourcePool(level * 5);
                    break;
                case "bard":
                    int chaMod = sheet.EffectiveAbilities.GetModifier(Ability.CHA);
                    int inspirationUses = chaMod < 1 ? 1 : chaMod;
                    sheet.Resources["BardicInspiration"] = new ResourcePool(inspirationUses);
                    break;
                case "warrior":
                    if (level >= 2)
                        sheet.Resources["ActionSurge"] = new ResourcePool(1);
                    if (level >= 9)
                        sheet.Resources["Indomitable"] = new ResourcePool(1);
                    break;
                case "cleric":
                    if (level >= 2)
                        sheet.Resources["ChannelDivinity"] = new ResourcePool(level < 6 ? 1 : level < 18 ? 2 : 3);
                    break;
                case "druid":
                    if (level >= 2)
                        sheet.Resources["WildShape"] = new ResourcePool(2);
                    break;
            }
        }

        /// <summary>
        /// Create a character with standard array ability scores (15, 14, 13, 12, 10, 8).
        /// Assigns highest scores to class primary abilities.
        /// </summary>
        public static CharacterSheet CreateWithStandardArray(
            string name,
            SpeciesData species,
            ClassData startingClass)
        {
            int[] standardArray = { 15, 14, 13, 12, 10, 8 };
            var abilities = AbilityScores.Default;

            // Assign highest scores to primary abilities
            int arrayIdx = 0;
            if (startingClass.PrimaryAbilities != null)
            {
                foreach (var primary in startingClass.PrimaryAbilities)
                {
                    if (arrayIdx < standardArray.Length)
                    {
                        abilities = abilities.SetScore(primary, standardArray[arrayIdx]);
                        arrayIdx++;
                    }
                }
            }

            // Fill remaining abilities in order
            for (int a = 0; a < 6 && arrayIdx < standardArray.Length; a++)
            {
                var ability = (Ability)a;
                // Skip if already assigned
                bool assigned = false;
                if (startingClass.PrimaryAbilities != null)
                {
                    foreach (var primary in startingClass.PrimaryAbilities)
                    {
                        if (primary == ability) { assigned = true; break; }
                    }
                }
                if (!assigned)
                {
                    abilities = abilities.SetScore(ability, standardArray[arrayIdx]);
                    arrayIdx++;
                }
            }

            return Create(name, species, startingClass, abilities);
        }
    }
}
