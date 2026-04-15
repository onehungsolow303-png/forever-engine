using ForeverEngine.ECS.Utility;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;

namespace ForeverEngine.Demo.Battle
{
    public class BattleCombatant
    {
        // === Core fields (unchanged) ===
        public string Name;
        public int X, Y;
        public int HP, MaxHP, AC;
        public int Strength, Dexterity, Speed;
        public int AtkCount, AtkSides, AtkBonus;
        public string Behavior;
        public bool IsPlayer;
        public bool IsAlive => HP > 0 || (IsPlayer && DeathSaves != null && DeathSaves.IsActive);
        public int MovementRemaining;
        public bool HasAction;
        public bool HasBonusAction;
        public bool HasReaction;
        public int InitiativeRoll;

        // === RPG Integration fields ===
        public CharacterSheet Sheet;
        public ConditionManager Conditions = new ConditionManager();
        public DeathSaveTracker DeathSaves;
        public ConcentrationTracker Concentration;

        // Damage type info for the resolver pipeline
        public DamageType Resistances;
        public DamageType Vulnerabilities;
        public DamageType Immunities;
        public DamageType AttackDamageType = DamageType.Slashing;

        // Temp HP (tracked here for non-sheet combatants; sheet combatants use Sheet.TempHP)
        public int TempHP;
        public string ModelId;
        public float ModelScale = 1f;
        public ModelAnimator Animator;
        /// <summary>World-space position at the moment this combatant entered the battle.
        /// Set by the battle initialiser before calling BattleZoneManager.Initialize.</summary>
        public UnityEngine.Vector3 SpawnWorldPos;
        public bool HasRangedAttack;
        public int AttackRange;
        public int RangedAtkCount, RangedAtkSides, RangedAtkBonus;

        public void RollInitiative(ref uint seed)
        {
            InitiativeRoll = DiceRoller.Roll(1, 20, 0, ref seed) + DiceRoller.AbilityModifier(Dexterity);
        }

        public int RollAttack(ref uint seed) => DiceRoller.Roll(1, 20, 0, ref seed) + DiceRoller.AbilityModifier(Strength);
        public int RollDamage(ref uint seed) => DiceRoller.Roll(AtkCount, AtkSides, AtkBonus, ref seed);
        public int RollRangedDamage(ref uint seed) => DiceRoller.Roll(
            RangedAtkCount > 0 ? RangedAtkCount : AtkCount,
            RangedAtkSides > 0 ? RangedAtkSides : AtkSides,
            RangedAtkBonus > 0 ? RangedAtkBonus : AtkBonus, ref seed);

        /// <summary>
        /// Apply raw HP damage (after resistance/tempHP already resolved by DamageResolver).
        /// </summary>
        public void TakeDamage(int amount)
        {
            HP = System.Math.Max(0, HP - amount);
            if (Sheet != null) Sheet.HP = HP;
        }

        /// <summary>
        /// Apply healing. Resets death saves if revived from 0 HP.
        /// </summary>
        public void Heal(int amount)
        {
            bool wasAtZero = HP <= 0;
            HP = System.Math.Min(MaxHP, HP + amount);
            if (Sheet != null) Sheet.HP = HP;
            if (wasAtZero && HP > 0 && DeathSaves != null)
                DeathSaves.Reset();
        }
        public void StartTurn()
        {
            MovementRemaining = Speed;
            HasAction = true;
            HasBonusAction = true;
            HasReaction = true;
            // Tick condition durations at start of this combatant's turn
            if (Conditions != null)
                Conditions.TickDurations();
        }

