using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct AIExecutionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var store = MapDataStore.Instance;
            if (store == null || !store.Walkability.IsCreated) return;

            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();
            if (gameState.CurrentState != GameState.Combat &&
                gameState.CurrentState != GameState.Exploration)
                return;

            int2 playerPos = default;
            bool playerAlive = false;
            foreach (var (tag, pos, combat) in
                SystemAPI.Query<RefRO<PlayerTag>, RefRO<PositionComponent>, RefRO<CombatStateComponent>>())
            {
                playerPos = new int2(pos.ValueRO.X, pos.ValueRO.Y);
                playerAlive = combat.ValueRO.Alive;
                break;
            }

            var npcEntities = new NativeList<Entity>(32, Allocator.TempJob);
            var behaviors = new NativeList<AIBehaviorComponent>(32, Allocator.TempJob);
            var positions = new NativeList<PositionComponent>(32, Allocator.TempJob);
            var combatStates = new NativeList<CombatStateComponent>(32, Allocator.TempJob);
            var stats = new NativeList<StatsComponent>(32, Allocator.TempJob);

            foreach (var (ai, pos, combat, stat, entity) in
                SystemAPI.Query<RefRO<AIBehaviorComponent>, RefRO<PositionComponent>,
                    RefRO<CombatStateComponent>, RefRO<StatsComponent>>()
                    .WithEntityAccess())
            {
                if (!combat.ValueRO.Alive) continue;
                npcEntities.Add(entity);
                behaviors.Add(ai.ValueRO);
                positions.Add(pos.ValueRO);
                combatStates.Add(combat.ValueRO);
                stats.Add(stat.ValueRO);
            }

            if (npcEntities.Length == 0)
            {
                npcEntities.Dispose(); behaviors.Dispose(); positions.Dispose();
                combatStates.Dispose(); stats.Dispose();
                return;
            }

            var decisions = new NativeArray<AIDecision>(npcEntities.Length, Allocator.TempJob);

            var decisionJob = new AIDecisionJob
            {
                PlayerPosition = playerPos,
                PlayerAlive = playerAlive,
                FogGrid = store.FogGrid,
                MapWidth = store.Width,
                Behaviors = behaviors.AsArray(),
                Positions = positions.AsArray(),
                CombatStates = combatStates.AsArray(),
                Stats = stats.AsArray(),
                Decisions = decisions
            };

            var handle = decisionJob.Schedule(npcEntities.Length, 8, state.Dependency);
            handle.Complete();

            for (int i = 0; i < npcEntities.Length; i++)
            {
                var decision = decisions[i];
                if (decision.Action == AIAction.None) continue;

                var entity = npcEntities[i];
                var pos = state.EntityManager.GetComponentData<PositionComponent>(entity);

                if (decision.Action == AIAction.MoveTo)
                {
                    int dx = math.clamp(decision.TargetX - pos.X, -1, 1);
                    int dy = math.clamp(decision.TargetY - pos.Y, -1, 1);
                    int newX = pos.X + dx;
                    int newY = pos.Y + dy;

                    if (newX >= 0 && newX < store.Width && newY >= 0 && newY < store.Height)
                    {
                        if (store.Walkability[newY * store.Width + newX])
                        {
                            pos.X = newX;
                            pos.Y = newY;
                            state.EntityManager.SetComponentData(entity, pos);
                        }
                    }
                }
            }

            npcEntities.Dispose(); behaviors.Dispose(); positions.Dispose();
            combatStates.Dispose(); stats.Dispose(); decisions.Dispose();
        }
    }
}
