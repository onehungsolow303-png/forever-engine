using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using UnityEngine;

namespace ForeverEngine.Demo
{
    /// <summary>
    /// Bridge between RPG CharacterSheet and Demo PlayerData.
    /// Creates premade characters and syncs CharacterSheet state to PlayerData.
    /// </summary>
    public static class RPGBridge
    {
        // Cached ScriptableObjects (loaded once from Resources)
        private static ClassData _warrior, _wizard, _cleric, _rogue;
        private static SpeciesData _human, _highElf, _hillDwarf, _lightfootHalfling;

        // Weapon/Armor templates
        private static WeaponData _longsword, _quarterstaff, _mace, _shortsword;
        private static ArmorData _chainMail, _scaleMail, _leather, _shield;

        /// <summary>
        /// Load all ScriptableObject assets from Resources folders.
        /// Call once before creating any premade characters.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_warrior != null) return;

            // Classes (assets live in Assets/Resources/RPG/Content/Classes/)
            _warrior = Resources.Load<ClassData>("RPG/Content/Classes/warrior");
            _wizard  = Resources.Load<ClassData>("RPG/Content/Classes/wizard");
            _cleric  = Resources.Load<ClassData>("RPG/Content/Classes/cleric");
            _rogue   = Resources.Load<ClassData>("RPG/Content/Classes/rogue");

            // Species (assets live in Assets/Resources/RPG/Content/Species/)
            _human              = Resources.Load<SpeciesData>("RPG/Content/Species/human");
            _highElf            = Resources.Load<SpeciesData>("RPG/Content/Species/high_elf");
            _hillDwarf          = Resources.Load<SpeciesData>("RPG/Content/Species/hill_dwarf");
            _lightfootHalfling  = Resources.Load<SpeciesData>("RPG/Content/Species/lightfoot_halfling");

            // Weapons (TODO: generate weapon/armor assets into Resources/RPG/Content/Weapons/)
            _longsword    = Resources.Load<WeaponData>("RPG/Content/Weapons/longsword");
            _quarterstaff = Resources.Load<WeaponData>("RPG/Content/Weapons/quarterstaff");
            _mace         = Resources.Load<WeaponData>("RPG/Content/Weapons/mace");
            _shortsword   = Resources.Load<WeaponData>("RPG/Content/Weapons/shortsword");

            // Armor (TODO: generate weapon/armor assets into Resources/RPG/Content/Armor/)
            _chainMail = Resources.Load<ArmorData>("RPG/Content/Armor/chain_mail");
            _scaleMail = Resources.Load<ArmorData>("RPG/Content/Armor/scale_mail");
            _leather   = Resources.Load<ArmorData>("RPG/Content/Armor/leather");
            _shield    = Resources.Load<ArmorData>("RPG/Content/Armor/shield");
        }

        /// <summary>
        /// Create the Human Warrior premade: STR 16, CON 15, DEX 14.
        /// Longsword (1d8 slashing), Chain Mail (AC 16), Shield (+2).
        /// </summary>
        public static CharacterSheet CreateHumanWarrior()
        {
            EnsureLoaded();
            // Human gets +1 to all scores (per SpeciesData.AbilityBonuses), so base 15 STR -> 16
            var abilities = new AbilityScores(15, 13, 14, 8, 10, 12);
            var sheet = CharacterBuilder.Create("Human Warrior", _human, _warrior, abilities);

            // Equip starting gear
            sheet.MainHand = _longsword;
            sheet.OffHand  = _shield;
            sheet.Armor    = _chainMail;
            sheet.RecalculateAC();
            return sheet;
        }

