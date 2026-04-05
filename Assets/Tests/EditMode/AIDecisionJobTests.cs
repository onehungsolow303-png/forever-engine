using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Jobs;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.Tests
{
    public class AIDecisionJobTests
    {
        [Test]
        public void Chase_Adjacent_Attacks()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Chase, DetectRange = 12 };
            positions[0] = new PositionComponent { X = 5, Y = 5 };
            combats[0] = new CombatStateComponent { Alive = true, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(5, 6),
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.Attack, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }

        [Test]
        public void Chase_FarAway_Moves()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Chase, DetectRange = 12 };
            positions[0] = new PositionComponent { X = 1, Y = 1 };
            combats[0] = new CombatStateComponent { Alive = true, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(6, 6),
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.MoveTo, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }

        [Test]
        public void Static_DoesNothing()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Static };
            positions[0] = new PositionComponent { X = 3, Y = 3 };
            combats[0] = new CombatStateComponent { Alive = true, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(3, 4),
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.None, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }

        [Test]
        public void Dead_DoesNothing()
        {
            var behaviors = new NativeArray<AIBehaviorComponent>(1, Allocator.TempJob);
            var positions = new NativeArray<PositionComponent>(1, Allocator.TempJob);
            var combats = new NativeArray<CombatStateComponent>(1, Allocator.TempJob);
            var stats = new NativeArray<StatsComponent>(1, Allocator.TempJob);
            var decisions = new NativeArray<AIDecision>(1, Allocator.TempJob);
            var fog = new NativeArray<byte>(64, Allocator.TempJob);

            behaviors[0] = new AIBehaviorComponent { Type = AIType.Chase, DetectRange = 12 };
            positions[0] = new PositionComponent { X = 5, Y = 5 };
            combats[0] = new CombatStateComponent { Alive = false, HasAction = true };
            stats[0] = StatsComponent.Default;

            new AIDecisionJob
            {
                PlayerPosition = new int2(5, 6),
                PlayerAlive = true,
                FogGrid = fog, MapWidth = 8,
                Behaviors = behaviors, Positions = positions,
                CombatStates = combats, Stats = stats,
                Decisions = decisions
            }.Schedule(1, 1).Complete();

            Assert.AreEqual(AIAction.None, decisions[0].Action);

            behaviors.Dispose(); positions.Dispose(); combats.Dispose();
            stats.Dispose(); decisions.Dispose(); fog.Dispose();
        }
    }
}
