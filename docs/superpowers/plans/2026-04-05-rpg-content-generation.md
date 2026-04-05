# RPG Content Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Generate all RPG game content — 12 classes, 15 species, 205 spells — as ScriptableObject assets via editor scripts.

**Architecture:** 4 editor scripts in Assets/Editor/RPG/. ContentGenerator is the master orchestrator. SpellGenerator runs first (species reference spells), then ClassGenerator, then SpeciesGenerator. All data defined inline as C# — single source of truth.

**Tech Stack:** Unity 6, C# Editor scripts, ScriptableObject, AssetDatabase

---

## Task 1: ContentGenerator.cs (Master Orchestrator)

Create `Assets/Editor/RPG/ContentGenerator.cs`

### Compile check
```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-content.log -quit 2>/dev/null; echo "Exit: $?"
```

### File: `Assets/Editor/RPG/ContentGenerator.cs`

```csharp
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor.RPG
{
    public static class ContentGenerator
    {
        private const string ClassDir = "Assets/Scripts/RPG/Content/Classes";
        private const string SpeciesDir = "Assets/Scripts/RPG/Content/Species";
        private const string SpellDir = "Assets/Scripts/RPG/Content/Spells";

        [MenuItem("Forever Engine/RPG/Generate All Content")]
        public static void GenerateAll()
        {
            Debug.Log("[ContentGenerator] Starting full content generation...");

            EnsureDirectories();

            SpellGenerator.GenerateAll();
            ClassGenerator.GenerateAll();
            SpeciesGenerator.GenerateAll();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate();

            Debug.Log("[ContentGenerator] Content generation complete.");
        }

        private static void EnsureDirectories()
        {
            EnsureFolder("Assets/Scripts/RPG", "Content");
            EnsureFolder("Assets/Scripts/RPG/Content", "Classes");
            EnsureFolder("Assets/Scripts/RPG/Content", "Species");
            EnsureFolder("Assets/Scripts/RPG/Content", "Spells");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string full = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void Validate()
        {
            int errors = 0;

            var classes = AssetDatabase.FindAssets("t:ClassData", new[] { ClassDir });
            var species = AssetDatabase.FindAssets("t:SpeciesData", new[] { SpeciesDir });
            var spells = AssetDatabase.FindAssets("t:SpellData", new[] { SpellDir });

            Debug.Log($"[ContentGenerator] Generated {classes.Length} ClassData assets");
            Debug.Log($"[ContentGenerator] Generated {species.Length} SpeciesData assets");
            Debug.Log($"[ContentGenerator] Generated {spells.Length} SpellData assets");

            if (classes.Length != 12)
            {
                Debug.LogError($"[ContentGenerator] Expected 12 classes, found {classes.Length}");
                errors++;
            }
            if (species.Length != 15)
            {
                Debug.LogError($"[ContentGenerator] Expected 15 species, found {species.Length}");
                errors++;
            }
            if (spells.Length != 205)
            {
                Debug.LogError($"[ContentGenerator] Expected 205 spells, found {spells.Length}");
                errors++;
            }

            // Validate class progressions
            foreach (string guid in classes)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cd = AssetDatabase.LoadAssetAtPath<ForeverEngine.RPG.Character.ClassData>(path);
                if (cd == null) { Debug.LogError($"[ContentGenerator] Null class at {path}"); errors++; continue; }
                if (cd.Progression == null || cd.Progression.Length != 20)
                {
                    Debug.LogError($"[ContentGenerator] {cd.Name} has {cd.Progression?.Length ?? 0} progression entries (expected 20)");
                    errors++;
                }
            }

            // Validate species innate spell references
            foreach (string guid in species)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sd = AssetDatabase.LoadAssetAtPath<ForeverEngine.RPG.Character.SpeciesData>(path);
                if (sd == null) { Debug.LogError($"[ContentGenerator] Null species at {path}"); errors++; continue; }
                if (sd.InnateSpells != null)
                {
                    for (int i = 0; i < sd.InnateSpells.Length; i++)
                    {
                        if (sd.InnateSpells[i] == null)
                        {
                            Debug.LogError($"[ContentGenerator] {sd.Name} has null innate spell at index {i}");
                            errors++;
                        }
                    }
                }
            }

            int total = classes.Length + species.Length + spells.Length;
            Debug.Log($"[ContentGenerator] Validation: {total} total assets, {errors} errors");
        }
    }
}
```

