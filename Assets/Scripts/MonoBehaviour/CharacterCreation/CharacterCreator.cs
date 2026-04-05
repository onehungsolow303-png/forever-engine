using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Data;

namespace ForeverEngine.MonoBehaviour.CharacterCreation
{
    /// <summary>
    /// Character creation state machine.
    /// UI panels call Set* methods to build up choices, then call FinalizeCharacter()
    /// to produce a validated CharacterData and fire OnCharacterCreated.
    ///
    /// Does NOT touch ECS directly — Bootstrap/GameBootstrap is responsible for
    /// reading CharacterData and spawning the player entity.
    /// </summary>
    public class CharacterCreator : UnityEngine.MonoBehaviour
    {

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action<CharacterData> OnCharacterCreated;
        public event System.Action<string> OnValidationError;

        // ── Pending selections ────────────────────────────────────────────
        private string _name       = "Adventurer";
        private string _species    = "Human";
        private string _className  = "Fighter";
        private string _subclass   = "";
        private string _background = "Soldier";
        private int    _strength   = 10;
        private int    _dexterity  = 10;
        private int    _constitution = 10;
        private int    _intelligence = 10;
        private int    _wisdom     = 10;
        private int    _charisma   = 10;

        // ── Setters (called by UI panels) ─────────────────────────────────

        public void SetName(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                OnValidationError?.Invoke("Character name cannot be empty.");
                return;
            }
            _name = characterName.Trim();
        }

        public void SetSpecies(string species)
        {
            if (string.IsNullOrEmpty(species))
            {
                OnValidationError?.Invoke("Species cannot be empty.");
                return;
            }
            _species = species;
        }

        public void SetClass(string className)
        {
            if (string.IsNullOrEmpty(className))
            {
                OnValidationError?.Invoke("Class cannot be empty.");
                return;
            }
            _className = className;
        }

        public void SetSubclass(string subclass) => _subclass = subclass ?? "";

        public void SetBackground(string background)
        {
            if (string.IsNullOrEmpty(background))
            {
                OnValidationError?.Invoke("Background cannot be empty.");
                return;
            }
            _background = background;
        }

        /// <summary>
        /// Sets all six ability scores at once.
        /// Keys: "STR", "DEX", "CON", "INT", "WIS", "CHA".
        /// Values must be in [1, 20] range.
        /// </summary>
        public void SetAbilityScores(Dictionary<string, int> scores)
        {
            if (scores == null)
            {
                OnValidationError?.Invoke("Ability scores dictionary is null.");
                return;
            }

            int total = 0;
            foreach (var kvp in scores) total += kvp.Value;

            if (total > 80)
            {
                OnValidationError?.Invoke($"Total ability score points ({total}) exceeds the standard array maximum.");
                return;
            }

            if (scores.TryGetValue("STR", out int str)) _strength     = Clamp(str, 1, 20);
            if (scores.TryGetValue("DEX", out int dex)) _dexterity    = Clamp(dex, 1, 20);
            if (scores.TryGetValue("CON", out int con)) _constitution = Clamp(con, 1, 20);
            if (scores.TryGetValue("INT", out int intScore)) _intelligence = Clamp(intScore, 1, 20);
            if (scores.TryGetValue("WIS", out int wis)) _wisdom       = Clamp(wis, 1, 20);
            if (scores.TryGetValue("CHA", out int cha)) _charisma     = Clamp(cha, 1, 20);
        }

        // ── Finalize ──────────────────────────────────────────────────────

