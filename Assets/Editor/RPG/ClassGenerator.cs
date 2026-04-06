using UnityEditor;
using UnityEngine;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Editor.RPG
{
    public static class ClassGenerator
    {
        private const string Dir = "Assets/Resources/RPG/Content/Classes";

        [MenuItem("Forever Engine/RPG/Generate Classes")]
        public static void GenerateAll()
        {
            Debug.Log("[ClassGenerator] Generating 12 classes...");

            if (!AssetDatabase.IsValidFolder("Assets/Resources/RPG"))
                AssetDatabase.CreateFolder("Assets/Resources", "RPG");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/RPG/Content"))
                AssetDatabase.CreateFolder("Assets/Resources/RPG", "Content");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources/RPG/Content", "Classes");

            // Delete existing
            var existing = AssetDatabase.FindAssets("t:ClassData", new[] { Dir });
            foreach (string guid in existing)
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            int count = 0;
            Save(ref count, MakeWarrior());
            Save(ref count, MakeWizard());
            Save(ref count, MakeRogue());
            Save(ref count, MakeCleric());
            Save(ref count, MakeDruid());
            Save(ref count, MakeBard());
            Save(ref count, MakeRanger());
            Save(ref count, MakePaladin());
            Save(ref count, MakeSorcerer());
            Save(ref count, MakeWarlock());
            Save(ref count, MakeMonk());
            Save(ref count, MakeBarbarian());

            AssetDatabase.SaveAssets();
            Debug.Log($"[ClassGenerator] Generated {count} class assets");
        }

        private static void Save(ref int count, ClassData c)
        {
            AssetDatabase.CreateAsset(c, $"{Dir}/{c.Id}.asset");
            count++;
        }

        // Helper: shorthand level entry
        private static ClassLevelData L(int level, string[] features, bool asi = false, string sub = null)
        {
            return new ClassLevelData(level, features, asi, sub);
        }

        // ======================================================================
        // WARRIOR (Fighter equivalent)
        // ======================================================================
        private static ClassData MakeWarrior()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "warrior"; c.Name = "Warrior";
            c.HitDie = DieType.D10;
            c.PrimaryAbilities = new[] { Ability.STR, Ability.DEX };
            c.SpellcastingAbility = Ability.INT; // subclass casters use INT
            c.CastingType = SpellcastingType.Third;
            c.ArmorProficiencies = new[] { "Light", "Medium", "Heavy", "Shields" };
            c.WeaponProficiencies = new[] { "Simple", "Martial" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.STR, Ability.CON };
            c.SkillChoices = new[] { "Acrobatics", "Animal Handling", "Athletics", "History", "Insight", "Intimidation", "Perception", "Survival" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.STR };
            c.Progression = new[]
            {
                L(1, new[] { "Fighting Style", "Second Wind" }),
                L(2, new[] { "Action Surge (1 use)" }),
                L(3, new[] { "Martial Archetype" }, sub: "Martial Archetype"),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Extra Attack (1)" }),
                L(6, new[] { "Ability Score Improvement" }, asi: true),
                L(7, new[] { "Archetype Feature" }, sub: "Archetype Feature"),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "Indomitable (1 use)" }),
                L(10, new[] { "Archetype Feature" }, sub: "Archetype Feature"),
                L(11, new[] { "Extra Attack (2)" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "Indomitable (2 uses)" }),
                L(14, new[] { "Ability Score Improvement" }, asi: true),
                L(15, new[] { "Archetype Feature" }, sub: "Archetype Feature"),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "Action Surge (2 uses)", "Indomitable (3 uses)" }),
                L(18, new[] { "Archetype Feature" }, sub: "Archetype Feature"),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Extra Attack (3)" })
            };
            return c;
        }

        // ======================================================================
        // WIZARD
        // ======================================================================
        private static ClassData MakeWizard()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "wizard"; c.Name = "Wizard";
            c.HitDie = DieType.D6;
            c.PrimaryAbilities = new[] { Ability.INT };
            c.SpellcastingAbility = Ability.INT;
            c.CastingType = SpellcastingType.Full;
            c.ArmorProficiencies = System.Array.Empty<string>();
            c.WeaponProficiencies = new[] { "Dagger", "Dart", "Sling", "Quarterstaff", "Light Crossbow" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.INT, Ability.WIS };
            c.SkillChoices = new[] { "Arcana", "History", "Insight", "Investigation", "Medicine", "Religion" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.INT };
            c.Progression = new[]
            {
                L(1, new[] { "Spellcasting", "Arcane Recovery" }),
                L(2, new[] { "Arcane Tradition" }, sub: "Arcane Tradition"),
                L(3, new[] { "2nd-Level Spells" }),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "3rd-Level Spells" }),
                L(6, new[] { "Tradition Feature" }, sub: "Tradition Feature"),
                L(7, new[] { "4th-Level Spells" }),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "5th-Level Spells" }),
                L(10, new[] { "Tradition Feature" }, sub: "Tradition Feature"),
                L(11, new[] { "6th-Level Spells" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "7th-Level Spells" }),
                L(14, new[] { "Tradition Feature" }, sub: "Tradition Feature"),
                L(15, new[] { "8th-Level Spells" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "9th-Level Spells" }),
                L(18, new[] { "Spell Mastery" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Signature Spells" })
            };
            return c;
        }

        // ======================================================================
        // ROGUE
        // ======================================================================
        private static ClassData MakeRogue()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "rogue"; c.Name = "Rogue";
            c.HitDie = DieType.D8;
            c.PrimaryAbilities = new[] { Ability.DEX };
            c.SpellcastingAbility = Ability.INT; // Arcane Trickster subclass
            c.CastingType = SpellcastingType.Third;
            c.ArmorProficiencies = new[] { "Light" };
            c.WeaponProficiencies = new[] { "Simple", "Hand Crossbow", "Longsword", "Rapier", "Shortsword" };
            c.ToolProficiencies = new[] { "Thieves' Tools" };
            c.SaveProficiencies = new[] { Ability.DEX, Ability.INT };
            c.SkillChoices = new[] { "Acrobatics", "Athletics", "Deception", "Insight", "Intimidation", "Investigation", "Perception", "Performance", "Persuasion", "Sleight of Hand", "Stealth" };
            c.SkillChoiceCount = 4;
            c.MulticlassPrereqs = new[] { Ability.DEX };
            c.Progression = new[]
            {
                L(1, new[] { "Expertise", "Sneak Attack (1d6)", "Thieves' Cant" }),
                L(2, new[] { "Cunning Action" }),
                L(3, new[] { "Roguish Archetype", "Sneak Attack (2d6)" }, sub: "Roguish Archetype"),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Uncanny Dodge", "Sneak Attack (3d6)" }),
                L(6, new[] { "Expertise" }),
                L(7, new[] { "Evasion", "Sneak Attack (4d6)" }),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "Archetype Feature", "Sneak Attack (5d6)" }, sub: "Archetype Feature"),
                L(10, new[] { "Ability Score Improvement" }, asi: true),
                L(11, new[] { "Reliable Talent", "Sneak Attack (6d6)" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "Archetype Feature", "Sneak Attack (7d6)" }, sub: "Archetype Feature"),
                L(14, new[] { "Blindsense" }),
                L(15, new[] { "Slippery Mind", "Sneak Attack (8d6)" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "Archetype Feature", "Sneak Attack (9d6)" }, sub: "Archetype Feature"),
                L(18, new[] { "Elusive" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Stroke of Luck", "Sneak Attack (10d6)" })
            };
            return c;
        }

        // ======================================================================
        // CLERIC
        // ======================================================================
        private static ClassData MakeCleric()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "cleric"; c.Name = "Cleric";
            c.HitDie = DieType.D8;
            c.PrimaryAbilities = new[] { Ability.WIS };
            c.SpellcastingAbility = Ability.WIS;
            c.CastingType = SpellcastingType.Full;
            c.ArmorProficiencies = new[] { "Light", "Medium", "Shields" };
            c.WeaponProficiencies = new[] { "Simple" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.WIS, Ability.CHA };
            c.SkillChoices = new[] { "History", "Insight", "Medicine", "Persuasion", "Religion" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.WIS };
            c.Progression = new[]
            {
                L(1, new[] { "Spellcasting", "Divine Domain" }, sub: "Divine Domain"),
                L(2, new[] { "Channel Divinity (1 use)", "Domain Feature" }, sub: "Domain Feature"),
                L(3, new[] { "2nd-Level Spells" }),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Destroy Undead (CR 1/2)", "3rd-Level Spells" }),
                L(6, new[] { "Channel Divinity (2 uses)", "Domain Feature" }, sub: "Domain Feature"),
                L(7, new[] { "4th-Level Spells" }),
                L(8, new[] { "Ability Score Improvement", "Destroy Undead (CR 1)", "Domain Feature" }, asi: true, sub: "Domain Feature"),
                L(9, new[] { "5th-Level Spells" }),
                L(10, new[] { "Divine Intervention" }),
                L(11, new[] { "Destroy Undead (CR 2)", "6th-Level Spells" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "7th-Level Spells" }),
                L(14, new[] { "Destroy Undead (CR 3)" }),
                L(15, new[] { "8th-Level Spells" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "Destroy Undead (CR 4)", "9th-Level Spells", "Domain Feature" }, sub: "Domain Feature"),
                L(18, new[] { "Channel Divinity (3 uses)" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Divine Intervention Improvement" })
            };
            return c;
        }

        // ======================================================================
        // DRUID
        // ======================================================================
        private static ClassData MakeDruid()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "druid"; c.Name = "Druid";
            c.HitDie = DieType.D8;
            c.PrimaryAbilities = new[] { Ability.WIS };
            c.SpellcastingAbility = Ability.WIS;
            c.CastingType = SpellcastingType.Full;
            c.ArmorProficiencies = new[] { "Light", "Medium", "Shields (nonmetal)" };
            c.WeaponProficiencies = new[] { "Club", "Dagger", "Dart", "Javelin", "Mace", "Quarterstaff", "Scimitar", "Sickle", "Sling", "Spear" };
            c.ToolProficiencies = new[] { "Herbalism Kit" };
            c.SaveProficiencies = new[] { Ability.INT, Ability.WIS };
            c.SkillChoices = new[] { "Arcana", "Animal Handling", "Insight", "Medicine", "Nature", "Perception", "Religion", "Survival" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.WIS };
            c.Progression = new[]
            {
                L(1, new[] { "Druidic", "Spellcasting" }),
                L(2, new[] { "Wild Shape", "Druid Circle" }, sub: "Druid Circle"),
                L(3, new[] { "2nd-Level Spells" }),
                L(4, new[] { "Ability Score Improvement", "Wild Shape Improvement" }, asi: true),
                L(5, new[] { "3rd-Level Spells" }),
                L(6, new[] { "Circle Feature" }, sub: "Circle Feature"),
                L(7, new[] { "4th-Level Spells" }),
                L(8, new[] { "Ability Score Improvement", "Wild Shape Improvement" }, asi: true),
                L(9, new[] { "5th-Level Spells" }),
                L(10, new[] { "Circle Feature" }, sub: "Circle Feature"),
                L(11, new[] { "6th-Level Spells" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "7th-Level Spells" }),
                L(14, new[] { "Circle Feature" }, sub: "Circle Feature"),
                L(15, new[] { "8th-Level Spells" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "9th-Level Spells" }),
                L(18, new[] { "Timeless Body", "Beast Spells" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Archdruid" })
            };
            return c;
        }

        // ======================================================================
        // BARD
        // ======================================================================
        private static ClassData MakeBard()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "bard"; c.Name = "Bard";
            c.HitDie = DieType.D8;
            c.PrimaryAbilities = new[] { Ability.CHA };
            c.SpellcastingAbility = Ability.CHA;
            c.CastingType = SpellcastingType.Full;
            c.ArmorProficiencies = new[] { "Light" };
            c.WeaponProficiencies = new[] { "Simple", "Hand Crossbow", "Longsword", "Rapier", "Shortsword" };
            c.ToolProficiencies = new[] { "Three Musical Instruments" };
            c.SaveProficiencies = new[] { Ability.DEX, Ability.CHA };
            c.SkillChoices = new[] { "Acrobatics", "Animal Handling", "Arcana", "Athletics", "Deception", "History", "Insight", "Intimidation", "Investigation", "Medicine", "Nature", "Perception", "Performance", "Persuasion", "Religion", "Sleight of Hand", "Stealth", "Survival" };
            c.SkillChoiceCount = 3;
            c.MulticlassPrereqs = new[] { Ability.CHA };
            c.Progression = new[]
            {
                L(1, new[] { "Spellcasting", "Bardic Inspiration (d6)" }),
                L(2, new[] { "Jack of All Trades", "Song of Rest (d6)" }),
                L(3, new[] { "Bard College", "Expertise" }, sub: "Bard College"),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Bardic Inspiration (d8)", "Font of Inspiration" }),
                L(6, new[] { "Countercharm", "College Feature" }, sub: "College Feature"),
                L(7, new[] { "4th-Level Spells" }),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "Song of Rest (d8)", "5th-Level Spells" }),
                L(10, new[] { "Bardic Inspiration (d10)", "Expertise", "Magical Secrets" }),
                L(11, new[] { "6th-Level Spells" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "Song of Rest (d10)", "7th-Level Spells" }),
                L(14, new[] { "Magical Secrets", "College Feature" }, sub: "College Feature"),
                L(15, new[] { "Bardic Inspiration (d12)", "8th-Level Spells" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "Song of Rest (d12)", "9th-Level Spells" }),
                L(18, new[] { "Magical Secrets" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Superior Inspiration" })
            };
            return c;
        }

        // ======================================================================
        // RANGER
        // ======================================================================
        private static ClassData MakeRanger()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "ranger"; c.Name = "Ranger";
            c.HitDie = DieType.D10;
            c.PrimaryAbilities = new[] { Ability.DEX, Ability.WIS };
            c.SpellcastingAbility = Ability.WIS;
            c.CastingType = SpellcastingType.Half;
            c.ArmorProficiencies = new[] { "Light", "Medium", "Shields" };
            c.WeaponProficiencies = new[] { "Simple", "Martial" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.STR, Ability.DEX };
            c.SkillChoices = new[] { "Animal Handling", "Athletics", "Insight", "Investigation", "Nature", "Perception", "Stealth", "Survival" };
            c.SkillChoiceCount = 3;
            c.MulticlassPrereqs = new[] { Ability.DEX, Ability.WIS };
            c.Progression = new[]
            {
                L(1, new[] { "Favored Enemy", "Natural Explorer" }),
                L(2, new[] { "Fighting Style", "Spellcasting" }),
                L(3, new[] { "Ranger Conclave", "Primeval Awareness" }, sub: "Ranger Conclave"),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Extra Attack" }),
                L(6, new[] { "Favored Enemy Improvement", "Natural Explorer Improvement" }),
                L(7, new[] { "Conclave Feature" }, sub: "Conclave Feature"),
                L(8, new[] { "Ability Score Improvement", "Land's Stride" }, asi: true),
                L(9, new[] { "3rd-Level Spells" }),
                L(10, new[] { "Natural Explorer Improvement", "Hide in Plain Sight" }),
                L(11, new[] { "Conclave Feature" }, sub: "Conclave Feature"),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "4th-Level Spells" }),
                L(14, new[] { "Favored Enemy Improvement", "Vanish" }),
                L(15, new[] { "Conclave Feature" }, sub: "Conclave Feature"),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "5th-Level Spells" }),
                L(18, new[] { "Feral Senses" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Foe Slayer" })
            };
            return c;
        }

        // ======================================================================
        // PALADIN
        // ======================================================================
        private static ClassData MakePaladin()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "paladin"; c.Name = "Paladin";
            c.HitDie = DieType.D10;
            c.PrimaryAbilities = new[] { Ability.STR, Ability.CHA };
            c.SpellcastingAbility = Ability.CHA;
            c.CastingType = SpellcastingType.Half;
            c.ArmorProficiencies = new[] { "Light", "Medium", "Heavy", "Shields" };
            c.WeaponProficiencies = new[] { "Simple", "Martial" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.WIS, Ability.CHA };
            c.SkillChoices = new[] { "Athletics", "Insight", "Intimidation", "Medicine", "Persuasion", "Religion" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.STR, Ability.CHA };
            c.Progression = new[]
            {
                L(1, new[] { "Divine Sense", "Lay on Hands" }),
                L(2, new[] { "Fighting Style", "Spellcasting", "Divine Smite" }),
                L(3, new[] { "Divine Health", "Sacred Oath" }, sub: "Sacred Oath"),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Extra Attack" }),
                L(6, new[] { "Aura of Protection" }),
                L(7, new[] { "Oath Feature" }, sub: "Oath Feature"),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "3rd-Level Spells" }),
                L(10, new[] { "Aura of Courage" }),
                L(11, new[] { "Improved Divine Smite" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "4th-Level Spells" }),
                L(14, new[] { "Cleansing Touch" }),
                L(15, new[] { "Oath Feature" }, sub: "Oath Feature"),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "5th-Level Spells" }),
                L(18, new[] { "Aura Improvements" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Oath Feature" }, sub: "Oath Feature")
            };
            return c;
        }

        // ======================================================================
        // SORCERER
        // ======================================================================
        private static ClassData MakeSorcerer()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "sorcerer"; c.Name = "Sorcerer";
            c.HitDie = DieType.D6;
            c.PrimaryAbilities = new[] { Ability.CHA };
            c.SpellcastingAbility = Ability.CHA;
            c.CastingType = SpellcastingType.Full;
            c.ArmorProficiencies = System.Array.Empty<string>();
            c.WeaponProficiencies = new[] { "Dagger", "Dart", "Sling", "Quarterstaff", "Light Crossbow" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.CON, Ability.CHA };
            c.SkillChoices = new[] { "Arcana", "Deception", "Insight", "Intimidation", "Persuasion", "Religion" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.CHA };
            c.Progression = new[]
            {
                L(1, new[] { "Spellcasting", "Sorcerous Origin" }, sub: "Sorcerous Origin"),
                L(2, new[] { "Font of Magic (2 points)" }),
                L(3, new[] { "Metamagic (2 options)", "Font of Magic (3 points)" }),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Font of Magic (5 points)" }),
                L(6, new[] { "Origin Feature" }, sub: "Origin Feature"),
                L(7, new[] { "Font of Magic (7 points)" }),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "Font of Magic (9 points)" }),
                L(10, new[] { "Metamagic (3 options)" }),
                L(11, new[] { "Font of Magic (11 points)" }),
                L(12, new[] { "Ability Score Improvement" }, asi: true),
                L(13, new[] { "Font of Magic (13 points)" }),
                L(14, new[] { "Origin Feature" }, sub: "Origin Feature"),
                L(15, new[] { "Font of Magic (15 points)" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "Metamagic (4 options)", "Font of Magic (17 points)" }),
                L(18, new[] { "Origin Feature" }, sub: "Origin Feature"),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Sorcerous Restoration" })
            };
            return c;
        }

        // ======================================================================
        // WARLOCK
        // ======================================================================
        private static ClassData MakeWarlock()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "warlock"; c.Name = "Warlock";
            c.HitDie = DieType.D8;
            c.PrimaryAbilities = new[] { Ability.CHA };
            c.SpellcastingAbility = Ability.CHA;
            c.CastingType = SpellcastingType.Pact;
            c.ArmorProficiencies = new[] { "Light" };
            c.WeaponProficiencies = new[] { "Simple" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.WIS, Ability.CHA };
            c.SkillChoices = new[] { "Arcana", "Deception", "History", "Intimidation", "Investigation", "Nature", "Religion" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.CHA };
            c.Progression = new[]
            {
                L(1, new[] { "Otherworldly Patron", "Pact Magic" }, sub: "Otherworldly Patron"),
                L(2, new[] { "Eldritch Invocations (2)" }),
                L(3, new[] { "Pact Boon" }),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Eldritch Invocations (3)", "3rd-Level Pact Slots" }),
                L(6, new[] { "Patron Feature" }, sub: "Patron Feature"),
                L(7, new[] { "Eldritch Invocations (4)", "4th-Level Pact Slots" }),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "Eldritch Invocations (5)", "5th-Level Pact Slots" }),
                L(10, new[] { "Patron Feature" }, sub: "Patron Feature"),
                L(11, new[] { "Mystic Arcanum (6th level)" }),
                L(12, new[] { "Ability Score Improvement", "Eldritch Invocations (6)" }, asi: true),
                L(13, new[] { "Mystic Arcanum (7th level)" }),
                L(14, new[] { "Patron Feature" }, sub: "Patron Feature"),
                L(15, new[] { "Mystic Arcanum (8th level)", "Eldritch Invocations (7)" }),
                L(16, new[] { "Ability Score Improvement" }, asi: true),
                L(17, new[] { "Mystic Arcanum (9th level)" }),
                L(18, new[] { "Eldritch Invocations (8)" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Eldritch Master" })
            };
            return c;
        }

        // ======================================================================
        // MONK
        // ======================================================================
        private static ClassData MakeMonk()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "monk"; c.Name = "Monk";
            c.HitDie = DieType.D8;
            c.PrimaryAbilities = new[] { Ability.DEX, Ability.WIS };
            c.SpellcastingAbility = Ability.WIS; // placeholder — monks don't cast
            c.CastingType = SpellcastingType.None;
            c.ArmorProficiencies = System.Array.Empty<string>();
            c.WeaponProficiencies = new[] { "Simple", "Shortsword" };
            c.ToolProficiencies = new[] { "One Artisan Tool or Musical Instrument" };
            c.SaveProficiencies = new[] { Ability.STR, Ability.DEX };
            c.SkillChoices = new[] { "Acrobatics", "Athletics", "History", "Insight", "Religion", "Stealth" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.DEX, Ability.WIS };
            c.Progression = new[]
            {
                L(1, new[] { "Unarmored Defense", "Martial Arts (d4)" }),
                L(2, new[] { "Ki (2 points)", "Flurry of Blows", "Patient Defense", "Step of the Wind", "Unarmored Movement (+10 ft)" }),
                L(3, new[] { "Monastic Tradition", "Ki (3 points)", "Deflect Missiles" }, sub: "Monastic Tradition"),
                L(4, new[] { "Ability Score Improvement", "Ki (4 points)", "Slow Fall" }, asi: true),
                L(5, new[] { "Extra Attack", "Ki (5 points)", "Stunning Strike", "Martial Arts (d6)" }),
                L(6, new[] { "Ki-Empowered Strikes", "Ki (6 points)", "Tradition Feature", "Unarmored Movement (+15 ft)" }, sub: "Tradition Feature"),
                L(7, new[] { "Evasion", "Ki (7 points)", "Stillness of Mind" }),
                L(8, new[] { "Ability Score Improvement", "Ki (8 points)" }, asi: true),
                L(9, new[] { "Ki (9 points)", "Unarmored Movement Improvement" }),
                L(10, new[] { "Ki (10 points)", "Purity of Body", "Unarmored Movement (+20 ft)" }),
                L(11, new[] { "Ki (11 points)", "Tradition Feature", "Martial Arts (d8)" }, sub: "Tradition Feature"),
                L(12, new[] { "Ability Score Improvement", "Ki (12 points)" }, asi: true),
                L(13, new[] { "Ki (13 points)", "Tongue of the Sun and Moon" }),
                L(14, new[] { "Ki (14 points)", "Diamond Soul", "Unarmored Movement (+25 ft)" }),
                L(15, new[] { "Ki (15 points)", "Timeless Body" }),
                L(16, new[] { "Ability Score Improvement", "Ki (16 points)" }, asi: true),
                L(17, new[] { "Ki (17 points)", "Tradition Feature", "Martial Arts (d10)" }, sub: "Tradition Feature"),
                L(18, new[] { "Ki (18 points)", "Empty Body", "Unarmored Movement (+30 ft)" }),
                L(19, new[] { "Ability Score Improvement", "Ki (19 points)" }, asi: true),
                L(20, new[] { "Ki (20 points)", "Perfect Self" })
            };
            return c;
        }

        // ======================================================================
        // BARBARIAN
        // ======================================================================
        private static ClassData MakeBarbarian()
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            c.Id = "barbarian"; c.Name = "Barbarian";
            c.HitDie = DieType.D12;
            c.PrimaryAbilities = new[] { Ability.STR };
            c.SpellcastingAbility = Ability.STR; // placeholder — barbarians don't cast
            c.CastingType = SpellcastingType.None;
            c.ArmorProficiencies = new[] { "Light", "Medium", "Shields" };
            c.WeaponProficiencies = new[] { "Simple", "Martial" };
            c.ToolProficiencies = System.Array.Empty<string>();
            c.SaveProficiencies = new[] { Ability.STR, Ability.CON };
            c.SkillChoices = new[] { "Animal Handling", "Athletics", "Intimidation", "Nature", "Perception", "Survival" };
            c.SkillChoiceCount = 2;
            c.MulticlassPrereqs = new[] { Ability.STR };
            c.Progression = new[]
            {
                L(1, new[] { "Rage (2 uses, +2 damage)", "Unarmored Defense" }),
                L(2, new[] { "Reckless Attack", "Danger Sense" }),
                L(3, new[] { "Primal Path", "Rage (3 uses)" }, sub: "Primal Path"),
                L(4, new[] { "Ability Score Improvement" }, asi: true),
                L(5, new[] { "Extra Attack", "Fast Movement (+10 ft)" }),
                L(6, new[] { "Path Feature", "Rage (4 uses)" }, sub: "Path Feature"),
                L(7, new[] { "Feral Instinct" }),
                L(8, new[] { "Ability Score Improvement" }, asi: true),
                L(9, new[] { "Brutal Critical (1 die)", "Rage (+3 damage)" }),
                L(10, new[] { "Path Feature" }, sub: "Path Feature"),
                L(11, new[] { "Relentless Rage" }),
                L(12, new[] { "Ability Score Improvement", "Rage (5 uses)" }, asi: true),
                L(13, new[] { "Brutal Critical (2 dice)" }),
                L(14, new[] { "Path Feature" }, sub: "Path Feature"),
                L(15, new[] { "Persistent Rage" }),
                L(16, new[] { "Ability Score Improvement", "Rage (+4 damage)" }, asi: true),
                L(17, new[] { "Brutal Critical (3 dice)", "Rage (6 uses)" }),
                L(18, new[] { "Indomitable Might" }),
                L(19, new[] { "Ability Score Improvement" }, asi: true),
                L(20, new[] { "Primal Champion", "Rage (Unlimited)" })
            };
            return c;
        }
    }
}
