using System.Collections.Generic;
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

        // Equipment caches — load any weapon/armor by ID on demand
        private static readonly Dictionary<string, WeaponData> _weaponCache = new();
        private static readonly Dictionary<string, ArmorData> _armorCache = new();

        /// <summary>
        /// Get a weapon by ID from Resources/RPG/Content/Weapons/.
        /// Caches the result for subsequent calls.
        /// </summary>
        public static WeaponData GetWeapon(string id)
        {
            if (_weaponCache.TryGetValue(id, out var w)) return w;
            w = ForeverEngine.AI.SelfHealing.AssetFaultHandler.SafeLoad<WeaponData>($"RPG/Content/Weapons/{id}");
            _weaponCache[id] = w;
            return w;
        }

        /// <summary>
        /// Get armor by ID from Resources/RPG/Content/Armor/.
        /// Caches the result for subsequent calls.
        /// </summary>
        public static ArmorData GetArmor(string id)
        {
            if (_armorCache.TryGetValue(id, out var a)) return a;
            a = ForeverEngine.AI.SelfHealing.AssetFaultHandler.SafeLoad<ArmorData>($"RPG/Content/Armor/{id}");
            _armorCache[id] = a;
            return a;
        }

        /// <summary>
        /// Load class/species ScriptableObject assets from Resources folders.
        /// Call once before creating any premade characters.
        /// </summary>
        private static void EnsureClassesLoaded()
        {
            if (_warrior != null) return;

            _warrior = Resources.Load<ClassData>("RPG/Content/Classes/warrior");
            _wizard  = Resources.Load<ClassData>("RPG/Content/Classes/wizard");
            _cleric  = Resources.Load<ClassData>("RPG/Content/Classes/cleric");
            _rogue   = Resources.Load<ClassData>("RPG/Content/Classes/rogue");

            _human              = Resources.Load<SpeciesData>("RPG/Content/Species/human");
            _highElf            = Resources.Load<SpeciesData>("RPG/Content/Species/high_elf");
            _hillDwarf          = Resources.Load<SpeciesData>("RPG/Content/Species/hill_dwarf");
            _lightfootHalfling  = Resources.Load<SpeciesData>("RPG/Content/Species/lightfoot_halfling");
        }

        public static CharacterSheet CreateHumanWarrior()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(15, 13, 14, 8, 10, 12);
            var sheet = CharacterBuilder.Create("Human Warrior", _human, _warrior, abilities);

            sheet.MainHand = GetWeapon("longsword");
            sheet.OffHand  = GetArmor("shield");
            sheet.Armor    = GetArmor("chain_mail");
            sheet.RecalculateAC();
            return sheet;
        }

        public static CharacterSheet CreateElfWizard()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(8, 14, 13, 15, 10, 12);
            var sheet = CharacterBuilder.Create("Elf Wizard", _highElf, _wizard, abilities);

            sheet.MainHand = GetWeapon("quarterstaff");
            sheet.Armor    = null;
            sheet.RecalculateAC();

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

        public static CharacterSheet CreateDwarfCleric()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(13, 8, 14, 10, 15, 12);
            var sheet = CharacterBuilder.Create("Dwarf Cleric", _hillDwarf, _cleric, abilities);

            sheet.MainHand = GetWeapon("mace");
            sheet.OffHand  = GetArmor("shield");
            sheet.Armor    = GetArmor("scale_mail");
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

        public static CharacterSheet CreateHalflingRogue()
        {
            EnsureClassesLoaded();
            var abilities = new AbilityScores(10, 15, 13, 14, 8, 12);
            var sheet = CharacterBuilder.Create("Halfling Rogue", _lightfootHalfling, _rogue, abilities);

            sheet.MainHand = GetWeapon("shortsword");
            sheet.Armor    = GetArmor("leather");
            sheet.RecalculateAC();

            return sheet;
        }

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
        /// Apply a long rest to the CharacterSheet (the source of truth):
        /// HP → MaxHP, all spell slots restored, concentration dropped,
        /// non-permanent conditions cleared, death saves reset.
        ///
        /// Hit dice recovery is left to the ECS RestSystem path; this
        /// helper is the synchronous dialogue-driven equivalent that
        /// doesn't have access to the ECS world.
        ///
        /// Why this exists: PlayerData.FullRest only mutates PlayerData,
        /// which is the backward-compat layer. Combat reads from
        /// CharacterSheet (via BattleCombatant.FromCharacterSheet), so
        /// rests applied through dialogue never reached the in-combat
        /// HP — the player would talk to Garth, see "HP 12/12" in the
        /// dialogue scrollback, then enter combat at the old stale HP
        /// and get one-shot. Mutating the sheet first and syncing to
        /// PlayerData second matches the convention used elsewhere
        /// (character creation, level up, equip changes).
        /// </summary>
        public static void ApplyLongRestToSheet(CharacterSheet sheet)
        {
            if (sheet == null) return;
            sheet.HP = sheet.MaxHP;
            sheet.TempHP = 0;
            if (sheet.SpellSlots != null) sheet.SpellSlots.RestoreAll();
            if (sheet.Concentration != null && sheet.Concentration.IsConcentrating)
                sheet.Concentration.End();
            if (sheet.Conditions != null) sheet.Conditions.Clear();
            if (sheet.DeathSaves != null) sheet.DeathSaves.Reset();
        }

        /// <summary>
        /// Apply a positive or negative HP delta to the CharacterSheet,
        /// clamped to [0, MaxHP]. Mirrors PlayerData.Heal / TakeDamage so
        /// healing potions and damage emitted by NPCs in dialogue actually
        /// land on the source-of-truth sheet, not just the backward-compat
        /// PlayerData snapshot.
        /// </summary>
        public static void ApplyHpDeltaToSheet(CharacterSheet sheet, int delta)
        {
            if (sheet == null || delta == 0) return;
            int next = sheet.HP + delta;
            if (next < 0) next = 0;
            if (next > sheet.MaxHP) next = sheet.MaxHP;
            sheet.HP = next;
            // Healing above 0 should also reset death saves so the player
            // isn't still mid-dying after an NPC heals them out of combat.
            if (delta > 0 && next > 0 && sheet.DeathSaves != null)
                sheet.DeathSaves.Reset();
        }

        public static string GetClassName(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return "Adventurer";
            return sheet.ClassLevels[0].ClassRef != null ? sheet.ClassLevels[0].ClassRef.Name : "Adventurer";
        }

        public static Ability GetCastingAbility(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return Ability.INT;
            return sheet.ClassLevels[0].ClassRef != null
                ? sheet.ClassLevels[0].ClassRef.SpellcastingAbility
                : Ability.INT;
        }

        public static bool IsProficientConSave(CharacterSheet sheet)
        {
            return sheet != null && sheet.IsProficient("Save:CON");
        }
    }
}
