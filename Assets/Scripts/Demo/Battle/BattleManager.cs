using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ForeverEngine.ECS.Utility;

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
                // Attack nearest adjacent enemy with 1, or any key 1-9
                else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F))
                    AttackNearestEnemy();
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

            // Spawn player
            var player = BattleCombatant.FromPlayer(gm.Player);
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
            float[] savedTable = null; // Will load from LTM in Task 3
            foreach (var c in Combatants)
            {
                if (!c.IsPlayer && c.IsAlive)
                    _brains[c] = new CombatBrain(savedTable, seed: (int)_rngSeed + c.X * 100 + c.Y);
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
            if (!CurrentTurn.IsAlive) { NextTurn(); return; }
            CurrentTurn.StartTurn();

            if (!CurrentTurn.IsPlayer) ProcessAITurn();
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
            var action = brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");

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
            int roll = attacker.RollAttack(ref _rngSeed);
            bool hit = roll == 20 || (roll != 1 && roll + DiceRoller.AbilityModifier(attacker.Strength) >= target.AC);

            var ai = Demo.AI.DemoAIIntegration.Instance;
            if (hit)
            {
                int damage = attacker.RollDamage(ref _rngSeed);
                if (damage < 1) damage = 1;
                target.TakeDamage(damage);
                Log.Add($"{attacker.Name} hits {target.Name} for {damage}! (d20={roll})");

                // AI events
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(true, damage, target.Name);
                if (target.IsPlayer) ai?.OnPlayerDamaged(damage);

                if (!target.IsAlive)
                {
                    Log.Add($"{target.Name} is defeated!");
                    if (!target.IsPlayer) ai?.OnEnemyKilled(target.Name);
                }

                // Q-learning: penalize target enemy for taking damage
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(-0.3f);

                // All enemies rewarded for killing player
                if (!target.IsAlive && target.IsPlayer)
                    foreach (var b in _brains.Values) b.AddReward(1.0f);
            }
            else
            {
                Log.Add($"{attacker.Name} misses {target.Name}. (d20={roll})");
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(false, 0, target.Name);
            }

            // Q-learning: reward/penalize enemy attacker for hit/miss
            if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var atkBrain))
                atkBrain.AddReward(hit ? 0.5f : -0.1f);
        }

        private bool IsAdjacent(BattleCombatant a, BattleCombatant b) =>
            System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) <= 1;

        private void CheckBattleEnd()
        {
            var player = Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player == null || !player.IsAlive)
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
                    gm.Player.HP = player.HP; // Persist damage taken
                }
            }

            if (BattleOver)
            {
                float endReward = PlayerWon ? -0.5f : 0.5f;
                foreach (var b in _brains.Values) b.OnEpisodeEnd(endReward);
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