### Done criteria
- File compiles with zero errors
- Git commit

---

## Task 2: SpellGenerator.cs (205 Spells)

Create `Assets/Editor/RPG/SpellGenerator.cs`

### Compile check
```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-content.log -quit 2>/dev/null; echo "Exit: $?"
```

### File: `Assets/Editor/RPG/SpellGenerator.cs`

```csharp
using UnityEditor;
using UnityEngine;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.Editor.RPG
{
    public static class SpellGenerator
    {
        private const string Dir = "Assets/Scripts/RPG/Content/Spells";

        // Shorthand aliases
        private const ClassFlag War = ClassFlag.Warrior;
        private const ClassFlag Wiz = ClassFlag.Wizard;
        private const ClassFlag Rog = ClassFlag.Rogue;
        private const ClassFlag Clr = ClassFlag.Cleric;
        private const ClassFlag Dru = ClassFlag.Druid;
        private const ClassFlag Brd = ClassFlag.Bard;
        private const ClassFlag Rng = ClassFlag.Ranger;
        private const ClassFlag Pal = ClassFlag.Paladin;
        private const ClassFlag Sor = ClassFlag.Sorcerer;
        private const ClassFlag Wlk = ClassFlag.Warlock;

        [MenuItem("Forever Engine/RPG/Generate Spells")]
        public static void GenerateAll()
        {
            Debug.Log("[SpellGenerator] Generating 205 spells...");

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/RPG/Content"))
                AssetDatabase.CreateFolder("Assets/Scripts/RPG", "Content");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Scripts/RPG/Content", "Spells");

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
        private static SpellData DmgSpell(string id, string name, int level, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, bool spellAttack,
            ClassFlag classes, Ability save = Ability.STR,
            int upDice = 0, DieType upDie = DieType.D4,
            bool conc = false, string duration = "instantaneous")
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true;
            s.Duration = duration; s.Concentration = conc;
            s.DamageDiceCount = diceCount; s.DamageDie = die; s.DamageType = dmgType;
            s.SpellAttack = spellAttack;
            if (!spellAttack) { s.HasSave = true; s.SaveType = save; }
            s.UpcastDamageDiceCount = upDice; s.UpcastDamageDie = upDie;
            s.Classes = classes;
            return s;
        }

        /// <summary>Save-based AoE damage spell.</summary>
        private static SpellData SaveAoE(string id, string name, int level, SpellSchool school,
            int diceCount, DieType die, DamageType dmgType, int range, Ability save,
            AoEShape shape, int areaSize, ClassFlag classes,
            int upDice = 0, DieType upDie = DieType.D4)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true; s.Material = true;
            s.Duration = "instantaneous";
            s.DamageDiceCount = diceCount; s.DamageDie = die; s.DamageType = dmgType;
            s.HasSave = true; s.SaveType = save;
            s.AreaShape = shape; s.AreaSize = areaSize;
            s.UpcastDamageDiceCount = upDice; s.UpcastDamageDie = upDie;
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
            Ability save = Ability.STR, Condition condition = Condition.None, int condDuration = 0)
        {
            var s = UtilitySpell(id, name, level, school, range, duration, true, classes);
            if (condition != Condition.None)
            {
                s.HasSave = true;
                s.SaveType = save;
                s.AppliesCondition = condition;
                s.ConditionDuration = condDuration;
            }
            return s;
        }

        /// <summary>Spell that applies a condition (save-based).</summary>
        private static SpellData ConditionSpell(string id, string name, int level, SpellSchool school,
            int range, string duration, ClassFlag classes,
            Condition condition, int condDuration,
            Ability save = Ability.WIS)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.Id = id; s.Name = name; s.Level = level; s.School = school;
            s.CastingTime = "action"; s.Range = range;
            s.Verbal = true; s.Somatic = true; s.Material = true;
            s.Duration = duration;
            s.Concentration = duration.StartsWith("concentration");
            s.HasSave = true; s.SaveType = save;
            s.AppliesCondition = condition;
            s.ConditionDuration = condDuration;
            s.Classes = classes;
            return s;
        }
    }
}
```

