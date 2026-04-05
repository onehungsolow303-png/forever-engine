using UnityEditor;
using UnityEngine;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.Editor.RPG
{
    public static class SpeciesGenerator
    {
        private const string Dir = "Assets/Scripts/RPG/Content/Species";
        private const string SpellDir = "Assets/Scripts/RPG/Content/Spells";

        [MenuItem("Forever Engine/RPG/Generate Species")]
        public static void GenerateAll()
        {
            Debug.Log("[SpeciesGenerator] Generating 15 species...");

            if (!AssetDatabase.IsValidFolder("Assets/Scripts/RPG/Content"))
                AssetDatabase.CreateFolder("Assets/Scripts/RPG", "Content");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Scripts/RPG/Content", "Species");

            // Delete existing
            var existing = AssetDatabase.FindAssets("t:SpeciesData", new[] { Dir });
            foreach (string guid in existing)
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            int count = 0;

            // --- HUMAN ---
            Save(ref count, MakeSpecies(
                "human", "Human",
                new[] { AB(Ability.STR, 1), AB(Ability.DEX, 1), AB(Ability.CON, 1), AB(Ability.INT, 1), AB(Ability.WIS, 1), AB(Ability.CHA, 1) },
                1, 30, 0,
                SpeciesTrait.ExtraSkill | SpeciesTrait.ExtraFeat,
                new[] { "Common", "One Extra" },
                System.Array.Empty<string>(),
                null, null
            ));

            // --- HIGH ELF ---
            var highElf = MakeSpecies(
                "high_elf", "High Elf",
                new[] { AB(Ability.DEX, 2), AB(Ability.INT, 1) },
                1, 30, 60,
                SpeciesTrait.FeyAncestry | SpeciesTrait.Trance | SpeciesTrait.DarkvisionStandard,
                new[] { "Common", "Elvish" },
                new[] { "Longsword", "Shortsword", "Shortbow", "Longbow" },
                new[] { LoadSpell("minor_illusion") }, // Bonus cantrip from wizard list
                null
            );

            // --- WOOD ELF ---
            var woodElf = MakeSpecies(
                "wood_elf", "Wood Elf",
                new[] { AB(Ability.DEX, 2), AB(Ability.WIS, 1) },
                1, 35, 60,
                SpeciesTrait.FeyAncestry | SpeciesTrait.Trance | SpeciesTrait.DarkvisionStandard | SpeciesTrait.MaskOfTheWild,
                new[] { "Common", "Elvish" },
                new[] { "Longsword", "Shortsword", "Shortbow", "Longbow" },
                null, null
            );

            // --- DARK ELF (DROW) ---
            var darkElf = MakeSpecies(
                "dark_elf", "Dark Elf",
                new[] { AB(Ability.DEX, 2), AB(Ability.CHA, 1) },
                1, 30, 120,
                SpeciesTrait.FeyAncestry | SpeciesTrait.Trance | SpeciesTrait.DarkvisionSuperior | SpeciesTrait.SunlightSensitivity | SpeciesTrait.DrowMagic,
                new[] { "Common", "Elvish", "Undercommon" },
                new[] { "Rapier", "Shortsword", "Hand Crossbow" },
                new[] { LoadSpell("dancing_lights") }, // Dancing Lights innate
                null
            );

            // Save elves — subraces reference each other
            highElf.Subraces = null; // Standalone; parent not needed
            woodElf.Subraces = null;
            darkElf.Subraces = null;
            Save(ref count, highElf);
            Save(ref count, woodElf);
            Save(ref count, darkElf);

            // --- MOUNTAIN DWARF ---
            var mountainDwarf = MakeSpecies(
                "mountain_dwarf", "Mountain Dwarf",
                new[] { AB(Ability.STR, 2), AB(Ability.CON, 2) },
                1, 25, 60,
                SpeciesTrait.DwarvenResilience | SpeciesTrait.Stonecunning | SpeciesTrait.DarkvisionStandard | SpeciesTrait.DwarvenArmorTraining,
                new[] { "Common", "Dwarvish" },
                new[] { "Battleaxe", "Handaxe", "Light Hammer", "Warhammer", "Light Armor", "Medium Armor" },
                null, null
            );

            // --- HILL DWARF ---
            var hillDwarf = MakeSpecies(
                "hill_dwarf", "Hill Dwarf",
                new[] { AB(Ability.CON, 2), AB(Ability.WIS, 1) },
                1, 25, 60,
                SpeciesTrait.DwarvenResilience | SpeciesTrait.Stonecunning | SpeciesTrait.DarkvisionStandard | SpeciesTrait.DwarvenToughness,
                new[] { "Common", "Dwarvish" },
                new[] { "Battleaxe", "Handaxe", "Light Hammer", "Warhammer" },
                null, null
            );

            Save(ref count, mountainDwarf);
            Save(ref count, hillDwarf);

            // --- LIGHTFOOT HALFLING ---
            var lightfootHalfling = MakeSpecies(
                "lightfoot_halfling", "Lightfoot Halfling",
                new[] { AB(Ability.DEX, 2), AB(Ability.CHA, 1) },
                0, 25, 0,
                SpeciesTrait.Lucky | SpeciesTrait.BraveHalfling | SpeciesTrait.NaturallyStealthy,
                new[] { "Common", "Halfling" },
                System.Array.Empty<string>(),
                null, null
            );

            // --- STOUT HALFLING ---
            var stoutHalfling = MakeSpecies(
                "stout_halfling", "Stout Halfling",
                new[] { AB(Ability.DEX, 2), AB(Ability.CON, 1) },
                0, 25, 0,
                SpeciesTrait.Lucky | SpeciesTrait.BraveHalfling | SpeciesTrait.StoutResilience,
                new[] { "Common", "Halfling" },
                System.Array.Empty<string>(),
                null, null
            );

            Save(ref count, lightfootHalfling);
            Save(ref count, stoutHalfling);

            // --- DRAGONBORN ---
            Save(ref count, MakeSpecies(
                "dragonborn", "Dragonborn",
                new[] { AB(Ability.STR, 2), AB(Ability.CHA, 1) },
                1, 30, 0,
                SpeciesTrait.BreathWeapon | SpeciesTrait.DamageResistance,
                new[] { "Common", "Draconic" },
                System.Array.Empty<string>(),
                null, null
            ));

            // --- TIEFLING ---
            Save(ref count, MakeSpecies(
                "tiefling", "Tiefling",
                new[] { AB(Ability.INT, 1), AB(Ability.CHA, 2) },
                1, 30, 60,
                SpeciesTrait.HellishResistance | SpeciesTrait.InfernalLegacy | SpeciesTrait.DarkvisionStandard,
                new[] { "Common", "Infernal" },
                System.Array.Empty<string>(),
                new[] { LoadSpell("thaumaturgy") }, // Thaumaturgy innate
                null
            ));

            // --- HALF-ORC ---
            Save(ref count, MakeSpecies(
                "half_orc", "Half-Orc",
                new[] { AB(Ability.STR, 2), AB(Ability.CON, 1) },
                1, 30, 60,
                SpeciesTrait.Relentless | SpeciesTrait.SavageAttacks | SpeciesTrait.DarkvisionStandard,
                new[] { "Common", "Orc" },
                new[] { "Intimidation" },
                null, null
            ));

            // --- FOREST GNOME ---
            var forestGnome = MakeSpecies(
                "forest_gnome", "Forest Gnome",
                new[] { AB(Ability.INT, 2), AB(Ability.DEX, 1) },
                0, 25, 60,
                SpeciesTrait.GnomeCunning | SpeciesTrait.MinorIllusion | SpeciesTrait.DarkvisionStandard,
                new[] { "Common", "Gnomish" },
                System.Array.Empty<string>(),
                new[] { LoadSpell("minor_illusion") }, // Minor Illusion innate
                null
            );

            // --- ROCK GNOME ---
            var rockGnome = MakeSpecies(
                "rock_gnome", "Rock Gnome",
                new[] { AB(Ability.INT, 2), AB(Ability.CON, 1) },
                0, 25, 60,
                SpeciesTrait.GnomeCunning | SpeciesTrait.Tinker | SpeciesTrait.DarkvisionStandard,
                new[] { "Common", "Gnomish" },
                new[] { "Tinker's Tools" },
                null, null
            );

            Save(ref count, forestGnome);
            Save(ref count, rockGnome);

            // --- HALF-ELF ---
            Save(ref count, MakeSpecies(
                "half_elf", "Half-Elf",
                new[] { AB(Ability.CHA, 2), AB(Ability.STR, 1), AB(Ability.CON, 1) }, // +1 to two of player's choice — default STR/CON
                1, 30, 60,
                SpeciesTrait.FeyAncestry | SpeciesTrait.ExtraSkill | SpeciesTrait.DarkvisionStandard,
                new[] { "Common", "Elvish", "One Extra" },
                System.Array.Empty<string>(),
                null, null
            ));

            // --- CHANGELING ---
            Save(ref count, MakeSpecies(
                "changeling", "Changeling",
                new[] { AB(Ability.CHA, 2), AB(Ability.DEX, 1) }, // +1 any — default DEX
                1, 30, 0,
                SpeciesTrait.Shapechanger,
                new[] { "Common", "Two Extra" },
                System.Array.Empty<string>(),
                null, null
            ));

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpeciesGenerator] Generated {count} species assets");
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        private static void Save(ref int count, SpeciesData s)
        {
            AssetDatabase.CreateAsset(s, $"{Dir}/{s.Id}.asset");
            count++;
        }

        private static SpeciesData.AbilityBonus AB(Ability ability, int bonus)
        {
            return new SpeciesData.AbilityBonus { Ability = ability, Bonus = bonus };
        }

        private static SpellData LoadSpell(string id)
        {
            return AssetDatabase.LoadAssetAtPath<SpellData>($"{SpellDir}/{id}.asset");
        }

        private static SpeciesData MakeSpecies(
            string id, string name,
            SpeciesData.AbilityBonus[] bonuses,
            int size, int speed, int darkvision,
            SpeciesTrait traits,
            string[] languages,
            string[] proficiencies,
            SpellData[] innateSpells,
            SpeciesData[] subraces)
        {
            var s = ScriptableObject.CreateInstance<SpeciesData>();
            s.Id = id;
            s.Name = name;
            s.AbilityBonuses = bonuses;
            s.Size = size;
            s.Speed = speed;
            s.DarkvisionRange = darkvision;
            s.Traits = traits;
            s.Languages = languages;
            s.BonusProficiencies = proficiencies;
            s.InnateSpells = innateSpells;
            s.Subraces = subraces;
            return s;
        }
    }
}
