using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Jobs;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateSystem))]
    public partial struct CombatSystem : ISystem
    {
        private NativeList<Entity> _turnOrder;
        private bool _combatActive;
        private uint _rngSeed;

        public void OnCreate(ref SystemState state)
        {
            _turnOrder = new NativeList<Entity>(16, Allocator.Persistent);
            _rngSeed = (uint)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();

            if (gameState.CurrentState != GameState.Combat)
            {
                if (_combatActive)
                {
                    _turnOrder.Clear();
                    _combatActive = false;
                }
                return;
            }

            if (!_combatActive)
            {
                RollInitiative(ref state);
                _combatActive = true;
            }
        }

        private void RollInitiative(ref SystemState state)
        {
            _turnOrder.Clear();

            foreach (var (combat, stats, entity) in
                SystemAPI.Query<RefRW<CombatStateComponent>, RefRO<StatsComponent>>()
                    .WithEntityAccess())
            {
                if (!combat.ValueRO.Alive) continue;

                int roll = DiceRoller.Roll(1, 20, 0, ref _rngSeed);
                combat.ValueRW.InitiativeRoll = roll + stats.ValueRO.DexMod;
                combat.ValueRW.MovementRemaining = stats.ValueRO.Speed;
                combat.ValueRW.HasAction = true;

                _turnOrder.Add(entity);
            }

            for (int i = 1; i < _turnOrder.Length; i++)
            {
                var key = _turnOrder[i];
                int keyInit = state.EntityManager.GetComponentData<CombatStateComponent>(key).InitiativeRoll;
                int j = i - 1;
                while (j >= 0)
                {
                    int jInit = state.EntityManager.GetComponentData<CombatStateComponent>(_turnOrder[j]).InitiativeRoll;
                    if (jInit >= keyInit) break;
                    _turnOrder[j + 1] = _turnOrder[j];
                    j--;
                }
                _turnOrder[j + 1] = key;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_turnOrder.IsCreated) _turnOrder.Dispose();
        }
    }
}