        /// <summary>
        /// Create the Elf Wizard premade: INT 16, DEX 16, CON 13.
        /// Quarterstaff (1d6 bludgeoning), no armor.
        /// Cantrips: Flame Dart, Arcane Bolt, Frost Ray + 6 L1 prepared spells.
        /// </summary>
        public static CharacterSheet CreateElfWizard()
        {
            EnsureLoaded();
            // High Elf gets +2 DEX, +1 INT via AbilityBonuses => base 14 DEX -> 16, base 15 INT -> 16
            var abilities = new AbilityScores(8, 14, 13, 15, 10, 12);
            var sheet = CharacterBuilder.Create("Elf Wizard", _highElf, _wizard, abilities);

            sheet.MainHand = _quarterstaff;
            sheet.Armor    = null; // No armor
            sheet.RecalculateAC();

            // Load spells from Resources
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/flame_dart",
                "RPG/Content/Spells/arcane_bolt",
                "RPG/Content/Spells/ray_of_frost"
            }, isCantrip: true);
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/flame_burst",
                "RPG/Content/Spells/arcane_bolt",
                "RPG/Content/Spells/force_barrier",
                "RPG/Content/Spells/slumber",
                "RPG/Content/Spells/mage_armor",
                "RPG/Content/Spells/shockwave"
            }, isCantrip: false);

            return sheet;
        }

        /// <summary>
        /// Create the Dwarf Cleric premade: WIS 16, CON 16, STR 13.
        /// Mace (1d6 bludgeoning), Scale Mail (AC 14+DEX max 2), Shield (+2).
        /// Cantrips: Holy Spark, Glow + L1: Mending Touch, Sacred Shield, Guiding Light.
        /// </summary>
        public static CharacterSheet CreateDwarfCleric()
        {
            EnsureLoaded();
            // Hill Dwarf gets +2 CON, +1 WIS => base 14 CON -> 16, base 15 WIS -> 16
            var abilities = new AbilityScores(13, 8, 14, 10, 15, 12);
            var sheet = CharacterBuilder.Create("Dwarf Cleric", _hillDwarf, _cleric, abilities);

            sheet.MainHand = _mace;
            sheet.OffHand  = _shield;
            sheet.Armor    = _scaleMail;
            sheet.RecalculateAC();

            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/holy_spark",
                "RPG/Content/Spells/glow"
            }, isCantrip: true);
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Content/Spells/mending_touch",
                "RPG/Content/Spells/sanctuary",
                "RPG/Content/Spells/guiding_bolt"
            }, isCantrip: false);

            return sheet;
        }

        /// <summary>
        /// Create the Halfling Rogue premade: DEX 17, CON 13, INT 14.
        /// Shortsword (1d6 piercing), Leather Armor (AC 11+DEX).
        /// No spells.
        /// </summary>
        public static CharacterSheet CreateHalflingRogue()
        {
            EnsureLoaded();
            // Lightfoot Halfling gets +2 DEX, +1 CHA => base 15 DEX -> 17
            var abilities = new AbilityScores(10, 15, 13, 14, 8, 12);
            var sheet = CharacterBuilder.Create("Halfling Rogue", _lightfootHalfling, _rogue, abilities);

            sheet.MainHand = _shortsword;
            sheet.Armor    = _leather;
            sheet.RecalculateAC();

            return sheet;
        }

        /// <summary>
        /// Helper: load spell ScriptableObjects from Resources and add to known/prepared lists.
        /// </summary>
        private static void LoadSpellsFromResources(CharacterSheet sheet, string[] paths, bool isCantrip)
        {
            foreach (var path in paths)
            {
                var spell = Resources.Load<ForeverEngine.RPG.Spells.SpellData>(path);
                if (spell == null)
                {
                    Debug.LogWarning($"[RPGBridge] Spell not found at Resources/{path}");
                    continue;
                }
                sheet.KnownSpells.Add(spell);
                sheet.PreparedSpells.Add(spell);
            }
        }

        /// <summary>
        /// Sync a CharacterSheet's current state into a PlayerData for backward compatibility.
        /// Copies HP, MaxHP, AC, ability scores, speed, attack dice, and level.
        /// </summary>
        public static void SyncPlayerFromCharacter(CharacterSheet sheet, PlayerData player)
        {
            if (sheet == null || player == null) return;

            var snap = sheet.ToStatsSnapshot();
            player.HP           = snap.HP;
            player.MaxHP        = snap.MaxHP;
            player.AC           = snap.AC;
            player.Strength     = snap.Strength;
            player.Dexterity    = snap.Dexterity;
            player.Constitution = snap.Constitution;
            player.Speed        = snap.Speed;
            player.Level        = sheet.TotalLevel;

            // Attack dice string from equipped weapon
            if (sheet.MainHand != null)
            {
                var dmg = sheet.MainHand.GetDamage();
                int bonus = dmg.Bonus + sheet.MainHand.MagicBonus;
                player.AttackDice = bonus != 0
                    ? $"{dmg.Count}d{(int)dmg.Die}{(bonus >= 0 ? "+" : "")}{bonus}"
                    : $"{dmg.Count}d{(int)dmg.Die}";
                player.WeaponName = sheet.MainHand.Name;
            }
            else
            {
                player.AttackDice = "1d1+" + snap.AtkDiceBonus;
                player.WeaponName = "Unarmed";
            }

            if (sheet.Armor != null)
                player.ArmorName = sheet.Armor.Name;
        }

        /// <summary>
        /// Get the primary class name for display (e.g., "Wizard").
        /// </summary>
        public static string GetClassName(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return "Adventurer";
            return sheet.ClassLevels[0].ClassRef != null ? sheet.ClassLevels[0].ClassRef.Name : "Adventurer";
        }

        /// <summary>
        /// Get the casting ability for the primary class.
        /// </summary>
        public static Ability GetCastingAbility(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return Ability.INT;
            return sheet.ClassLevels[0].ClassRef != null
                ? sheet.ClassLevels[0].ClassRef.SpellcastingAbility
                : Ability.INT;
        }

        /// <summary>
        /// Check if the primary class is proficient in CON saves.
        /// Needed for concentration checks.
        /// </summary>
        public static bool IsProficientConSave(CharacterSheet sheet)
        {
            return sheet != null && sheet.IsProficient("Save:CON");
        }
    }
}