        /// <summary>
        /// Validates selections, calculates derived stats, fires OnCharacterCreated.
        /// Returns null if validation fails (check OnValidationError events for details).
        /// </summary>
        public CharacterData FinalizeCharacter()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                OnValidationError?.Invoke("Character name is required.");
                return null;
            }

            var data = new CharacterData
            {
                characterName = _name,
                species       = _species,
                className     = _className,
                subclass      = _subclass,
                background    = _background,
                level         = 1,
                strength      = _strength,
                dexterity     = _dexterity,
                constitution  = _constitution,
                intelligence  = _intelligence,
                wisdom        = _wisdom,
                charisma      = _charisma
            };

            ApplySpeciesBonus(data);
            CalculateDerivedStats(data);
            ApplyClassDefaults(data);

            OnCharacterCreated?.Invoke(data);
            return data;
        }

        // ── Internal helpers ──────────────────────────────────────────────

        private static void ApplySpeciesBonus(CharacterData data)
        {
            // Standard species ability score bonuses (D&D 5e SRD)
            switch (data.species.ToLowerInvariant())
            {
                case "human":
                    data.strength++;     data.dexterity++;  data.constitution++;
                    data.intelligence++; data.wisdom++;     data.charisma++;
                    break;
                case "elf":
                    data.dexterity += 2;
                    break;
                case "dwarf":
                    data.constitution += 2;
                    break;
                case "halfling":
                    data.dexterity += 2;
                    break;
                case "gnome":
                    data.intelligence += 2;
                    break;
                case "half-orc":
                    data.strength += 2;
                    data.constitution++;
                    break;
                case "tiefling":
                    data.intelligence++;
                    data.charisma += 2;
                    break;
                // TODO: expand species list in Plan 5
            }

            // Clamp all scores after bonuses
            data.strength     = Clamp(data.strength,     1, 20);
            data.dexterity    = Clamp(data.dexterity,    1, 20);
            data.constitution = Clamp(data.constitution, 1, 20);
            data.intelligence = Clamp(data.intelligence, 1, 20);
            data.wisdom       = Clamp(data.wisdom,       1, 20);
            data.charisma     = Clamp(data.charisma,     1, 20);
        }

        private static void CalculateDerivedStats(CharacterData data)
        {
            data.proficiencyBonus = InfinityRPGData.GetProficiencyBonus(data.level);

            int conMod = InfinityRPGData.AbilityModifier(data.constitution);
            int hitDieSides = InfinityRPGData.HitDieValue(data.hitDieType);

            // Level 1 HP = max hit die + CON mod
            data.maxHP     = hitDieSides + conMod;
            data.maxHP     = Mathf.Max(1, data.maxHP);
            data.currentHP = data.maxHP;

            // Default unarmored AC = 10 + DEX mod
            int dexMod = InfinityRPGData.AbilityModifier(data.dexterity);
            data.armorClass = 10 + dexMod;

            data.speed = 30; // feet (most species); overridden per species in full build
        }

        private static void ApplyClassDefaults(CharacterData data)
        {
            // Hit die and spellcasting ability by class (SRD classes)
            switch (data.className.ToLowerInvariant())
            {
                case "barbarian":
                    data.hitDieType = "d12";
                    data.savingThrowProficiencies = new[] { "STR", "CON" };
                    break;
                case "bard":
                    data.hitDieType = "d8";
                    data.isSpellcaster = true; data.spellcastingAbility = "CHA";
                    data.savingThrowProficiencies = new[] { "DEX", "CHA" };
                    break;
                case "cleric":
                    data.hitDieType = "d8";
                    data.isSpellcaster = true; data.spellcastingAbility = "WIS";
                    data.savingThrowProficiencies = new[] { "WIS", "CHA" };
                    break;
                case "druid":
                    data.hitDieType = "d8";
                    data.isSpellcaster = true; data.spellcastingAbility = "WIS";
                    data.savingThrowProficiencies = new[] { "INT", "WIS" };
                    break;
                case "fighter":
                    data.hitDieType = "d10";
                    data.savingThrowProficiencies = new[] { "STR", "CON" };
                    break;
                case "monk":
                    data.hitDieType = "d8";
                    data.savingThrowProficiencies = new[] { "STR", "DEX" };
                    break;
                case "paladin":
                    data.hitDieType = "d10";
                    data.isSpellcaster = true; data.spellcastingAbility = "CHA";
                    data.savingThrowProficiencies = new[] { "WIS", "CHA" };
                    break;
                case "ranger":
                    data.hitDieType = "d10";
                    data.isSpellcaster = true; data.spellcastingAbility = "WIS";
                    data.savingThrowProficiencies = new[] { "STR", "DEX" };
                    break;
                case "rogue":
                    data.hitDieType = "d8";
                    data.savingThrowProficiencies = new[] { "DEX", "INT" };
                    break;
                case "sorcerer":
                    data.hitDieType = "d6";
                    data.isSpellcaster = true; data.spellcastingAbility = "CHA";
                    data.savingThrowProficiencies = new[] { "CON", "CHA" };
                    break;
                case "warlock":
                    data.hitDieType = "d8";
                    data.isSpellcaster = true; data.spellcastingAbility = "CHA";
                    data.savingThrowProficiencies = new[] { "WIS", "CHA" };
                    break;
                case "wizard":
                    data.hitDieType = "d6";
                    data.isSpellcaster = true; data.spellcastingAbility = "INT";
                    data.savingThrowProficiencies = new[] { "INT", "WIS" };
                    break;
                default:
                    data.hitDieType = "d8";
                    data.savingThrowProficiencies = new[] { "STR", "CON" };
                    break;
            }

            // Recalculate HP now that we have the correct hit die
            int conMod = InfinityRPGData.AbilityModifier(data.constitution);
            int sides = InfinityRPGData.HitDieValue(data.hitDieType);
            data.maxHP     = Mathf.Max(1, sides + conMod);
            data.currentHP = data.maxHP;

            // Set spell save DC and attack bonus if spellcaster
            if (data.isSpellcaster)
            {
                int castingAbilityScore = data.spellcastingAbility switch
                {
                    "INT" => data.intelligence,
                    "WIS" => data.wisdom,
                    "CHA" => data.charisma,
                    _     => 10
                };
                data.spellSaveDC      = InfinityRPGData.SpellSaveDC(data.proficiencyBonus, castingAbilityScore);
                data.spellAttackBonus = data.proficiencyBonus + InfinityRPGData.AbilityModifier(castingAbilityScore);

                // Set level 1 spell slots for full casters
                data.spellSlotsMax[1] = InfinityRPGData.GetFullCasterSlots(1, 1);
            }
        }

        private static int Clamp(int val, int min, int max) =>
            val < min ? min : val > max ? max : val;
    }
}