### Spell count verification

| Level | Count | Spells |
|-------|-------|--------|
| 0 (Cantrips) | 30 | Eldritch Ray, Flame Dart, Holy Spark, Glacial Shard, Acid Splash, Voltaic Touch, Toxic Mist, Death Knell, Grasp of Shadows, Thunderclap, Radiant Word, Conjure Flame, Thorn Lash, Force Strike, Glow, Spectral Hand, Minor Trick, Nature's Touch, Divine Manifestation, Whisper, Mend, Phantom Image, Charm Glance, Guiding Light, Warding Sign, Battle Sight, Death Ward Cantrip, Floating Lights, Shape Water, Shape Earth |
| 1 | 35 | Arcane Bolt, Searing Touch, Shockwave, Guiding Strike, Fan of Flames, Necrotic Grasp, Elemental Orb, Storm Tether, Frost Spike, Ground Rupture, Mending Touch, Healing Whisper, Purge Toxin, Lifeberry, Arcane Ward, Bolster, Hex of Weakness, Sense Magic, Arcane Insight, Gentle Descent, Tongues of Understanding, Illusory Guise, Swift Stride, Arcane Armor, Sanctuary, Fleet Foot, Slumber, Grasping Vines, Luminous Outline, Compel, Mist Shroud, Uncontrollable Mirth, Curse Mark, Prey Sense, Wave of Nausea |
| 2 | 30 | Searing Rays, Sonic Blast, Flame Brand, Spirit Weapon, Lunar Ray, Blade Tempest, Caustic Arrow, Purify, Prayer of Healing, Healing Spirit, Preserve Remains, Vanish, Open Lock, Blink Step, Night Eyes, Reveal Unseen, Wall Crawler, Levitate, Echo Form, Distortion, Enhance Ability, Paralyze, Spider Silk, Thorn Field, Zone of Silence, Veil of Shadow, Blind/Deafen, Searing Metal, Crown of Madness, Suggestion |
| 3 | 25 | Flame Burst, Thunder Lance, Radiant Sentinels, Life Drain, Erupting Earth, Radiant Smite, Void Maw, Resurgence, Mass Healing Whisper, Spell Break, Nullify, Soar, Quicken, Distant Message, Aquatic Adaptation, Lift Curse, Universal Speech, Energy Shield, Mesmerize, Lethargy, Noxious Cloud, Bestow Curse, Overgrowth, Wave of Terror, Frozen Deluge |
| 4 | 20 | Frozen Tempest, Wither, Corrosive Sphere, Phantom Slayer, Flame Aegis, Death Ward, Aura of Vitality, Exile, Portal Step, Shapeshift, Greater Vanish, Iron Skin, Unbound Movement, Creature Sense, Bewilderment, Flame Wall, Shadow Tendrils, Dominate Beast, Compulsion, Storm Sphere |
| 5 | 20 | Frost Cone, Heaven's Fire, Destructive Wave, Swarm Plague, Death Cloud, Mass Mending, Resurrection Call, Greater Purify, Mass Healing Whisper, Warp Circle, Far Sight, Phase Door, Legend Lore, Planar Binding, Animate Objects, Greater Paralyze, Force Barrier, Stone Barrier, Plague Touch, Dominate Person |
| 6 | 15 | Arc Storm, Annihilate, Bane Touch, Solar Beam, Restoration, Champion's Feast, Word of Healing, Spell Aegis, Truesight, Wind Walk, Pathfinder, Mass Command, Compelled Dance, Ice Barrier, Thorn Barrier |
| 7 | 12 | Death Grasp, Inferno, Prismatic Spray, Regrowth, Greater Resurrection Call, Greater Warp, Realm Shift, Ethereal Walk, Arcane Sigil, Prison of Force, Gravity Reversal, Divine Word |
| 8 | 10 | Solar Flare, Cataclysm, Desiccation, Pocket Realm, Labyrinth, Thought Shield, Dominate Creature, Word of Stunning, Mind Shatter, Allure/Repulse |
| 9 | 8 | Cataclysmic Barrage, Word of Death, Mass Restoration, True Rebirth, Miracle, Planar Gate, Prescience, Temporal Halt |
| **Total** | **205** | |

