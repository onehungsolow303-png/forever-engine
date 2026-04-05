using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Ticks status conditions down by one round at the end of each combat round.
    /// Removes expired conditions and applies ongoing effects (speed reduction, etc.).
    /// Runs once per combat round — gated behind the CombatRound counter changing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct ConditionSystem : ISystem
    {
        private int _lastProcessedRound;

        public void OnCreate(ref SystemState state)
        {
            _lastProcessedRound = 0;
            state.RequireForUpdate<GameStateSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // ── Long-rest condition clear (runs in any game state) ─────────
            foreach (var (conditions, entity) in
                SystemAPI.Query<DynamicBuffer<ConditionBufferElement>>()
                .WithAll<LongRestConditionClearTag>()
                .WithEntityAccess())
            {
                conditions.Clear();
                ecb.RemoveComponent<LongRestConditionClearTag>(entity);
            }

            // ── Combat condition ticking (only once per combat round) ──────
            if (gameState.CurrentState == GameState.Combat)
            {
                if (gameState.CombatRound != _lastProcessedRound)
                {
                    _lastProcessedRound = gameState.CombatRound;

                    foreach (var (conditions, stats, entity) in
                        SystemAPI.Query<
                            DynamicBuffer<ConditionBufferElement>,
                            RefRW<StatsComponent>>()
                        .WithEntityAccess())
                    {
                        TickConditions(conditions, entity, ref stats.ValueRW, ref ecb);
                    }
                }
            }
            else
            {
                // Outside combat: reset tracker so next combat starts fresh
                _lastProcessedRound = 0;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void TickConditions(
            DynamicBuffer<ConditionBufferElement> conditions,
            Entity entity,
            ref StatsComponent stats,
            ref EntityCommandBuffer ecb)
        {
            for (int i = conditions.Length - 1; i >= 0; i--)
            {
                var condition = conditions[i];

                // -1 = permanent, don't decrement
                if (condition.RemainingRounds > 0)
                {
                    condition.RemainingRounds--;
                    conditions[i] = condition;
                }

                if (condition.RemainingRounds == 0)
                {
                    conditions.RemoveAt(i);
                    // Condition expired — any temporary stat modifiers would be reversed here
                    continue;
                }

                // ── Apply ongoing effects ──────────────────────────────────
                // Poison: 1d4 damage at start of turn is handled in CombatSystem.
                // Speed: grappled and restrained effects applied via HasCondition queries in movement.
                // These tags inform movement/combat systems; we just track them here.
            }
        }

        public void OnDestroy(ref SystemState state) { }

        // ── Static query helper used by other systems ──────────────────────

        /// <summary>
        /// Returns true if the entity's condition buffer contains the named condition.
        /// Safe to call from any system; does not require ConditionSystem to be running.
        /// </summary>
        public static bool HasCondition(
            ref SystemState state,
            Entity entity,
            FixedString32Bytes conditionName)
        {
            if (!state.EntityManager.HasBuffer<ConditionBufferElement>(entity)) return false;
            var buf = state.EntityManager.GetBuffer<ConditionBufferElement>(entity, true);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].ConditionName == conditionName) return true;
            return false;
        }

        /// <summary>
        /// Appends a condition to an entity's buffer.
        /// Stacks new instances rather than refreshing, matching D&D 5e RAW for most conditions.
        /// </summary>
        public static void AddCondition(
            ref EntityCommandBuffer ecb,
            Entity target,
            FixedString32Bytes conditionName,
            int rounds,
            Entity source)
        {
            ecb.AppendToBuffer(target, new ConditionBufferElement
            {
                ConditionName = conditionName,
                RemainingRounds = rounds,
                Source = source
            });
        }
    }
}