        public static BattleCombatant FromPlayer(PlayerData data)
        {
            DiceRoller.Parse(data.AttackDice, out int c, out int s, out int b);
            var combatant = new BattleCombatant
            {
                Name = "Wanderer", X = 1, Y = 1, IsPlayer = true,
                HP = data.HP, MaxHP = data.MaxHP, AC = data.AC,
                Strength = data.Strength, Dexterity = data.Dexterity, Speed = data.Speed,
                AtkCount = c, AtkSides = s, AtkBonus = b, Behavior = "player",
                ModelId = data.ModelId
            };
            var (modelPath, modelScale) = ModelRegistry.Resolve(combatant.ModelId ?? "Default_Player");
            if (modelPath != null) { combatant.ModelId = modelPath; combatant.ModelScale = modelScale; }
            return combatant;
        }

        public static BattleCombatant FromEnemy(Encounters.EnemyDef def, int x, int y)
        {
            DiceRoller.Parse(def.AtkDice, out int c, out int s, out int b);
            var result = new BattleCombatant
            {
                Name = def.Name, X = x, Y = y, IsPlayer = false,
                HP = def.HP, MaxHP = def.HP, AC = def.AC,
                Strength = def.Str, Dexterity = def.Dex, Speed = def.Spd,
                AtkCount = c, AtkSides = s, AtkBonus = b, Behavior = def.Behavior,
                Conditions = new ConditionManager(),
                Resistances = def.Resistances,
                Vulnerabilities = def.Vulnerabilities,
                Immunities = def.Immunities,
                AttackDamageType = def.AttackDamageType,
                ModelId = def.ModelId,
                ModelScale = def.ModelScale,
                HasRangedAttack = def.HasRangedAttack,
                AttackRange = def.AttackRange,
            };

            if (def.HasRangedAttack && !string.IsNullOrEmpty(def.RangedAtkDice))
            {
                DiceRoller.Parse(def.RangedAtkDice, out int rc, out int rs, out int rb);
                result.RangedAtkCount = rc;
                result.RangedAtkSides = rs;
                result.RangedAtkBonus = rb;
            }
            return result;
        }

        /// <summary>
        /// Create a BattleCombatant from a full CharacterSheet (player).
        /// </summary>
        public static BattleCombatant FromCharacterSheet(CharacterSheet sheet)
        {
            var snap = sheet.ToStatsSnapshot();
            var combatant = new BattleCombatant
            {
                Name = sheet.Name,
                X = 1, Y = 1,
                IsPlayer = true,
                Sheet = sheet,
                HP = snap.HP,
                MaxHP = snap.MaxHP,
                AC = snap.AC,
                Strength = snap.Strength,
                Dexterity = snap.Dexterity,
                Speed = snap.Speed,
                AtkCount = snap.AtkDiceCount,
                AtkSides = snap.AtkDiceSides,
                AtkBonus = snap.AtkDiceBonus,
                Behavior = "player",
                Conditions = sheet.Conditions,
                DeathSaves = sheet.DeathSaves,
                Concentration = sheet.Concentration,
                TempHP = sheet.TempHP,
                AttackDamageType = sheet.MainHand != null ? sheet.MainHand.Type : DamageType.Bludgeoning,
                ModelId = sheet.ModelId
            };
            // Resolve model via registry; fall back to species+class key if ModelId not set
            string registryKey = !string.IsNullOrEmpty(combatant.ModelId)
                ? combatant.ModelId
                : BuildPlayerKey(sheet);
            var (modelPath, modelScale) = ModelRegistry.Resolve(registryKey);
            if (modelPath != null) { combatant.ModelId = modelPath; combatant.ModelScale = modelScale; }
            return combatant;
        }

        private static string BuildPlayerKey(CharacterSheet sheet)
        {
            string speciesName = sheet.Species != null && !string.IsNullOrEmpty(sheet.Species.Name)
                ? sheet.Species.Name.Replace(" ", "")
                : "Human";
            string className = sheet.ClassLevels != null && sheet.ClassLevels.Count > 0
                               && sheet.ClassLevels[0].ClassRef != null
                ? sheet.ClassLevels[0].ClassRef.Name
                : "Fighter";
            return $"{speciesName}_{className}";
        }
    }
}
