using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ForeverEngine.ECS.Utility;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.Demo.Battle
{
    public class BattleManager : UnityEngine.MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public BattleGrid Grid { get; private set; }
        public List<BattleCombatant> Combatants { get; private set; } = new();
        public BattleCombatant CurrentTurn { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public bool BattleOver { get; private set; }
        public bool PlayerWon { get; private set; }
        public List<string> Log { get; private set; } = new();

        private int _turnIndex;
        private uint _rngSeed;
        private Encounters.EncounterData _encounterData;
        private BattleRenderer _renderer;
        private Dictionary<BattleCombatant, CombatBrain> _brains = new();
        private CombatIntelligence _neuralBrain;

        // Spell casting UI state
        private bool _spellMenuOpen;
        private List<SpellData> _availableSpells = new();
        private int _selectedSpellSlotLevel = 1;

        private void Awake() => Instance = this;

        private void Update()
        {
            if (_renderer == null) _renderer = FindFirstObjectByType<BattleRenderer>();
            if (_renderer != null) _renderer.UpdateVisuals(Combatants, CurrentTurn);

            // Player input during their turn
            if (CurrentTurn != null && CurrentTurn.IsPlayer && !BattleOver)
            {
                if (Input.GetKeyDown(KeyCode.W)) PlayerMove(0, 1);
                else if (Input.GetKeyDown(KeyCode.S)) PlayerMove(0, -1);
                else if (Input.GetKeyDown(KeyCode.A)) PlayerMove(-1, 0);
                else if (Input.GetKeyDown(KeyCode.D)) PlayerMove(1, 0);
                // Attack nearest adjacent enemy with 1 or F
                else if (!_spellMenuOpen && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F)))
                    AttackNearestEnemy();
                // Toggle spell menu with Q
                else if (Input.GetKeyDown(KeyCode.Q))
                    ToggleSpellMenu();
                // Spell selection (1-9) when menu is open
                else if (_spellMenuOpen)
                    HandleSpellInput();
                else if (Input.GetKeyDown(KeyCode.Space)) PlayerEndTurn();
            }
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            string encId = gm.PendingEncounterId ?? gm.PendingLocationId ?? "default";
            _encounterData = Encounters.EncounterData.Get(encId);
            _rngSeed = (uint)(gm.CurrentSeed + encId.GetHashCode());

            // Build grid
            Grid = new BattleGrid(_encounterData.GridWidth, _encounterData.GridHeight, (int)_rngSeed);

            // Spawn player — use CharacterSheet if available, fall back to PlayerData
            BattleCombatant player;
            if (gm.Character != null)
                player = BattleCombatant.FromCharacterSheet(gm.Character);
            else
                player = BattleCombatant.FromPlayer(gm.Player);
            Combatants.Add(player);

            // Spawn enemies
            var rng = new System.Random((int)_rngSeed);
            foreach (var enemyDef in _encounterData.Enemies)
            {
                int ex, ey;
                do { ex = rng.Next(2, Grid.Width - 2); ey = rng.Next(2, Grid.Height - 2); }
                while (!Grid.IsWalkable(ex, ey) || Combatants.Any(c => c.X == ex && c.Y == ey));
                Combatants.Add(BattleCombatant.FromEnemy(enemyDef, ex, ey));
            }

            // Roll initiative
            foreach (var c in Combatants) c.RollInitiative(ref _rngSeed);
            Combatants = Combatants.OrderByDescending(c => c.InitiativeRoll).ToList();

            // Notify AI integration
            Demo.AI.DemoAIIntegration.Instance?.OnCombatStarted(encId);

            // Initialize Q-learning brains for each enemy
            float[] savedTable = Demo.AI.DemoAIIntegration.Instance?.LoadCombatQTable();
            foreach (var c in Combatants)
            {
                if (!c.IsPlayer && c.IsAlive)
                    _brains[c] = new CombatBrain(savedTable, seed: (int)_rngSeed + c.X * 100 + c.Y);
            }

            // Create neural intelligence if InferenceEngine exists
            if (ForeverEngine.AI.Inference.InferenceEngine.Instance != null)
            {
                var go = new GameObject("CombatIntelligence");
                go.transform.SetParent(transform);
                _neuralBrain = go.AddComponent<CombatIntelligence>();
            }

            // Create visual renderer
            var rendererGO = new GameObject("BattleRenderer");
            var renderer = rendererGO.AddComponent<BattleRenderer>();
            renderer.Initialize(Grid, Combatants, Camera.main);

            StartTurn();
            Log.Add($"Battle begins! {_encounterData.Enemies.Count} enemies.");
        }

        public void StartTurn()
        {
            if (BattleOver) return;
            CurrentTurn = Combatants[_turnIndex];

            // Skip truly dead combatants (enemies at 0 HP, or player who failed death saves)
            if (!CurrentTurn.IsAlive) { NextTurn(); return; }

            CurrentTurn.StartTurn();

            // Handle death save mode: player at 0 HP rolls a death save instead of acting
            if (CurrentTurn.IsPlayer && CurrentTurn.HP <= 0 && CurrentTurn.DeathSaves != null && CurrentTurn.DeathSaves.IsActive)
            {
                RollPlayerDeathSave();
                return; // Turn ends after death save
            }

            // Check if conditions prevent acting
            if (CurrentTurn.Conditions != null && !CurrentTurn.Conditions.CanAct)
            {
                Log.Add($"{CurrentTurn.Name} is incapacitated and cannot act!");
                Invoke(nameof(NextTurn), 0.5f);
                return;
            }

            if (!CurrentTurn.IsPlayer) ProcessAITurn();
        }

        private void RollPlayerDeathSave()
        {
            int roll = DiceRoller.Roll(1, 20, 0, ref _rngSeed);
            var result = CurrentTurn.DeathSaves.RollDeathSave(roll);

            switch (result)
            {
                case DeathSaveResult.Revived:
                    CurrentTurn.HP = 1;
                    if (CurrentTurn.Sheet != null) CurrentTurn.Sheet.HP = 1;
                    Log.Add($"Death Save: d20={roll} -- NATURAL 20! {CurrentTurn.Name} revives with 1 HP!");
                    break;
                case DeathSaveResult.Stabilized:
                    Log.Add($"Death Save: d20={roll} -- Success ({CurrentTurn.DeathSaves.Successes}/3). {CurrentTurn.Name} is stabilized!");
                    break;
                case DeathSaveResult.Dead:
                    Log.Add($"Death Save: d20={roll} -- {(roll <= 1 ? "NATURAL 1! Two failures!" : "Failure")} ({CurrentTurn.DeathSaves.Failures}/3). {CurrentTurn.Name} has died!");
                    break;
                case DeathSaveResult.Success:
                    Log.Add($"Death Save: d20={roll} -- Success ({CurrentTurn.DeathSaves.Successes}/3)");
                    break;
                case DeathSaveResult.Failure:
                    Log.Add($"Death Save: d20={roll} -- {(roll <= 1 ? "NATURAL 1! Two failures!" : "Failure")} ({CurrentTurn.DeathSaves.Failures}/3)");
                    break;
            }

            Invoke(nameof(NextTurn), 1.0f);
        }

        public void NextTurn()
        {
            _turnIndex = (_turnIndex + 1) % Combatants.Count;
            if (_turnIndex == 0) RoundNumber++;
            CheckBattleEnd();
            if (!BattleOver) StartTurn();
        }

        public void PlayerMove(int dx, int dy)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer) return;

            int nx = CurrentTurn.X + dx, ny = CurrentTurn.Y + dy;
            if (!Grid.IsWalkable(nx, ny)) return;

            // Bump attack: moving into an enemy attacks them instead
            var target = Combatants.FirstOrDefault(c => c.IsAlive && !c.IsPlayer && c.X == nx && c.Y == ny);
            if (target != null)
            {
                if (CurrentTurn.HasAction)
                {
                    ResolveAttack(CurrentTurn, target);
                    CurrentTurn.HasAction = false;
                    CheckBattleEnd();
                }
                return;
            }

            if (CurrentTurn.MovementRemaining <= 0) return;
            CurrentTurn.X = nx; CurrentTurn.Y = ny;
            CurrentTurn.MovementRemaining--;
        }

        public void PlayerAttack(BattleCombatant target)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || !CurrentTurn.HasAction) return;
            if (!IsAdjacent(CurrentTurn, target)) return;
            ResolveAttack(CurrentTurn, target);
            CurrentTurn.HasAction = false;
            CheckBattleEnd();
        }

        public void AttackNearestEnemy()
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || !CurrentTurn.HasAction) return;
            // Find closest adjacent alive enemy
            var target = Combatants
                .Where(c => c.IsAlive && !c.IsPlayer && IsAdjacent(CurrentTurn, c))
                .OrderBy(c => System.Math.Abs(c.X - CurrentTurn.X) + System.Math.Abs(c.Y - CurrentTurn.Y))
                .FirstOrDefault();
            if (target != null)
            {
                ResolveAttack(CurrentTurn, target);
                CurrentTurn.HasAction = false;
                CheckBattleEnd();
            }
            else
            {
                Log.Add("No adjacent enemy to attack! Move closer first.");
            }
        }

        public void PlayerEndTurn() { if (CurrentTurn != null && CurrentTurn.IsPlayer) NextTurn(); }

        // === Spell Casting ===

        private void ToggleSpellMenu()
        {
            _spellMenuOpen = !_spellMenuOpen;
            if (_spellMenuOpen)
            {
                var playerCombatant = Combatants.FirstOrDefault(c => c.IsPlayer);
                if (playerCombatant?.Sheet == null || playerCombatant.Sheet.PreparedSpells.Count == 0)
                {
                    Log.Add("No spells available.");
                    _spellMenuOpen = false;
                    return;
                }
                _availableSpells = playerCombatant.Sheet.PreparedSpells;
                Log.Add("Spell menu open. Press 1-9 to cast, Q to close.");
            }
        }

        private void HandleSpellInput()
        {
            // Escape or Q closes menu
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q))
            {
                _spellMenuOpen = false;
                Log.Add("Spell menu closed.");
                return;
            }

            // Number keys 1-9 select a spell
            for (int i = 0; i < 9 && i < _availableSpells.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    CastSpell(_availableSpells[i]);
                    return;
                }
            }
        }

        private void CastSpell(SpellData spell)
        {
            var playerCombatant = Combatants.FirstOrDefault(c => c.IsPlayer);
            if (playerCombatant?.Sheet == null || !playerCombatant.HasAction)
            {
                Log.Add("Cannot cast right now.");
                return;
            }

            var sheet = playerCombatant.Sheet;
            int slotLevel = spell.IsCantrip ? 0 : spell.Level;

            // Find a target — closest enemy for damage spells, self for healing
            BattleCombatant target = null;
            if (spell.HealingDiceCount > 0)
            {
                target = playerCombatant; // Self-heal
            }
            else
            {
                // Target closest alive enemy
                target = Combatants
                    .Where(c => c.IsAlive && !c.IsPlayer)
                    .OrderBy(c => System.Math.Abs(c.X - playerCombatant.X) + System.Math.Abs(c.Y - playerCombatant.Y))
                    .FirstOrDefault();
            }

            if (target == null && spell.HealingDiceCount <= 0)
            {
                Log.Add("No valid target.");
                return;
            }

            var castingAbility = RPGBridge.GetCastingAbility(sheet);

            // Build CastContext
            var ctx = new CastContext
            {
                Caster = sheet,
                Targets = target != null ? new object[] { target } : null,
                Spell = spell,
                SlotLevel = slotLevel > 0 ? slotLevel : spell.Level,
                Metamagic = MetamagicType.None,
                IsRitual = false
            };

            var result = SpellCastingPipeline.Cast(
                ctx,
                sheet.EffectiveAbilities,
                sheet.ProficiencyBonus,
                castingAbility,
                sheet.SpellSlots,
                sheet.Concentration,
                null, // No sorcery points for premades
                ref _rngSeed);

            if (!result.Success)
            {
                Log.Add($"Cannot cast {spell.Name}: {result.FailureReason}");
                return;
            }

            _spellMenuOpen = false;
            playerCombatant.HasAction = false;

            // Apply damage to target
            if (result.DamageDealt > 0 && target != null && target != playerCombatant)
            {
                // Apply through DamageResolver with target's resistances
                var dmgCtx = new DamageContext
                {
                    BaseDamage = spell.GetDamage(),
                    Type = spell.DamageType,
                    Critical = false,
                    BonusDamage = 0,
                    Resistances = target.Resistances,
                    Vulnerabilities = target.Vulnerabilities,
                    Immunities = target.Immunities,
                    TargetTempHP = target.TempHP,
                    TargetHP = target.HP
                };
                var dmgResult = DamageResolver.Apply(dmgCtx, ref _rngSeed);

                // Apply temp HP absorption
                if (dmgResult.AbsorbedByTempHP > 0)
                    target.TempHP -= dmgResult.AbsorbedByTempHP;

                target.TakeDamage(dmgResult.HPDamage);

                string resistMsg = "";
                if ((target.Resistances & spell.DamageType) != 0)
                    resistMsg = " (resisted)";
                if ((target.Vulnerabilities & spell.DamageType) != 0)
                    resistMsg = " (vulnerable!)";

                Log.Add($"{playerCombatant.Name} casts {spell.Name} on {target.Name} for {dmgResult.AfterResistance} {spell.DamageType} damage{resistMsg}!");

                // AI events
                var ai = Demo.AI.DemoAIIntegration.Instance;
                ai?.OnPlayerAttacked(true, dmgResult.AfterResistance, target.Name);

                if (!target.IsAlive)
                {
                    Log.Add($"{target.Name} is defeated!");
                    ai?.OnEnemyKilled(target.Name);
                }

                // Q-learning penalty for target
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(-0.3f);
            }

            // Apply healing
            if (result.HealingDone > 0 && target != null)
            {
                target.Heal(result.HealingDone);
                Log.Add($"{playerCombatant.Name} casts {spell.Name} — heals {target.Name} for {result.HealingDone} HP!");
            }

            // Apply conditions
            if (result.ConditionsApplied != Condition.None && target != null && target.Conditions != null)
            {
                target.Conditions.Apply(result.ConditionsApplied, spell.ConditionDuration, spell.Name);
                Log.Add($"{target.Name} is now {result.ConditionsApplied}!");
            }

            // Concentration tracking
            if (result.ConcentrationStarted)
            {
                Log.Add($"{playerCombatant.Name} is concentrating on {spell.Name}.");
            }

            // Slot info
            if (result.SlotExpended > 0)
            {
                int remaining = sheet.SpellSlots.AvailableSlots[result.SlotExpended - 1];
                Log.Add($"(Level {result.SlotExpended} slot expended. {remaining} remaining)");
            }

            CheckBattleEnd();
        }

        // Public accessor for HUD to read spell menu state
        public bool IsSpellMenuOpen => _spellMenuOpen;
        public List<SpellData> AvailableSpells => _availableSpells;

        private void ProcessAITurn()
        {
            var ai = CurrentTurn;
            var player = Combatants.FirstOrDefault(c => c.IsPlayer && c.IsAlive);
            if (player == null) { NextTurn(); return; }

            if (!_brains.TryGetValue(ai, out var brain))
            {
                FallbackAI(ai, player);
                Invoke(nameof(NextTurn), 0.5f);
                return;
            }

            int aliveAllies = Combatants.Count(c => !c.IsPlayer && c.IsAlive && c != ai);
            CombatBrain.Action action;
            if (_neuralBrain != null && ForeverEngine.AI.Inference.InferenceEngine.Instance?.IsAvailable == true)
            {
                _neuralBrain.Configure(ai, brain);
                _neuralBrain.SetBattleContext(player, aliveAllies, ai.Behavior ?? "chase");
                action = _neuralBrain.DecideAction();
                // Keep Q-table updated even when using neural path
                if (_neuralBrain.UsingNeural)
                    brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");
            }
            else
            {
                action = brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");
            }

            switch (action)
            {
                case CombatBrain.Action.Advance:
                    MoveToward(ai, player.X, player.Y);
                    if (IsAdjacent(ai, player) && ai.HasAction)
                    {
                        ResolveAttack(ai, player);
                        ai.HasAction = false;
                        brain.AddReward(0.1f);
                    }
                    break;

                case CombatBrain.Action.Retreat:
                    MoveAway(ai, player.X, player.Y);
                    if (ai.HP < ai.MaxHP * 0.3f) brain.AddReward(0.2f);
                    break;

                case CombatBrain.Action.Flank:
                    MoveFlank(ai, player.X, player.Y);
                    break;

                case CombatBrain.Action.Attack:
                    if (IsAdjacent(ai, player) && ai.HasAction)
                    {
                        ResolveAttack(ai, player);
                        ai.HasAction = false;
                        brain.AddReward(0.3f);
                    }
                    else
                    {
                        MoveToward(ai, player.X, player.Y);
                    }
                    break;

                case CombatBrain.Action.Hold:
                    brain.AddReward(ai.Behavior == "guard" ? 0.1f : -0.05f);
                    break;
            }

            Invoke(nameof(NextTurn), 0.5f);
        }

        private void MoveToward(BattleCombatant ai, int targetX, int targetY)
        {
            while (ai.MovementRemaining > 0)
            {
                int dx = System.Math.Sign(targetX - ai.X);
                int dy = System.Math.Sign(targetY - ai.Y);
                if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
            }
        }

        private void MoveAway(BattleCombatant ai, int targetX, int targetY)
        {
            while (ai.MovementRemaining > 0)
            {
                int dx = -System.Math.Sign(targetX - ai.X);
                int dy = -System.Math.Sign(targetY - ai.Y);
                if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
            }
        }

        private void MoveFlank(BattleCombatant ai, int targetX, int targetY)
        {
            int dx = System.Math.Sign(targetX - ai.X);
            int dy = System.Math.Sign(targetY - ai.Y);
            int perpX = ai.X + dy, perpY = ai.Y - dx;
            if (!TryMove(ai, perpX, perpY))
            {
                perpX = ai.X - dy; perpY = ai.Y + dx;
                TryMove(ai, perpX, perpY);
            }
            MoveToward(ai, targetX, targetY);
        }

        private bool TryMove(BattleCombatant ai, int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= Grid.Width || ny >= Grid.Height) return false;
            if (!Grid.IsWalkable(nx, ny)) return false;
            if (Combatants.Any(c => c.IsAlive && c != ai && c.X == nx && c.Y == ny)) return false;
            ai.X = nx; ai.Y = ny; ai.MovementRemaining--;
            return true;
        }

        private void FallbackAI(BattleCombatant ai, BattleCombatant player)
        {
            int dist = System.Math.Abs(ai.X - player.X) + System.Math.Abs(ai.Y - player.Y);
            while (ai.MovementRemaining > 0 && dist > 1)
            {
                int dx = System.Math.Sign(player.X - ai.X);
                int dy = System.Math.Sign(player.Y - ai.Y);
                if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
                dist = System.Math.Abs(ai.X - player.X) + System.Math.Abs(ai.Y - player.Y);
            }
            if (IsAdjacent(ai, player) && ai.HasAction)
            {
                ResolveAttack(ai, player);
                ai.HasAction = false;
            }
        }

        private void ResolveAttack(BattleCombatant attacker, BattleCombatant target)
        {
            // Build AttackContext
            var atkAbilities = new AbilityScores(
                attacker.Strength, attacker.Dexterity, 10, 10, 10, 10);
            int profBonus = 2; // Default proficiency for demo enemies

            WeaponData weapon = null;
            bool isMelee = true;
            bool isRanged = false;
            int magicBonus = 0;

            if (attacker.Sheet != null)
            {
                atkAbilities = attacker.Sheet.EffectiveAbilities;
                profBonus = attacker.Sheet.ProficiencyBonus;
                weapon = attacker.Sheet.MainHand;
                if (weapon != null)
                {
                    isMelee = !weapon.IsRanged;
                    isRanged = weapon.IsRanged;
                    magicBonus = weapon.MagicBonus;
                }
            }

            var atkCtx = new AttackContext
            {
                AttackerAbilities = atkAbilities,
                AttackerProficiency = profBonus,
                Weapon = weapon,
                TargetAC = target.AC,
                AttackerConditions = attacker.Conditions?.ActiveFlags ?? Condition.None,
                TargetConditions = target.Conditions?.ActiveFlags ?? Condition.None,
                IsMelee = isMelee,
                IsRanged = isRanged,
                CritRange = 20,
                MagicBonus = magicBonus
            };

            var atkResult = AttackResolver.Resolve(atkCtx, ref _rngSeed);

            var ai = Demo.AI.DemoAIIntegration.Instance;

            // Format advantage/disadvantage in log
            string advStr = atkResult.State switch
            {
                AdvantageState.Advantage => " with advantage",
                AdvantageState.Disadvantage => " with disadvantage",
                _ => ""
            };

            if (atkResult.Hit)
            {
                // Build DamageContext
                var baseDmg = new DiceExpression(attacker.AtkCount, (DieType)attacker.AtkSides, attacker.AtkBonus);
                if (weapon != null)
                    baseDmg = weapon.GetDamage();

                int abilityDmgBonus = 0;
                if (attacker.Sheet != null)
                {
                    // STR mod for melee, DEX for ranged/finesse
                    if (weapon != null && weapon.IsFinesse)
                    {
                        int strMod = atkAbilities.GetModifier(Ability.STR);
                        int dexMod = atkAbilities.GetModifier(Ability.DEX);
                        abilityDmgBonus = strMod > dexMod ? strMod : dexMod;
                    }
                    else if (isRanged)
                        abilityDmgBonus = atkAbilities.GetModifier(Ability.DEX);
                    else
                        abilityDmgBonus = atkAbilities.GetModifier(Ability.STR);
                }

                var dmgType = attacker.AttackDamageType;
                if (weapon != null) dmgType = weapon.Type;

                var dmgCtx = new DamageContext
                {
                    BaseDamage = baseDmg,
                    Type = dmgType,
                    Critical = atkResult.Critical,
                    BonusDamage = abilityDmgBonus + magicBonus,
                    Resistances = target.Resistances,
                    Vulnerabilities = target.Vulnerabilities,
                    Immunities = target.Immunities,
                    TargetTempHP = target.TempHP,
                    TargetHP = target.HP
                };

                var dmgResult = DamageResolver.Apply(dmgCtx, ref _rngSeed);

                // Apply temp HP absorption
                if (dmgResult.AbsorbedByTempHP > 0)
                    target.TempHP -= dmgResult.AbsorbedByTempHP;

                int hpDamage = dmgResult.HPDamage;
                if (hpDamage < 1 && dmgResult.AfterResistance > 0) hpDamage = 1;

                // Handle damage at 0 HP (death save failures)
                if (target.HP <= 0 && target.IsPlayer && target.DeathSaves != null && target.DeathSaves.IsActive)
                {
                    var dsResult = target.DeathSaves.TakeDamageAtZero(atkResult.Critical);
                    Log.Add($"{target.Name} takes damage at 0 HP! Death save failure{(atkResult.Critical ? " (x2 from crit)" : "")}.");
                    if (dsResult == DeathSaveResult.Dead)
                        Log.Add($"{target.Name} has died!");
                }
                else
                {
                    target.TakeDamage(hpDamage);
                }

                // Format log message
                string critStr = atkResult.Critical ? "CRITICAL HIT! " : "";
                string resistStr = "";
                if ((target.Resistances & dmgType) != 0)
                    resistStr = $" (resisted, halved to {dmgResult.AfterResistance})";
                if ((target.Vulnerabilities & dmgType) != 0)
                    resistStr = $" (vulnerable! doubled to {dmgResult.AfterResistance})";

                Log.Add($"{critStr}{attacker.Name} hits {target.Name}{advStr} for {dmgResult.AfterResistance} {dmgType} damage! (d20={atkResult.NaturalRoll}, total={atkResult.Total} vs AC {target.AC}){resistStr}");

                // AI events (preserved)
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(true, dmgResult.AfterResistance, target.Name);
                if (target.IsPlayer) ai?.OnPlayerDamaged(dmgResult.AfterResistance);

                // Check concentration on damaged caster
                if (target.Concentration != null && target.Concentration.IsConcentrating && target.Sheet != null)
                {
                    bool maintained = target.Concentration.CheckConcentration(
                        dmgResult.AfterResistance,
                        target.Sheet.EffectiveAbilities,
                        target.Sheet.ProficiencyBonus,
                        RPGBridge.IsProficientConSave(target.Sheet),
                        false, // No War Caster for demo
                        ref _rngSeed);
                    if (!maintained)
                        Log.Add($"{target.Name} lost concentration on {target.Concentration.ActiveSpell?.Name ?? "spell"}!");
                }

                // Death & defeat
                if (target.HP <= 0)
                {
                    if (target.IsPlayer && target.DeathSaves != null && !target.DeathSaves.IsActive && !target.DeathSaves.IsDead)
                    {
                        // Enter death save mode
                        target.DeathSaves.Begin();
                        Log.Add($"{target.Name} falls to 0 HP! Death saves begin...");
                    }
                    else if (!target.IsPlayer)
                    {
                        Log.Add($"{target.Name} is defeated!");
                        ai?.OnEnemyKilled(target.Name);
                    }
                }

                // Q-learning: penalize target enemy for taking damage (preserved)
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(-0.3f);

                // All enemies rewarded for downing player (preserved)
                if (target.HP <= 0 && target.IsPlayer)
                    foreach (var b in _brains.Values) b.AddReward(1.0f);
            }
            else
            {
                Log.Add($"{attacker.Name} misses {target.Name}{advStr}. (d20={atkResult.NaturalRoll}, total={atkResult.Total} vs AC {target.AC})");
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(false, 0, target.Name);
            }

            // Q-learning: reward/penalize enemy attacker for hit/miss (preserved)
            if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var atkBrain))
                atkBrain.AddReward(atkResult.Hit ? 0.5f : -0.1f);
        }

        private bool IsAdjacent(BattleCombatant a, BattleCombatant b) =>
            System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) <= 1;

        private void CheckBattleEnd()
        {
            var player = Combatants.FirstOrDefault(c => c.IsPlayer);

            // Player is truly dead only if DeathSaveTracker says so (or no tracker and HP <= 0)
            bool playerDead = false;
            if (player == null)
            {
                playerDead = true;
            }
            else if (player.DeathSaves != null)
            {
                playerDead = player.DeathSaves.IsDead;
            }
            else
            {
                playerDead = !player.IsAlive;
            }

            if (playerDead)
            {
                BattleOver = true; PlayerWon = false;
                Log.Add("You have fallen...");
                Demo.AI.DemoAIIntegration.Instance?.OnPlayerDied();
            }
            else if (Combatants.All(c => c.IsPlayer || !c.IsAlive))
            {
                BattleOver = true; PlayerWon = true;
                Log.Add("Victory!");
                Demo.AI.DemoAIIntegration.Instance?.OnCombatVictory(_encounterData.GoldReward, _encounterData.XPReward);
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.LastBattleWon = true;
                    gm.LastBattleGoldEarned = _encounterData.GoldReward;
                    gm.LastBattleXPEarned = _encounterData.XPReward;
                    // Persist damage taken back to CharacterSheet and PlayerData
                    if (gm.Character != null)
                    {
                        gm.Character.HP = player.HP;
                        gm.SyncPlayerFromCharacter();
                    }
                    else
                    {
                        gm.Player.HP = player.HP;
                    }
                }
            }

            if (BattleOver)
            {
                float endReward = PlayerWon ? -0.5f : 0.5f;
                foreach (var b in _brains.Values) b.OnEpisodeEnd(endReward);

                // Save Q-table to LTM (preserved)
                var firstBrain = _brains.Values.FirstOrDefault();
                if (firstBrain != null)
                    Demo.AI.DemoAIIntegration.Instance?.SaveCombatQTable(firstBrain.SaveQTable());
            }
        }

        public void EndBattle()
        {
            if (PlayerWon)
                GameManager.Instance?.ReturnToOverworld();
            else
                GameManager.Instance?.PlayerDied();
        }
    }
}