### Done criteria
- File compiles with zero errors
- All 205 spell definitions present (no placeholders)
- Git commit

---

## Task 3: ClassGenerator.cs (12 Classes)

Create `Assets/Editor/RPG/ClassGenerator.cs`

### Compile check
```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-content.log -quit 2>/dev/null; echo "Exit: $?"
```

### File: `Assets/Editor/RPG/ClassGenerator.cs`

```csharp
using UnityEditor;
using UnityEngine;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.Editor.RPG
{
    public static class ClassGenerator
    {
        private const string Dir = "Assets/Scripts/RPG/Content/Classes";

        [MenuItem("Forever Engine/RPG/Generate Classes")]
        public static void GenerateAll()
        {
            Debug.Log("[ClassGenerator] Generating 12 classes...");

            if (!AssetDatabase.IsValidFolder("Assets/Scripts/RPG/Content"))
                AssetDatabase.CreateFolder("Assets/Scripts/RPG", "Content");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Scripts/RPG/Content", "Classes");

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
```

### Done criteria
- File compiles with zero errors
- All 12 classes present with 20-level progression tables
- Git commit

---

## Task 4: SpeciesGenerator.cs (15 Species)

Create `Assets/Editor/RPG/SpeciesGenerator.cs`

### Compile check
```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-content.log -quit 2>/dev/null; echo "Exit: $?"
```

### File: `Assets/Editor/RPG/SpeciesGenerator.cs`

```csharp
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
                new[] { LoadSpell("phantom_image") }, // Bonus cantrip from wizard list
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
                new[] { LoadSpell("floating_lights") }, // Dancing Lights innate
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
                new[] { LoadSpell("divine_manifestation") }, // Thaumaturgy innate
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
                new[] { LoadSpell("phantom_image") }, // Minor Illusion innate
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
```

### Species summary

| # | Species | Size | Speed | Darkvision | Key Traits |
|---|---------|------|-------|------------|------------|
| 1 | Human | Medium | 30 | 0 | ExtraSkill, ExtraFeat |
| 2 | High Elf | Medium | 30 | 60 | FeyAncestry, Trance, BonusCantrip |
| 3 | Wood Elf | Medium | 35 | 60 | FeyAncestry, Trance, MaskOfTheWild |
| 4 | Dark Elf | Medium | 30 | 120 | FeyAncestry, Trance, SuperiorDarkvision, SunlightSensitivity, DrowMagic |
| 5 | Mountain Dwarf | Medium | 25 | 60 | DwarvenResilience, Stonecunning, DwarvenArmorTraining |
| 6 | Hill Dwarf | Medium | 25 | 60 | DwarvenResilience, Stonecunning, DwarvenToughness |
| 7 | Lightfoot Halfling | Small | 25 | 0 | Lucky, BraveHalfling, NaturallyStealthy |
| 8 | Stout Halfling | Small | 25 | 0 | Lucky, BraveHalfling, StoutResilience |
| 9 | Dragonborn | Medium | 30 | 0 | BreathWeapon, DamageResistance |
| 10 | Tiefling | Medium | 30 | 60 | HellishResistance, InfernalLegacy |
| 11 | Half-Orc | Medium | 30 | 60 | Relentless, SavageAttacks |
| 12 | Forest Gnome | Small | 25 | 60 | GnomeCunning, MinorIllusion |
| 13 | Rock Gnome | Small | 25 | 60 | GnomeCunning, Tinker |
| 14 | Half-Elf | Medium | 30 | 60 | FeyAncestry, ExtraSkill |
| 15 | Changeling | Medium | 30 | 0 | Shapechanger |

### Done criteria
- File compiles with zero errors
- All 15 species present with complete data
- Innate spell references resolve (Tiefling, Dark Elf, High Elf, Forest Gnome)
- Git commit
