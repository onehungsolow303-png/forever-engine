using System.Collections.Generic;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using ForeverEngine.RPG.Spells;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// The master character record. Source of truth for all character state.
    /// Pure C# — no MonoBehaviour or ECS dependencies except the bridge method.
    /// </summary>
    [System.Serializable]
    public class CharacterSheet
    {
        // === Identity ===
        public string Name;
        public SpeciesData Species;

        // === Ability Scores ===
        public AbilityScores BaseAbilities;
        private AbilityScores _effectiveAbilities;
        public AbilityScores EffectiveAbilities => _effectiveAbilities;

        // === Class Levels (multiclass support) ===
        public List<ClassLevel> ClassLevels = new List<ClassLevel>();

        public int TotalLevel
        {
            get
            {
                int total = 0;
                foreach (var cl in ClassLevels) total += cl.Level;
                return total;
            }
        }

        public int ProficiencyBonus => ProficiencyTable.GetBonus(TotalLevel);
        public Tier CurrentTier => ExperienceTable.GetTierForLevel(TotalLevel);

        // === Hit Points ===
        public int HP;
        public int MaxHP;
        public int TempHP;

        // === Armor Class ===
        public int AC { get; private set; }

        // === Experience ===
        public int XP;

        // === Proficiencies ===
        public HashSet<string> Proficiencies = new HashSet<string>();

        // === Resources (Rage, Ki, Sorcery Points, Lay on Hands, etc.) ===
        public Dictionary<string, ResourcePool> Resources = new Dictionary<string, ResourcePool>();

        // === Spells ===
        public List<SpellData> KnownSpells = new List<SpellData>();
        public List<SpellData> PreparedSpells = new List<SpellData>();
        public SpellSlotManager SpellSlots = new SpellSlotManager();

        // === Conditions ===
        public ConditionManager Conditions = new ConditionManager();

        // === Death Saves ===
        public DeathSaveTracker DeathSaves = new DeathSaveTracker();

        // === Concentration ===
        public ConcentrationTracker Concentration = new ConcentrationTracker();

        // === Attunement ===
        public AttunementManager Attunement = new AttunementManager();

        // === Equipment Slots ===
        public WeaponData MainHand;
        public UnityEngine.ScriptableObject OffHand; // WeaponData (dual wield) or ArmorData (shield)
        public ArmorData Armor;

        // === Inventory ===
        public ForeverEngine.ECS.Data.Inventory Bag = new ForeverEngine.ECS.Data.Inventory(40);

        // === Expertise (double proficiency for certain skills) ===
        public HashSet<string> Expertise = new HashSet<string>();

        // === Model ===
        public string ModelId;

        // ================================================================
        // METHODS
        // ================================================================

        /// <summary>
        /// Get the saving throw DC for a caster with a given casting ability.
        /// DC = 8 + proficiency + ability modifier.
        /// </summary>
        public int GetSaveDC(Ability castingAbility)
        {
            return 8 + ProficiencyBonus + _effectiveAbilities.GetModifier(castingAbility);
        }

        /// <summary>
        /// Get the spell attack bonus for a caster with a given casting ability.
        /// Bonus = proficiency + ability modifier.
        /// </summary>
        public int GetSpellAttackBonus(Ability castingAbility)
        {
            return ProficiencyBonus + _effectiveAbilities.GetModifier(castingAbility);
        }

        /// <summary>
        /// Check if the character is proficient in a skill, weapon, armor, tool, or save.
        /// </summary>
        public bool IsProficient(string proficiency)
        {
            return Proficiencies.Contains(proficiency);
        }

        /// <summary>
        /// Get the total bonus for a skill check.
        /// = ability modifier + proficiency (if proficient) + expertise (if applicable).
        /// </summary>
        /// <param name="skill">The skill name (e.g., "Athletics", "Stealth").</param>
        /// <param name="ability">The ability associated with this skill.</param>
        public int GetSkillBonus(string skill, Ability ability)
        {
            int mod = _effectiveAbilities.GetModifier(ability);
            if (IsProficient(skill))
            {
                mod += ProficiencyBonus;
                if (Expertise.Contains(skill))
                {
                    mod += ProficiencyBonus; // Expertise = double proficiency
                }
            }
            return mod;
        }

        /// <summary>
        /// Add XP and check for level up.
        /// </summary>
        public void GainXP(int amount)
        {
            XP += amount;
            // Level up is handled manually via LevelUp() — caller checks if threshold is met
        }

        /// <summary>
        /// Check if the character has enough XP to level up.
        /// </summary>
        public bool CanLevelUp
        {
            get
            {
                int nextLevel = TotalLevel + 1;
                if (nextLevel > 40) return false;
                return XP >= ExperienceTable.GetThreshold(nextLevel);
            }
        }

        /// <summary>
        /// Level up in a specific class. Gains HP (hit die + CON mod), updates slots.
        /// </summary>
        /// <param name="classToLevel">The class to gain a level in.</param>
        /// <param name="seed">RNG seed for HP roll.</param>
        public void LevelUp(ClassData classToLevel, ref uint seed)
        {
            if (classToLevel == null) return;
            if (TotalLevel >= 40) return;

            // Find existing class level or add new one
            bool found = false;
            for (int i = 0; i < ClassLevels.Count; i++)
            {
                if (ClassLevels[i].ClassRef == classToLevel)
                {
                    var cl = ClassLevels[i];
                    cl.Level++;
                    ClassLevels[i] = cl;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                ClassLevels.Add(new ClassLevel(classToLevel, 1));
            }

            // Roll HP: hit die + CON mod (minimum 1)
            int hitDieSides = (int)classToLevel.HitDie;
            int hpRoll = DiceRoller.Roll(1, hitDieSides, 0, ref seed);
            int conMod = _effectiveAbilities.GetModifier(Ability.CON);
            int hpGain = hpRoll + conMod;
            if (hpGain < 1) hpGain = 1;

            MaxHP += hpGain;
            HP += hpGain;

            // Recalculate spell slots
            SpellSlots.RecalculateSlots(ClassLevels);

            // Recalculate AC
            RecalculateAC();

            // Recalculate effective abilities
            RecalculateEffectiveAbilities();
        }

        /// <summary>
        /// Long rest: restore all HP, spell slots, resources; remove expired conditions.
        /// </summary>
        public void LongRest()
        {
            HP = MaxHP;
            TempHP = 0;
            SpellSlots.RestoreAll();

            // Restore all long-rest resources
            var keys = new List<string>(Resources.Keys);
            foreach (var key in keys)
            {
                var pool = Resources[key];
                pool.RestoreAll();
                Resources[key] = pool;
            }

            // Clear death saves
            DeathSaves.Reset();

            // End concentration
            Concentration.End();

            // Tick conditions (could remove some)
            Conditions.TickDurations();
        }

        /// <summary>
        /// Short rest: restore Warlock pact slots, spend hit dice to heal,
        /// reset short-rest resources.
        /// </summary>
        /// <param name="hitDiceToSpend">Number of hit dice to spend for healing.</param>
        /// <param name="seed">RNG seed for healing rolls.</param>
        public void ShortRest(int hitDiceToSpend, ref uint seed)
        {
            SpellSlots.RestorePactSlots();

            // Spend hit dice for healing
            if (hitDiceToSpend > 0 && ClassLevels.Count > 0)
            {
                int conMod = _effectiveAbilities.GetModifier(Ability.CON);
                for (int i = 0; i < hitDiceToSpend; i++)
                {
                    // Use first class's hit die (simplified — full impl would track per-class)
                    int hitDieSides = (int)ClassLevels[0].ClassRef.HitDie;
                    int healing = DiceRoller.Roll(1, hitDieSides, 0, ref seed) + conMod;
                    if (healing < 0) healing = 0;
                    HP += healing;
                    if (HP > MaxHP) HP = MaxHP;
                }
            }
        }

        /// <summary>
        /// Recalculate effective abilities from base + species bonuses + item bonuses.
        /// </summary>
        public void RecalculateEffectiveAbilities()
        {
            _effectiveAbilities = BaseAbilities;

            // Apply species bonuses
            if (Species != null && Species.AbilityBonuses != null)
            {
                foreach (var bonus in Species.AbilityBonuses)
                {
                    _effectiveAbilities = _effectiveAbilities.WithBonus(bonus.Ability, bonus.Bonus);
                }
            }

            // Apply magic item bonuses from attunement
            for (int i = 0; i < AttunementManager.MaxSlots; i++)
            {
                var item = Attunement.GetSlot(i);
                if (item != null && item.AbilityBonuses != null)
                {
                    foreach (var bonus in item.AbilityBonuses)
                    {
                        _effectiveAbilities = _effectiveAbilities.WithBonus(bonus.Ability, bonus.Bonus);
                    }
                }
            }
        }

        /// <summary>
        /// Recalculate AC from equipment.
        /// </summary>
        public void RecalculateAC()
        {
            ArmorData shield = OffHand as ArmorData;
            bool hasMonkUnarmored = IsProficient("MonkUnarmoredDefense");
            bool hasBarbarianUnarmored = IsProficient("BarbarianUnarmoredDefense");

            int magicACBonus = 0;
            for (int i = 0; i < AttunementManager.MaxSlots; i++)
            {
                var item = Attunement.GetSlot(i);
                if (item != null) magicACBonus += item.ACBonus;
            }

            AC = EquipmentResolver.CalculateAC(
                _effectiveAbilities,
                Armor,
                shield,
                hasMonkUnarmored,
                hasBarbarianUnarmored,
                magicACBonus);
        }

        /// <summary>
        /// Create a lightweight stats snapshot for ECS bridge / CombatBrain compatibility.
        /// </summary>
        public StatsSnapshot ToStatsSnapshot()
        {
            var snapshot = new StatsSnapshot
            {
                Strength = _effectiveAbilities.Strength,
                Dexterity = _effectiveAbilities.Dexterity,
                Constitution = _effectiveAbilities.Constitution,
                Intelligence = _effectiveAbilities.Intelligence,
                Wisdom = _effectiveAbilities.Wisdom,
                Charisma = _effectiveAbilities.Charisma,
                AC = AC,
                HP = HP,
                MaxHP = MaxHP,
                Speed = Species != null ? Species.Speed / 5 : 6 // Convert feet to tiles
            };

            // Map equipped weapon to attack dice
            if (MainHand != null)
            {
                var dmg = MainHand.GetDamage();
                snapshot.AtkDiceCount = dmg.Count;
                snapshot.AtkDiceSides = (int)dmg.Die;
                snapshot.AtkDiceBonus = dmg.Bonus + MainHand.MagicBonus;
            }
            else
            {
                // Unarmed strike: 1 + STR mod
                snapshot.AtkDiceCount = 1;
                snapshot.AtkDiceSides = 1;
                snapshot.AtkDiceBonus = _effectiveAbilities.GetModifier(Ability.STR);
            }

            return snapshot;
        }
    }
}
