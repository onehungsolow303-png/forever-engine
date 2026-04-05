using Unity.Entities;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Game state machine — rewritten from pygame game_engine.py.
    /// Manages EXPLORATION ↔ COMBAT ↔ MENU transitions.
    /// Singleton system that other systems query for current state.
    /// </summary>
    public enum GameState : byte
    {
        MainMenu = 0,
        Exploration = 1,
        Combat = 2,
        Dialogue = 3,
        Inventory = 4,
        Paused = 5,
        GameOver = 6
    }

    public struct GameStateSingleton : IComponentData
    {
        public GameState CurrentState;
        public GameState PreviousState;   // For unpausing back to correct state
        public int CombatRound;
        public int CurrentTurnEntityIndex;
        public float AITurnTimer;         // Delay before AI acts (visual feedback)
        public float AITurnDelay;         // Frames to wait (default 0.33s)
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct GameStateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new GameStateSingleton
            {
                CurrentState = GameState.MainMenu,
                PreviousState = GameState.MainMenu,
                CombatRound = 0,
                CurrentTurnEntityIndex = -1,
                AITurnTimer = 0f,
                AITurnDelay = 0.33f
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();

            switch (gameState.ValueRO.CurrentState)
            {
                case GameState.Exploration:
                    CheckCombatTrigger(ref state, ref gameState.ValueRW);
                    break;

                case GameState.Combat:
                    ProcessCombat(ref state, ref gameState.ValueRW);
                    break;

                case GameState.GameOver:
                    // No transitions from game over (handled by UI restart button)
                    break;
            }
        }

        private void CheckCombatTrigger(ref SystemState state, ref GameStateSingleton gameState)
        {
            // Find player position
            int2 playerPos = default;
            bool foundPlayer = false;

            foreach (var (combat, pos) in
                SystemAPI.Query<RefRO<CombatStateComponent>, RefRO<PositionComponent>>())
            {
                if (combat.ValueRO.TokenType == TokenType.Player)
                {
                    playerPos = new Unity.Mathematics.int2(pos.ValueRO.X, pos.ValueRO.Y);
                    foundPlayer = true;
                    break;
                }
            }

            if (!foundPlayer) return;

            // Check if any enemy is within detect range and visible
            foreach (var (combat, pos, ai) in
                SystemAPI.Query<RefRO<CombatStateComponent>, RefRO<PositionComponent>, RefRO<AIBehaviorComponent>>())
            {
                if (combat.ValueRO.TokenType != TokenType.Enemy || !combat.ValueRO.Alive)
                    continue;

                int dist = Unity.Mathematics.math.abs(pos.ValueRO.X - playerPos.x)
                         + Unity.Mathematics.math.abs(pos.ValueRO.Y - playerPos.y);

                if (dist <= ai.ValueRO.DetectRange)
                {
                    EnterCombat(ref gameState);
                    break;
                }
            }
        }

        private void EnterCombat(ref GameStateSingleton gameState)
        {
            gameState.PreviousState = gameState.CurrentState;
            gameState.CurrentState = GameState.Combat;
            gameState.CombatRound = 1;
            gameState.AITurnTimer = 0f;
            // Initiative rolling handled by CombatSystem
        }

        private void ProcessCombat(ref SystemState state, ref GameStateSingleton gameState)
        {
            // AI turn delay timer
            if (gameState.AITurnTimer > 0f)
            {
                gameState.AITurnTimer -= SystemAPI.Time.DeltaTime;
                return; // Wait for timer before AI acts
            }

            // Check combat end: all enemies dead = victory, player dead = game over
            bool anyEnemyAlive = false;
            bool playerAlive = false;

            foreach (var combat in SystemAPI.Query<RefRO<CombatStateComponent>>())
            {
                if (combat.ValueRO.TokenType == TokenType.Enemy && combat.ValueRO.Alive)
                    anyEnemyAlive = true;
                if (combat.ValueRO.TokenType == TokenType.Player && combat.ValueRO.Alive)
                    playerAlive = true;
            }

            if (!playerAlive)
            {
                gameState.CurrentState = GameState.GameOver;
            }
            else if (!anyEnemyAlive)
            {
                gameState.CurrentState = GameState.Exploration;
                gameState.CombatRound = 0;
            }
        }
    }
}
