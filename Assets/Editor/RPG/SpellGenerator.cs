using UnityEditor;
using UnityEngine;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.Editor.RPG
{
    public static class SpellGenerator
    {
        private const string Dir = "Assets/Resources/RPG/Content/Spells";

        // Shorthand aliases — `static readonly` instead of `const` to sidestep
        // a Unity 6 Entities IL post-processor bug (Mono.Cecil GetConstantType
        // fails on cross-assembly enum constants). See
        // feedback_unity6_const_enum_il_postprocessor.md.
        private static readonly ClassFlag War = ClassFlag.Warrior;
        private static readonly ClassFlag Wiz = ClassFlag.Wizard;
        private static readonly ClassFlag Rog = ClassFlag.Rogue;
        private static readonly ClassFlag Clr = ClassFlag.Cleric;
        private static readonly ClassFlag Dru = ClassFlag.Druid;
        private static readonly ClassFlag Brd = ClassFlag.Bard;
        private static readonly ClassFlag Rng = ClassFlag.Ranger;
        private static readonly ClassFlag Pal = ClassFlag.Paladin;
        private static readonly ClassFlag Sor = ClassFlag.Sorcerer;
        private static readonly ClassFlag Wlk = ClassFlag.Warlock;

        [MenuItem("Forever Engine/RPG/Generate Spells")]
        public static void GenerateAll()
        {
            Debug.Log("[SpellGenerator] Generating 205 spells...");

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources/RPG"))
                AssetDatabase.CreateFolder("Assets/Resources", "RPG");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/RPG/Content"))
                AssetDatabase.CreateFolder("Assets/Resources/RPG", "Content");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources/RPG/Content", "Spells");

            // Delete existing spell assets
            var existing = AssetDatabase.FindAssets("t:SpellData", new[] { Dir });
            foreach (string guid in existing)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }

            int count = 0;
            // =====================================================================
            // CANTRIPS (Level 0) — 30 spells
            // =====================================================================

            // --- Damage Cantrips (14) ---
            Save(ref count, DmgCantrip("eldritch_ray", "Eldritch Ray", SpellSchool.Evocation, 1, DieType.D10, DamageType.Force, 120, Wlk));
            Save(ref count, DmgCantrip("flame_dart", "Flame Dart", SpellSchool.Evocation, 1, DieType.D10, DamageType.Fire, 120, Wiz | Sor));
            Save(ref count, SaveCantrip("holy_spark", "Holy Spark", SpellSchool.Evocation, 1, DieType.D8, DamageType.Radiant, 60, Ability.DEX, Clr));
            Save(ref count, DmgCantrip("ray_of_frost", "Glacial Shard", SpellSchool.Evocation, 1, DieType.D8, DamageType.Cold, 60, Wiz | Sor));
            Save(ref count, SaveCantrip("acid_splash", "Acid Splash", SpellSchool.Conjuration, 1, DieType.D6, DamageType.Acid, 60, Ability.DEX, Wiz | Sor));
            Save(ref count, DmgCantrip("shocking_grasp", "Voltaic Touch", SpellSchool.Evocation, 1, DieType.D8, DamageType.Lightning, 5, Wiz | Sor));
            Save(ref count, SaveCantrip("poison_spray", "Toxic Mist", SpellSchool.Conjuration, 1, DieType.D12, DamageType.Poison, 10, Ability.CON, Wiz | Sor | Dru));
            Save(ref count, SaveCantrip("toll_the_dead", "Death Knell", SpellSchool.Necromancy, 1, DieType.D12, DamageType.Necrotic, 60, Ability.WIS, Clr | Wlk));
            Save(ref count, DmgCantrip("chill_touch", "Grasp of Shadows", SpellSchool.Necromancy, 1, DieType.D8, DamageType.Necrotic, 120, Wiz | Sor | Wlk));
            Save(ref count, SaveCantrip("thunderclap", "Thunderclap", SpellSchool.Evocation, 1, DieType.D6, DamageType.Thunder, 5, Ability.CON, Brd | Dru | Sor | Wiz | Wlk));
            Save(ref count, SaveCantrip("word_of_radiance", "Radiant Word", SpellSchool.Evocation, 1, DieType.D6, DamageType.Radiant, 5, Ability.CON, Clr));
            Save(ref count, DmgCantrip("produce_flame", "Conjure Flame", SpellSchool.Conjuration, 1, DieType.D8, DamageType.Fire, 30, Dru));
            Save(ref count, SaveCantrip("thorn_whip", "Thorn Lash", SpellSchool.Transmutation, 1, DieType.D6, DamageType.Piercing, 30, Ability.STR, Dru));
            Save(ref count, DmgCantrip("blade_ward_strike", "Force Strike", SpellSchool.Evocation, 1, DieType.D8, DamageType.Force, 5, Wiz | Sor | Wlk));

            // --- Utility Cantrips (16) ---
            Save(ref count, UtilityCantrip("glow", "Glow", SpellSchool.Evocation, 0, "1_hour", false, Brd | Clr | Sor | Wiz));
            Save(ref count, UtilityCantrip("spectral_hand", "Spectral Hand", SpellSchool.Conjuration, 30, "1_minute", false, Brd | Sor | Wiz | Wlk));
            Save(ref count, UtilityCantrip("minor_trick", "Minor Trick", SpellSchool.Transmutation, 10, "1_hour", false, Brd | Sor | Wiz | Wlk));
            Save(ref count, UtilityCantrip("druidcraft", "Nature's Touch", SpellSchool.Transmutation, 30, "instantaneous", false, Dru));
            Save(ref count, UtilityCantrip("thaumaturgy", "Divine Manifestation", SpellSchool.Transmutation, 30, "1_minute", false, Clr));
            Save(ref count, UtilityCantrip("message", "Whisper", SpellSchool.Transmutation, 120, "1_round", false, Brd | Sor | Wiz));
            Save(ref count, UtilityCantrip("mending", "Mend", SpellSchool.Transmutation, 5, "instantaneous", false, Brd | Clr | Dru | Sor | Wiz));
            Save(ref count, UtilityCantrip("minor_illusion", "Phantom Image", SpellSchool.Illusion, 30, "1_minute", false, Brd | Sor | Wiz | Wlk));
            Save(ref count, UtilityCantrip("friends", "Charm Glance", SpellSchool.Enchantment, 0, "concentration_1_minute", true, Brd | Sor | Wiz | Wlk));
            Save(ref count, UtilityCantrip("guidance", "Guiding Light", SpellSchool.Divination, 5, "concentration_1_minute", true, Clr | Dru));
            Save(ref count, UtilityCantrip("resistance", "Warding Sign", SpellSchool.Abjuration, 5, "concentration_1_minute", true, Clr | Dru));
            Save(ref count, UtilityCantrip("true_strike", "Battle Sight", SpellSchool.Divination, 30, "concentration_1_round", true, Brd | Sor | Wiz | Wlk));
            Save(ref count, UtilityCantrip("spare_the_dying", "Death Ward Cantrip", SpellSchool.Necromancy, 5, "instantaneous", false, Clr));
            Save(ref count, UtilityCantrip("dancing_lights", "Floating Lights", SpellSchool.Evocation, 120, "concentration_1_minute", true, Brd | Sor | Wiz));
            Save(ref count, UtilityCantrip("shape_water", "Shape Water", SpellSchool.Transmutation, 30, "instantaneous", false, Dru | Sor | Wiz));
            Save(ref count, UtilityCantrip("mold_earth", "Shape Earth", SpellSchool.Transmutation, 30, "instantaneous", false, Dru | Sor | Wiz));

            // =====================================================================
            // LEVEL 1 — 35 spells
            // =====================================================================

            // --- Damage L1 (10) ---
            Save(ref count, MagicMissile("arcane_bolt", "Arcane Bolt", Wiz | Sor));
            Save(ref count, DmgSpell("searing_touch", "Searing Touch", 1, SpellSchool.Evocation, 3, DieType.D6, DamageType.Fire, 5, true, Wiz | Sor | Wlk));
            Save(ref count, SaveAoE("shockwave", "Shockwave", 1, SpellSchool.Evocation, 2, DieType.D8, DamageType.Thunder, 0, Ability.CON, AoEShape.Cube, 15, Brd | Dru | Sor | Wiz));
            Save(ref count, DmgSpell("guiding_bolt", "Guiding Strike", 1, SpellSchool.Evocation, 4, DieType.D6, DamageType.Radiant, 120, true, Clr));
            Save(ref count, SaveAoE("burning_hands", "Fan of Flames", 1, SpellSchool.Evocation, 3, DieType.D6, DamageType.Fire, 0, Ability.DEX, AoEShape.Cone, 15, Wiz | Sor));
            Save(ref count, DmgSpell("inflict_wounds", "Necrotic Grasp", 1, SpellSchool.Necromancy, 3, DieType.D10, DamageType.Necrotic, 5, true, Clr));
            Save(ref count, DmgSpell("chromatic_orb", "Elemental Orb", 1, SpellSchool.Evocation, 3, DieType.D8, DamageType.Fire, 90, true, Wiz | Sor));
            Save(ref count, DmgSpell("witch_bolt", "Storm Tether", 1, SpellSchool.Evocation, 1, DieType.D12, DamageType.Lightning, 30, true, Sor | Wiz | Wlk));
            Save(ref count, DmgSpell("ice_knife", "Frost Spike", 1, SpellSchool.Conjuration, 1, DieType.D10, DamageType.Cold, 60, true, Dru | Sor | Wiz));
            Save(ref count, DmgSpell("earth_tremor", "Ground Rupture", 1, SpellSchool.Evocation, 1, DieType.D6, DamageType.Bludgeoning, 0, false, Brd | Dru | Sor | Wiz, Ability.DEX));

            // --- Healing L1 (4) ---
            Save(ref count, HealSpell("mending_touch", "Mending Touch", 1, 1, DieType.D8, 5, Brd | Clr | Dru | Pal | Rng));
            Save(ref count, HealSpellBonus("healing_whisper", "Healing Whisper", 1, 1, DieType.D4, 60, Brd | Clr | Dru));
            Save(ref count, HealSpell("cure_poison", "Purge Toxin", 1, 0, DieType.D4, 5, Clr | Dru | Pal | Rng));
            Save(ref count, HealSpell("goodberry", "Lifeberry", 1, 0, DieType.D4, 5, Dru | Rng, ritual: true));

            // --- Defensive/Utility L1 (12) ---
            Save(ref count, ReactionSpell("arcane_ward", "Arcane Ward", 1, SpellSchool.Abjuration, "1_round", Wiz | Sor));
            Save(ref count, ConcSpell("bless_spell", "Bolster", 1, SpellSchool.Enchantment, 30, "concentration_1_minute", Clr | Pal));
            Save(ref count, ConcSpell("bane_spell", "Hex of Weakness", 1, SpellSchool.Enchantment, 30, "concentration_1_minute", Brd | Clr, Ability.CHA, Condition.None, 0));
            Save(ref count, UtilitySpell("sense_magic", "Sense Magic", 1, SpellSchool.Divination, 0, "concentration_10_minutes", true, Brd | Clr | Dru | Pal | Rng | Sor | Wiz, true));
            Save(ref count, UtilitySpell("arcane_insight", "Arcane Insight", 1, SpellSchool.Divination, 5, "instantaneous", false, Brd | Wiz, true));
            Save(ref count, UtilitySpell("feather_fall", "Gentle Descent", 1, SpellSchool.Transmutation, 60, "1_minute", false, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("comprehend_lang", "Tongues of Understanding", 1, SpellSchool.Divination, 0, "1_hour", false, Brd | Sor | Wiz | Wlk, true));
            Save(ref count, UtilitySpell("disguise_self", "Illusory Guise", 1, SpellSchool.Illusion, 0, "1_hour", false, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("expeditious_retreat", "Swift Stride", 1, SpellSchool.Transmutation, 0, "concentration_10_minutes", true, Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("mage_armor", "Arcane Armor", 1, SpellSchool.Abjuration, 5, "8_hours", false, Wiz | Sor));
            Save(ref count, UtilitySpell("sanctuary", "Sanctuary", 1, SpellSchool.Abjuration, 30, "1_minute", false, Clr));
            Save(ref count, UtilitySpell("longstrider", "Fleet Foot", 1, SpellSchool.Transmutation, 5, "1_hour", false, Brd | Dru | Rng | Wiz));

            // --- Control L1 (9) ---
            Save(ref count, ConditionSpell("slumber", "Slumber", 1, SpellSchool.Enchantment, 90, "1_minute", Brd | Sor | Wiz, Condition.Unconscious, 10));
            Save(ref count, ConcSpell("grasping_vines", "Grasping Vines", 1, SpellSchool.Conjuration, 90, "concentration_1_minute", Dru, Ability.STR, Condition.Restrained, 10));
            Save(ref count, ConcSpell("faerie_fire", "Luminous Outline", 1, SpellSchool.Evocation, 60, "concentration_1_minute", Brd | Dru, Ability.DEX, Condition.None, 0));
            Save(ref count, ConditionSpell("command_spell", "Compel", 1, SpellSchool.Enchantment, 60, "1_round", Clr | Pal, Condition.Prone, 1));
            Save(ref count, ConcSpell("fog_cloud", "Mist Shroud", 1, SpellSchool.Conjuration, 120, "concentration_1_hour", Dru | Rng | Sor | Wiz));
            Save(ref count, ConditionSpell("tashas_laughter", "Uncontrollable Mirth", 1, SpellSchool.Enchantment, 30, "concentration_1_minute", Brd | Wiz, Condition.Prone | Condition.Incapacitated, 10));
            Save(ref count, ConcSpell("hex", "Curse Mark", 1, SpellSchool.Enchantment, 90, "concentration_1_hour", Wlk));
            Save(ref count, ConcSpell("hunters_mark", "Prey Sense", 1, SpellSchool.Divination, 90, "concentration_1_hour", Rng));
            Save(ref count, ConditionSpell("ray_of_sickness", "Wave of Nausea", 1, SpellSchool.Necromancy, 60, "1_round", Sor | Wiz, Condition.Poisoned, 1));

            // =====================================================================
            // LEVEL 2 — 30 spells
            // =====================================================================

            // --- Damage L2 (7) ---
            Save(ref count, DmgSpell("searing_rays", "Searing Rays", 2, SpellSchool.Evocation, 2, DieType.D6, DamageType.Fire, 120, true, Sor | Wiz, upDice: 1, upDie: DieType.D6));
            Save(ref count, SaveAoE("sonic_blast", "Sonic Blast", 2, SpellSchool.Evocation, 3, DieType.D8, DamageType.Thunder, 60, Ability.CON, AoEShape.Sphere, 10, Brd | Sor | Wiz | Wlk));
            Save(ref count, DmgSpell("flame_blade", "Flame Brand", 2, SpellSchool.Evocation, 3, DieType.D6, DamageType.Fire, 0, true, Dru, conc: true, duration: "concentration_10_minutes"));
            Save(ref count, DmgSpell("spiritual_weapon", "Spirit Weapon", 2, SpellSchool.Evocation, 1, DieType.D8, DamageType.Force, 60, true, Clr));
            Save(ref count, DmgSpell("moonbeam", "Lunar Ray", 2, SpellSchool.Evocation, 2, DieType.D10, DamageType.Radiant, 120, false, Dru, Ability.CON, conc: true, duration: "concentration_1_minute"));
            Save(ref count, DmgSpell("cloud_of_daggers", "Blade Tempest", 2, SpellSchool.Conjuration, 4, DieType.D4, DamageType.Slashing, 60, false, Brd | Sor | Wiz | Wlk));
            Save(ref count, DmgSpell("acid_arrow", "Caustic Arrow", 2, SpellSchool.Evocation, 4, DieType.D4, DamageType.Acid, 90, true, Wiz));

            // --- Healing L2 (4) ---
            Save(ref count, HealSpell("lesser_restoration", "Purify", 2, 0, DieType.D4, 5, Brd | Clr | Dru | Pal | Rng));
            Save(ref count, HealSpell("prayer_of_healing", "Prayer of Healing", 2, 2, DieType.D8, 30, Clr, castTime: "10_minutes"));
            Save(ref count, HealSpell("healing_spirit", "Healing Spirit", 2, 1, DieType.D6, 60, Dru | Rng, conc: true, duration: "concentration_1_minute"));
            Save(ref count, UtilitySpell("gentle_repose", "Preserve Remains", 2, SpellSchool.Necromancy, 5, "10_days", false, Clr | Wiz, true));

            // --- Utility L2 (10) ---
            Save(ref count, UtilitySpell("vanish", "Vanish", 2, SpellSchool.Illusion, 5, "concentration_1_hour", true, Brd | Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("open_lock", "Open Lock", 2, SpellSchool.Transmutation, 60, "instantaneous", false, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("blink_step", "Blink Step", 2, SpellSchool.Conjuration, 0, "instantaneous", false, Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("darkvision_spell", "Night Eyes", 2, SpellSchool.Transmutation, 5, "8_hours", false, Dru | Rng | Sor | Wiz));
            Save(ref count, UtilitySpell("see_invisibility", "Reveal Unseen", 2, SpellSchool.Divination, 0, "1_hour", false, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("spider_climb", "Wall Crawler", 2, SpellSchool.Transmutation, 5, "concentration_1_hour", true, Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("levitate", "Levitate", 2, SpellSchool.Transmutation, 60, "concentration_10_minutes", true, Sor | Wiz));
            Save(ref count, UtilitySpell("mirror_image", "Echo Form", 2, SpellSchool.Illusion, 0, "1_minute", false, Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("blur_spell", "Distortion", 2, SpellSchool.Illusion, 0, "concentration_1_minute", true, Sor | Wiz));
            Save(ref count, UtilitySpell("enhance_ability", "Enhance Ability", 2, SpellSchool.Transmutation, 5, "concentration_1_hour", true, Brd | Clr | Dru | Sor));

            // --- Control L2 (9) ---
            Save(ref count, ConditionSpell("paralyze", "Paralyze", 2, SpellSchool.Enchantment, 60, "concentration_1_minute", Brd | Clr | Dru | Sor | Wiz | Wlk, Condition.Paralyzed, 10));
            Save(ref count, ConcSpell("spider_silk", "Spider Silk", 2, SpellSchool.Conjuration, 60, "concentration_1_hour", Sor | Wiz, Ability.DEX, Condition.Restrained, 10));
            Save(ref count, ConcSpell("spike_growth", "Thorn Field", 2, SpellSchool.Transmutation, 150, "concentration_10_minutes", Dru | Rng));
            Save(ref count, ConcSpell("silence_spell", "Zone of Silence", 2, SpellSchool.Illusion, 120, "concentration_10_minutes", Brd | Clr | Rng));
            Save(ref count, ConcSpell("darkness_spell", "Veil of Shadow", 2, SpellSchool.Evocation, 60, "concentration_10_minutes", Sor | Wiz | Wlk));
            Save(ref count, ConditionSpell("blindness_deafness", "Blind/Deafen", 2, SpellSchool.Necromancy, 30, "1_minute", Brd | Clr | Sor | Wiz, Condition.Blinded, 10));
            Save(ref count, ConcSpell("heat_metal", "Searing Metal", 2, SpellSchool.Transmutation, 60, "concentration_1_minute", Brd | Dru));
            Save(ref count, ConditionSpell("crown_of_madness", "Crown of Madness", 2, SpellSchool.Enchantment, 120, "concentration_1_minute", Brd | Sor | Wiz | Wlk, Condition.Charmed, 10));
            Save(ref count, ConcSpell("suggestion_spell", "Suggestion", 2, SpellSchool.Enchantment, 30, "concentration_8_hours", Brd | Sor | Wiz | Wlk, Ability.WIS, Condition.Charmed, 10));

            // =====================================================================
            // LEVEL 3 — 25 spells
            // =====================================================================

            // --- Damage L3 (7) ---
            Save(ref count, SaveAoE("flame_burst", "Flame Burst", 3, SpellSchool.Evocation, 8, DieType.D6, DamageType.Fire, 150, Ability.DEX, AoEShape.Sphere, 20, Sor | Wiz, upDice: 1, upDie: DieType.D6));
            Save(ref count, SaveAoE("thunder_lance", "Thunder Lance", 3, SpellSchool.Evocation, 8, DieType.D6, DamageType.Lightning, 0, Ability.DEX, AoEShape.Line, 100, Sor | Wiz, upDice: 1, upDie: DieType.D6));
            Save(ref count, ConcDmgAoE("radiant_sentinels", "Radiant Sentinels", 3, SpellSchool.Evocation, 3, DieType.D8, DamageType.Radiant, 0, Ability.WIS, AoEShape.Sphere, 15, Clr));
            Save(ref count, DmgSpell("vampiric_touch", "Life Drain", 3, SpellSchool.Necromancy, 3, DieType.D6, DamageType.Necrotic, 5, true, Sor | Wiz | Wlk, conc: true, duration: "concentration_1_minute"));
            Save(ref count, SaveAoE("erupting_earth", "Erupting Earth", 3, SpellSchool.Transmutation, 3, DieType.D12, DamageType.Bludgeoning, 120, Ability.DEX, AoEShape.Cube, 20, Dru | Sor | Wiz));
            Save(ref count, DmgSpell("blinding_smite", "Radiant Smite", 3, SpellSchool.Evocation, 3, DieType.D8, DamageType.Radiant, 0, true, Pal, conc: true, duration: "concentration_1_minute"));
            Save(ref count, SaveAoE("hunger_of_hadar", "Void Maw", 3, SpellSchool.Conjuration, 2, DieType.D6, DamageType.Cold, 150, Ability.DEX, AoEShape.Sphere, 20, Wlk));

            // --- Healing L3 (2) ---
            Save(ref count, HealSpell("resurgence", "Resurgence", 3, 0, DieType.D4, 5, Clr | Pal, castTime: "action", material: true, materialDesc: "Diamond worth 300 gp"));
            Save(ref count, HealSpellBonus("mass_healing_word", "Mass Healing Whisper", 3, 1, DieType.D4, 60, Clr));

            // --- Utility L3 (9) ---
            Save(ref count, ReactionSpell("spell_break", "Spell Break", 3, SpellSchool.Abjuration, "instantaneous", Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("nullify", "Nullify", 3, SpellSchool.Abjuration, 120, "instantaneous", false, Brd | Clr | Dru | Pal | Sor | Wiz | Wlk));
            Save(ref count, ConcSpell("soar", "Soar", 3, SpellSchool.Transmutation, 5, "concentration_10_minutes", Sor | Wiz | Wlk));
            Save(ref count, ConcSpell("quicken", "Quicken", 3, SpellSchool.Transmutation, 30, "concentration_1_minute", Sor | Wiz));
            Save(ref count, UtilitySpell("sending", "Distant Message", 3, SpellSchool.Evocation, 0, "1_round", false, Brd | Clr | Wiz));
            Save(ref count, UtilitySpell("water_breathing", "Aquatic Adaptation", 3, SpellSchool.Transmutation, 30, "24_hours", false, Dru | Rng | Sor | Wiz, true));
            Save(ref count, UtilitySpell("remove_curse", "Lift Curse", 3, SpellSchool.Abjuration, 5, "instantaneous", false, Clr | Pal | Wiz | Wlk));
            Save(ref count, UtilitySpell("tongues", "Universal Speech", 3, SpellSchool.Divination, 5, "1_hour", false, Brd | Clr | Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("protection_from_energy", "Energy Shield", 3, SpellSchool.Abjuration, 5, "concentration_1_hour", true, Clr | Dru | Rng | Sor | Wiz));

            // --- Control L3 (7) ---
            Save(ref count, ConditionSpell("mesmerize", "Mesmerize", 3, SpellSchool.Illusion, 120, "concentration_1_minute", Brd | Sor | Wiz | Wlk, Condition.Charmed | Condition.Incapacitated, 10));
            Save(ref count, ConditionSpell("lethargy", "Lethargy", 3, SpellSchool.Transmutation, 120, "concentration_1_minute", Sor | Wiz, Condition.None, 10));
            Save(ref count, ConcSpell("stinking_cloud", "Noxious Cloud", 3, SpellSchool.Conjuration, 90, "concentration_1_minute", Brd | Sor | Wiz, Ability.CON, Condition.Poisoned, 10));
            Save(ref count, ConditionSpell("bestow_curse", "Bestow Curse", 3, SpellSchool.Necromancy, 5, "concentration_1_minute", Brd | Clr | Wiz, Condition.None, 10));
            Save(ref count, ConcSpell("plant_growth", "Overgrowth", 3, SpellSchool.Transmutation, 150, "instantaneous", Brd | Dru | Rng));
            Save(ref count, ConcSpell("fear_spell", "Wave of Terror", 3, SpellSchool.Illusion, 0, "concentration_1_minute", Brd | Sor | Wiz | Wlk, Ability.WIS, Condition.Frightened, 10));
            Save(ref count, ConcSpell("sleet_storm", "Frozen Deluge", 3, SpellSchool.Conjuration, 150, "concentration_1_minute", Dru | Sor | Wiz, Ability.DEX, Condition.Prone, 1));

            // =====================================================================
            // LEVEL 4 — 20 spells
            // =====================================================================

            // --- Damage L4 (5) ---
            Save(ref count, SaveAoE("frozen_tempest", "Frozen Tempest", 4, SpellSchool.Evocation, 2, DieType.D8, DamageType.Cold, 300, Ability.DEX, AoEShape.Cylinder, 20, Dru | Sor | Wiz, upDice: 1, upDie: DieType.D8));
            Save(ref count, DmgSpell("wither", "Wither", 4, SpellSchool.Necromancy, 8, DieType.D8, DamageType.Necrotic, 30, false, Dru | Sor | Wiz | Wlk, Ability.CON));
            Save(ref count, SaveAoE("vitriolic_sphere", "Corrosive Sphere", 4, SpellSchool.Evocation, 10, DieType.D4, DamageType.Acid, 150, Ability.DEX, AoEShape.Sphere, 20, Sor | Wiz));
            Save(ref count, DmgSpell("phantasmal_killer", "Phantom Slayer", 4, SpellSchool.Illusion, 4, DieType.D10, DamageType.Psychic, 120, false, Wiz, Ability.WIS, conc: true, duration: "concentration_1_minute"));
            Save(ref count, SaveAoE("fire_shield_dmg", "Flame Aegis", 4, SpellSchool.Evocation, 2, DieType.D8, DamageType.Fire, 0, Ability.DEX, AoEShape.None, 0, Wiz, upDice: 0, upDie: DieType.D4));

            // --- Healing L4 (2) ---
            Save(ref count, HealSpell("death_ward", "Death Ward", 4, 0, DieType.D4, 5, Clr | Pal));
            Save(ref count, UtilitySpell("aura_of_life", "Aura of Vitality", 4, SpellSchool.Abjuration, 0, "concentration_10_minutes", true, Clr | Pal));

            // --- Utility L4 (7) ---
            Save(ref count, UtilitySpell("exile", "Exile", 4, SpellSchool.Abjuration, 60, "concentration_1_minute", true, Clr | Pal | Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("portal_step", "Portal Step", 4, SpellSchool.Conjuration, 500, "1_round", false, Brd | Sor | Wiz | Wlk));
            Save(ref count, ConcSpell("shapeshift", "Shapeshift", 4, SpellSchool.Transmutation, 60, "concentration_1_hour", Brd | Dru | Sor | Wiz));
            Save(ref count, UtilitySpell("greater_invisibility", "Greater Vanish", 4, SpellSchool.Illusion, 5, "concentration_1_minute", true, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("stoneskin", "Iron Skin", 4, SpellSchool.Abjuration, 5, "concentration_1_hour", true, Dru | Rng | Sor | Wiz));
            Save(ref count, UtilitySpell("freedom_of_movement", "Unbound Movement", 4, SpellSchool.Abjuration, 5, "1_hour", false, Brd | Clr | Dru | Rng));
            Save(ref count, UtilitySpell("locate_creature", "Creature Sense", 4, SpellSchool.Divination, 0, "concentration_1_hour", true, Brd | Clr | Dru | Pal | Rng | Wiz));

            // --- Control L4 (6) ---
            Save(ref count, ConditionSpell("bewilderment", "Bewilderment", 4, SpellSchool.Enchantment, 90, "concentration_1_minute", Brd | Dru | Sor | Wiz, Condition.Charmed, 10));
            Save(ref count, ConcDmgAoE("flame_wall", "Flame Wall", 4, SpellSchool.Evocation, 5, DieType.D8, DamageType.Fire, 120, Ability.DEX, AoEShape.Line, 60, Dru | Sor | Wiz));
            Save(ref count, ConditionSpell("black_tentacles", "Shadow Tendrils", 4, SpellSchool.Conjuration, 90, "concentration_1_minute", Wiz, Condition.Restrained, 10));
            Save(ref count, ConcSpell("dominate_beast", "Dominate Beast", 4, SpellSchool.Enchantment, 60, "concentration_1_minute", Dru | Sor, Ability.WIS, Condition.Charmed, 10));
            Save(ref count, ConditionSpell("compulsion", "Compulsion", 4, SpellSchool.Enchantment, 30, "concentration_1_minute", Brd, Condition.Charmed, 10));
            Save(ref count, SaveAoE("storm_sphere", "Storm Sphere", 4, SpellSchool.Evocation, 2, DieType.D6, DamageType.Lightning, 150, Ability.STR, AoEShape.Sphere, 20, Sor | Wiz));

            // =====================================================================
            // LEVEL 5 — 20 spells
            // =====================================================================

            // --- Damage L5 (5) ---
            Save(ref count, SaveAoE("frost_cone", "Frost Cone", 5, SpellSchool.Evocation, 8, DieType.D8, DamageType.Cold, 0, Ability.CON, AoEShape.Cone, 60, Sor | Wiz));
            Save(ref count, SaveAoE("heavens_fire", "Heaven's Fire", 5, SpellSchool.Evocation, 4, DieType.D6, DamageType.Fire, 60, Ability.DEX, AoEShape.Cylinder, 10, Clr));
            Save(ref count, DmgSpell("destructive_wave", "Destructive Wave", 5, SpellSchool.Evocation, 5, DieType.D6, DamageType.Thunder, 0, false, Pal, Ability.CON));
            Save(ref count, SaveAoE("insect_plague", "Swarm Plague", 5, SpellSchool.Conjuration, 4, DieType.D10, DamageType.Piercing, 300, Ability.CON, AoEShape.Sphere, 20, Clr | Dru | Sor));
            Save(ref count, SaveAoE("cloudkill", "Death Cloud", 5, SpellSchool.Conjuration, 5, DieType.D8, DamageType.Poison, 120, Ability.CON, AoEShape.Sphere, 20, Sor | Wiz));

            // --- Healing L5 (4) ---
            Save(ref count, HealSpell("mass_mending", "Mass Mending", 5, 3, DieType.D8, 60, Brd | Clr | Dru));
            Save(ref count, HealSpell("resurrection_call", "Resurrection Call", 5, 0, DieType.D4, 5, Brd | Clr | Pal, castTime: "1_hour", material: true, materialDesc: "Diamond worth 500 gp"));
            Save(ref count, HealSpell("greater_restoration", "Greater Purify", 5, 0, DieType.D4, 5, Brd | Clr | Dru));
            Save(ref count, HealSpellBonus("mass_healing_whisper", "Mass Healing Whisper", 5, 1, DieType.D4, 60, Clr));

            // --- Utility L5 (6) ---
            Save(ref count, UtilitySpell("warp_circle", "Warp Circle", 5, SpellSchool.Conjuration, 10, "1_round", false, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("far_sight", "Far Sight", 5, SpellSchool.Divination, 0, "concentration_10_minutes", true, Brd | Clr | Dru | Wiz | Wlk));
            Save(ref count, UtilitySpell("passwall", "Phase Door", 5, SpellSchool.Transmutation, 30, "1_hour", false, Wiz));
            Save(ref count, UtilitySpell("legend_lore", "Legend Lore", 5, SpellSchool.Divination, 0, "instantaneous", false, Brd | Clr | Wiz));
            Save(ref count, UtilitySpell("planar_binding", "Planar Binding", 5, SpellSchool.Abjuration, 60, "24_hours", false, Brd | Clr | Dru | Wiz));
            Save(ref count, ConcSpell("animate_objects", "Animate Objects", 5, SpellSchool.Transmutation, 120, "concentration_1_minute", Brd | Sor | Wiz));

            // --- Control L5 (5) ---
            Save(ref count, ConditionSpell("greater_paralyze", "Greater Paralyze", 5, SpellSchool.Enchantment, 90, "concentration_1_minute", Brd | Sor | Wiz | Wlk, Condition.Paralyzed, 10));
            Save(ref count, ConcSpell("force_barrier", "Force Barrier", 5, SpellSchool.Evocation, 120, "concentration_10_minutes", Wiz));
            Save(ref count, ConcSpell("wall_of_stone", "Stone Barrier", 5, SpellSchool.Evocation, 120, "concentration_10_minutes", Dru | Sor | Wiz));
            Save(ref count, ConditionSpell("contagion", "Plague Touch", 5, SpellSchool.Necromancy, 5, "7_days", Clr | Dru, Condition.Poisoned, 70));
            Save(ref count, ConcSpell("dominate_person", "Dominate Person", 5, SpellSchool.Enchantment, 60, "concentration_1_minute", Brd | Sor | Wiz, Ability.WIS, Condition.Charmed, 10));

            // =====================================================================
            // LEVEL 6 — 15 spells
            // =====================================================================

            // --- Damage L6 (4) ---
            Save(ref count, SaveAoE("arc_storm", "Arc Storm", 6, SpellSchool.Evocation, 10, DieType.D8, DamageType.Lightning, 150, Ability.DEX, AoEShape.Sphere, 20, Sor | Wiz));
            Save(ref count, DmgSpell("annihilate", "Annihilate", 6, SpellSchool.Transmutation, 10, DieType.D6, DamageType.Force, 60, false, Sor | Wiz, Ability.DEX));
            Save(ref count, DmgSpell("bane_touch", "Bane Touch", 6, SpellSchool.Necromancy, 14, DieType.D6, DamageType.Necrotic, 60, false, Clr, Ability.WIS));
            Save(ref count, SaveAoE("sunbeam", "Solar Beam", 6, SpellSchool.Evocation, 6, DieType.D8, DamageType.Radiant, 0, Ability.CON, AoEShape.Line, 60, Dru | Sor | Wiz));

            // --- Healing L6 (3) ---
            Save(ref count, HealSpell("restoration_6", "Restoration", 6, 0, DieType.D4, 60, Clr | Dru));
            Save(ref count, HealSpell("champions_feast", "Champion's Feast", 6, 0, DieType.D4, 30, Clr | Dru, castTime: "10_minutes"));
            Save(ref count, HealSpell("heal_word", "Word of Healing", 6, 10, DieType.D4, 60, Clr));

            // --- Utility L6 (4) ---
            Save(ref count, UtilitySpell("spell_aegis", "Spell Aegis", 6, SpellSchool.Abjuration, 0, "concentration_1_minute", true, Sor | Wiz));
            Save(ref count, UtilitySpell("truesight_spell", "Truesight", 6, SpellSchool.Divination, 5, "1_hour", false, Brd | Clr | Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("wind_walk", "Wind Walk", 6, SpellSchool.Transmutation, 30, "8_hours", false, Dru));
            Save(ref count, UtilitySpell("find_the_path", "Pathfinder", 6, SpellSchool.Divination, 0, "concentration_1_day", true, Brd | Clr | Dru));

            // --- Control L6 (4) ---
            Save(ref count, ConditionSpell("mass_command", "Mass Command", 6, SpellSchool.Enchantment, 60, "24_hours", Brd | Sor | Wiz | Wlk, Condition.Charmed, 100));
            Save(ref count, ConditionSpell("compelled_dance", "Compelled Dance", 6, SpellSchool.Enchantment, 30, "concentration_1_minute", Brd | Wiz, Condition.Charmed, 10));
            Save(ref count, ConcSpell("wall_of_ice", "Ice Barrier", 6, SpellSchool.Evocation, 120, "concentration_10_minutes", Wiz));
            Save(ref count, ConcSpell("wall_of_thorns", "Thorn Barrier", 6, SpellSchool.Conjuration, 120, "concentration_10_minutes", Dru));

            // =====================================================================
            // LEVEL 7 — 12 spells
            // =====================================================================

            // --- Damage L7 (3) ---
            Save(ref count, DmgSpell("death_grasp", "Death Grasp", 7, SpellSchool.Necromancy, 7, DieType.D8, DamageType.Necrotic, 60, false, Sor | Wiz | Wlk, Ability.CON));
            Save(ref count, SaveAoE("inferno", "Inferno", 7, SpellSchool.Evocation, 7, DieType.D10, DamageType.Fire, 150, Ability.DEX, AoEShape.Cube, 10, Clr | Dru | Sor));
            Save(ref count, SaveAoE("prismatic_spray", "Prismatic Spray", 7, SpellSchool.Evocation, 10, DieType.D6, DamageType.Force, 0, Ability.DEX, AoEShape.Cone, 60, Sor | Wiz));

            // --- Healing L7 (2) ---
            Save(ref count, HealSpell("regrowth", "Regrowth", 7, 4, DieType.D8, 5, Brd | Clr | Dru));
            Save(ref count, HealSpell("greater_resurrection_call", "Greater Resurrection Call", 7, 0, DieType.D4, 5, Brd | Clr, castTime: "1_hour", material: true, materialDesc: "Diamond worth 1000 gp"));

            // --- Utility L7 (4) ---
            Save(ref count, UtilitySpell("greater_warp", "Greater Warp", 7, SpellSchool.Conjuration, 10, "instantaneous", false, Brd | Sor | Wiz));
            Save(ref count, UtilitySpell("realm_shift", "Realm Shift", 7, SpellSchool.Conjuration, 5, "instantaneous", false, Clr | Dru | Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("etherealness", "Ethereal Walk", 7, SpellSchool.Transmutation, 0, "8_hours", false, Brd | Clr | Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("symbol_spell", "Arcane Sigil", 7, SpellSchool.Abjuration, 5, "until_dispelled", false, Brd | Clr | Wiz));

            // --- Control L7 (3) ---
            Save(ref count, ConcSpell("prison_of_force", "Prison of Force", 7, SpellSchool.Evocation, 100, "1_hour", Brd | Sor | Wiz | Wlk));
            Save(ref count, ConcSpell("gravity_reversal", "Gravity Reversal", 7, SpellSchool.Transmutation, 100, "concentration_1_minute", Dru | Sor | Wiz, Ability.DEX, Condition.None, 0));
            Save(ref count, ConditionSpell("divine_word", "Divine Word", 7, SpellSchool.Evocation, 30, "instantaneous", Clr, Condition.Stunned, 1));

            // =====================================================================
            // LEVEL 8 — 10 spells
            // =====================================================================

            // --- Damage L8 (3) ---
            Save(ref count, SaveAoE("solar_flare", "Solar Flare", 8, SpellSchool.Evocation, 12, DieType.D6, DamageType.Radiant, 150, Ability.CON, AoEShape.Sphere, 60, Dru | Sor | Wiz));
            Save(ref count, SaveAoE("cataclysm", "Cataclysm", 8, SpellSchool.Evocation, 0, DieType.D4, DamageType.Bludgeoning, 500, Ability.DEX, AoEShape.Sphere, 100, Clr | Dru | Sor));
            Save(ref count, DmgSpell("abi_dalzims_horrid_wilting", "Desiccation", 8, SpellSchool.Necromancy, 12, DieType.D8, DamageType.Necrotic, 150, false, Sor | Wiz, Ability.CON));

            // --- Utility L8 (3) ---
            Save(ref count, UtilitySpell("pocket_realm", "Pocket Realm", 8, SpellSchool.Conjuration, 60, "1_hour", false, Sor | Wiz | Wlk));
            Save(ref count, UtilitySpell("labyrinth", "Labyrinth", 8, SpellSchool.Conjuration, 60, "concentration_10_minutes", true, Wiz));
            Save(ref count, UtilitySpell("thought_shield", "Thought Shield", 8, SpellSchool.Abjuration, 5, "24_hours", false, Brd | Wiz));

            // --- Control L8 (4) ---
            Save(ref count, ConditionSpell("dominate_creature", "Dominate Creature", 8, SpellSchool.Enchantment, 60, "concentration_1_hour", Brd | Sor | Wiz, Condition.Charmed, 600));
            Save(ref count, ConditionSpell("word_of_stunning", "Word of Stunning", 8, SpellSchool.Enchantment, 60, "instantaneous", Sor | Wiz, Condition.Stunned, 1));
            Save(ref count, ConditionSpell("mind_shatter", "Mind Shatter", 8, SpellSchool.Enchantment, 150, "instantaneous", Brd | Dru | Wlk | Wiz, Condition.Stunned, 100));
            Save(ref count, ConcSpell("antipathy_sympathy", "Allure/Repulse", 8, SpellSchool.Enchantment, 60, "10_days", Brd | Dru | Wiz));

            // =====================================================================
            // LEVEL 9 — 8 spells
            // =====================================================================

            // --- Damage L9 (2) ---
            Save(ref count, SaveAoE("cataclysmic_barrage", "Cataclysmic Barrage", 9, SpellSchool.Evocation, 40, DieType.D6, DamageType.Fire, 1000, Ability.DEX, AoEShape.Sphere, 40, Sor | Wiz));
            Save(ref count, ConditionSpell("word_of_death", "Word of Death", 9, SpellSchool.Enchantment, 60, "instantaneous", Brd | Sor | Wiz | Wlk, Condition.None, 0));

            // --- Healing L9 (2) ---
            Save(ref count, HealSpell("mass_restoration", "Mass Restoration", 9, 0, DieType.D4, 60, Clr));
            Save(ref count, HealSpell("true_rebirth", "True Rebirth", 9, 0, DieType.D4, 5, Clr | Dru, castTime: "1_hour", material: true, materialDesc: "Diamond worth 25000 gp"));

            // --- Utility L9 (4) ---
            Save(ref count, UtilitySpell("miracle", "Miracle", 9, SpellSchool.Conjuration, 0, "instantaneous", false, Wiz));
            Save(ref count, UtilitySpell("planar_gate", "Planar Gate", 9, SpellSchool.Conjuration, 60, "concentration_1_minute", true, Clr | Sor | Wiz));
            Save(ref count, UtilitySpell("prescience", "Prescience", 9, SpellSchool.Divination, 5, "8_hours", false, Brd | Dru | Wiz | Wlk));
            Save(ref count, UtilitySpell("temporal_halt", "Temporal Halt", 9, SpellSchool.Transmutation, 0, "instantaneous", false, Sor | Wiz));

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpellGenerator] Generated {count} spell assets");
        }

        // ==================================================================
        // FACTORY HELPERS
        // ==================================================================

        /// <summary>Save a spell asset to disk and increment counter.</summary>
        private static void Save(ref int count, SpellData s)
        {
            AssetDatabase.CreateAsset(s, $"{Dir}/{s.Id}.asset");
            count++;
        }

        /// <summary>Damage cantrip with spell attack roll.</summary>
        private static SpellData DmgCantrip(string id, string name, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, ClassFlag classes)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = 0; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true;
            s.Duration = "instantaneous";
            s.DamageDiceCount = diceCount; s.DamageDie = die; s.DamageType = dmgType;
            s.SpellAttack = true;
            s.Classes = classes;
            return s;
        }

        /// <summary>Damage cantrip with save instead of attack roll.</summary>
        private static SpellData SaveCantrip(string id, string name, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, Ability save, ClassFlag classes)
        {
            var s = DmgCantrip(id, name, school, diceCount, die, dmgType, range, classes);
            s.SpellAttack = false;
            s.HasSave = true;
            s.SaveType = save;
            return s;
        }

        /// <summary>Utility cantrip with no damage.</summary>
        private static SpellData UtilityCantrip(string id, string name, SpellSchool school,
            int range, string duration, bool concentration, ClassFlag classes)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = 0; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true;
            s.Duration = duration; s.Concentration = concentration;
            s.Classes = classes;
            return s;
        }

        /// <summary>Leveled spell with attack roll damage.</summary>
        // NOTE: enum default params use Nullable<T> + ?? sidestep instead of `= EnumValue`
        // because Unity 6 Entities IL post-processor (Mono.Cecil GetConstantType) crashes
        // on cross-assembly enum constants. See feedback_unity6_const_enum_il_postprocessor.md.
        private static SpellData DmgSpell(string id, string name, int level, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, bool spellAttack,
            ClassFlag classes, Ability? save = null,
            int upDice = 0, DieType? upDie = null,
            bool conc = false, string duration = "instantaneous")
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true;
            s.Duration = duration; s.Concentration = conc;
            s.DamageDiceCount = diceCount; s.DamageDie = die; s.DamageType = dmgType;
            s.SpellAttack = spellAttack;
            if (!spellAttack) { s.HasSave = true; s.SaveType = save ?? Ability.STR; }
            s.UpcastDamageDiceCount = upDice; s.UpcastDamageDie = upDie ?? DieType.D4;
            s.Classes = classes;
            return s;
        }

        /// <summary>Save-based AoE damage spell.</summary>
        private static SpellData SaveAoE(string id, string name, int level, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, Ability save,
            AoEShape shape, int areaSize, ClassFlag classes,
            int upDice = 0, DieType? upDie = null)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true; s.Material = true;
            s.Duration = "instantaneous";
            s.DamageDiceCount = diceCount; s.DamageDie = die; s.DamageType = dmgType;
            s.HasSave = true; s.SaveType = save;
            s.AreaShape = shape; s.AreaSize = areaSize;
            s.UpcastDamageDiceCount = upDice; s.UpcastDamageDie = upDie ?? DieType.D4;
            s.Classes = classes;
            return s;
        }

        /// <summary>Concentration AoE damage spell.</summary>
        private static SpellData ConcDmgAoE(string id, string name, int level, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, Ability save,
            AoEShape shape, int areaSize, ClassFlag classes)
        {
            var s = SaveAoE(id, name, level, school, diceCount, die, dmgType, range, save, shape, areaSize, classes);
            s.Duration = "concentration_10_minutes";
            s.Concentration = true;
            return s;
        }

        /// <summary>Magic Missile / Arcane Bolt — special: auto-hit, force, 3 darts.</summary>
        private static SpellData MagicMissile(string id, string name, ClassFlag classes)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = 1; s.School = SpellSchool.Evocation;
            s.CastingTime = "action"; s.Range = 120;
            s.Verbal = true; s.Somatic = true;
            s.Duration = "instantaneous";
            s.DamageDiceCount = 3; s.DamageDie = DieType.D4; s.DamageBonus = 3;
            s.DamageType = DamageType.Force;
            s.UpcastDamageDiceCount = 1; s.UpcastDamageDie = DieType.D4; s.UpcastDamageBonus = 1;
            s.Classes = classes;
            return s;
        }

        /// <summary>Healing spell (action, touch by default).</summary>
        private static SpellData HealSpell(string id, string name, int level,
            int healDice, DieType healDie, int range, ClassFlag classes,
            string castTime = "action", bool ritual = false,
            bool material = false, string materialDesc = "",
            bool conc = false, string duration = "instantaneous")
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = SpellSchool.Evocation;
            s.CastingTime = castTime; s.Range = range;
            s.Verbal = true; s.Somatic = true;
            s.Material = material; s.MaterialDescription = materialDesc;
            s.Duration = duration; s.Concentration = conc;
            s.Ritual = ritual;
            s.HealingDiceCount = healDice; s.HealingDie = healDie;
            s.Classes = classes;
            return s;
        }

        /// <summary>Healing spell cast as bonus action.</summary>
        private static SpellData HealSpellBonus(string id, string name, int level,
            int healDice, DieType healDie, int range, ClassFlag classes)
        {
            var s = HealSpell(id, name, level, healDice, healDie, range, classes);
            s.CastingTime = "bonus_action";
            return s;
        }

        /// <summary>Reaction spell (Shield / Counterspell).</summary>
        private static SpellData ReactionSpell(string id, string name, int level,
            SpellSchool school, string duration, ClassFlag classes)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "reaction"; s.Range = 0;
            s.Verbal = true; s.Somatic = true;
            s.Duration = duration;
            s.Classes = classes;
            return s;
        }

        /// <summary>Utility spell with no damage or healing.</summary>
        private static SpellData UtilitySpell(string id, string name, int level, SpellSchool school,
            int range, string duration, bool concentration, ClassFlag classes, bool ritual = false)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true;
            s.Duration = duration; s.Concentration = concentration;
            s.Ritual = ritual;
            s.Classes = classes;
            return s;
        }

        /// <summary>Concentration utility/buff spell.</summary>
        private static SpellData ConcSpell(string id, string name, int level, SpellSchool school,
            int range, string duration, ClassFlag classes,
            Ability? save = null, Condition? condition = null, int condDuration = 0)
        {
            var s = UtilitySpell(id, name, level, school, range, duration, true, classes);
            var actualCondition = condition ?? Condition.None;
            if (actualCondition != Condition.None)
            {
                s.HasSave = true;
                s.SaveType = save ?? Ability.STR;
                s.AppliesCondition = actualCondition;
                s.ConditionDuration = condDuration;
            }
            return s;
        }

        /// <summary>Spell that applies a condition (save-based).</summary>
        private static SpellData ConditionSpell(string id, string name, int level, SpellSchool school,
            int range, string duration, ClassFlag classes,
            Condition condition, int condDuration,
            Ability? save = null)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true; s.Material = true;
            s.Duration = duration;
            s.Concentration = duration.StartsWith("concentration");
            s.HasSave = true; s.SaveType = save ?? Ability.WIS;
            s.AppliesCondition = condition;
            s.ConditionDuration = condDuration;
            s.Classes = classes;
            return s;
        }
    }
}
