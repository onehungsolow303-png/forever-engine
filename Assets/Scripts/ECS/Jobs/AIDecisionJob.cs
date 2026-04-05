using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.ECS.Jobs
{
    /// <summary>
    /// Batch AI decision-making — rewritten from pygame ai.py ai_turn().
    /// Original: Sequential per-NPC greedy chase with single attack check.
    /// Rewrite: Parallel decision for all NPCs, with behavior types and tactics.
    ///
    /// This job determines WHAT each NPC wants to do. Pathfinding and execution
    /// happen in separate jobs/systems downstream.
    /// </summary>
    [BurstCompile]
    public struct AIDecisionJob : IJobParallelFor
    {
        // World state (read-only)
        [ReadOnly] public int2 PlayerPosition;
        [ReadOnly] public bool PlayerAlive;
        [ReadOnly] public NativeArray<byte> FogGrid;  // NPC can see player if in LOS
        [ReadOnly] public int MapWidth;

        // Per-NPC input
        [ReadOnly] public NativeArray<AIBehaviorComponent> Behaviors;
        [ReadOnly] public NativeArray<PositionComponent> Positions;
        [ReadOnly] public NativeArray<CombatStateComponent> CombatStates;
        [ReadOnly] public NativeArray<StatsComponent> Stats;

        // Per-NPC output: decided action
        [WriteOnly] public NativeArray<AIDecision> Decisions;

        public void Execute(int npcIndex)
        {
            var behavior = Behaviors[npcIndex];
            var pos = Positions[npcIndex];
            var combat = CombatStates[npcIndex];
            var stats = Stats[npcIndex];

            // Dead or not their turn: no action
            if (!combat.Alive || !combat.HasAction)
            {
                Decisions[npcIndex] = new AIDecision { Action = AIAction.None };
                return;
            }

            int distToPlayer = math.abs(pos.X - PlayerPosition.x) + math.abs(pos.Y - PlayerPosition.y);
            bool canSeePlayer = distToPlayer <= behavior.DetectRange;

            switch (behavior.Type)
            {
                case AIType.Static:
                    Decisions[npcIndex] = new AIDecision { Action = AIAction.None };
                    break;

                case AIType.Chase:
                    DecideChase(npcIndex, pos, distToPlayer, canSeePlayer, combat);
                    break;

                case AIType.Guard:
                    DecideGuard(npcIndex, behavior, pos, distToPlayer, canSeePlayer, combat);
                    break;

                case AIType.Patrol:
                    DecidePatrol(npcIndex, behavior, pos, distToPlayer, canSeePlayer, combat);
                    break;

                case AIType.Flee:
                    DecideFlee(npcIndex, pos, stats, distToPlayer, canSeePlayer);
                    break;

                case AIType.Wander:
                    Decisions[npcIndex] = new AIDecision
                    {
                        Action = AIAction.MoveTo,
                        TargetX = pos.X + ((npcIndex * 7 + 13) % 3 - 1), // Pseudo-random wander
                        TargetY = pos.Y + ((npcIndex * 11 + 7) % 3 - 1)
                    };
                    break;

                default:
                    Decisions[npcIndex] = new AIDecision { Action = AIAction.None };
                    break;
            }
        }

        private void DecideChase(int idx, PositionComponent pos, int dist, bool canSee, CombatStateComponent combat)
        {
            if (!PlayerAlive || !canSee)
            {
                Decisions[idx] = new AIDecision { Action = AIAction.None };
                return;
            }

            // Adjacent: attack. Otherwise: move toward player.
            if (dist <= 1 && combat.HasAction)
            {
                Decisions[idx] = new AIDecision
                {
                    Action = AIAction.Attack,
                    TargetX = PlayerPosition.x,
                    TargetY = PlayerPosition.y
                };
            }
            else
            {
                Decisions[idx] = new AIDecision
                {
                    Action = AIAction.MoveTo,
                    TargetX = PlayerPosition.x,
                    TargetY = PlayerPosition.y
                };
            }
        }

        private void DecideGuard(int idx, AIBehaviorComponent behavior, PositionComponent pos,
            int dist, bool canSee, CombatStateComponent combat)
        {
            int distFromHome = math.abs(pos.X - behavior.SpawnX) + math.abs(pos.Y - behavior.SpawnY);

            if (canSee && dist <= behavior.DetectRange)
            {
                // Player spotted — chase but respect leash
                if (distFromHome < behavior.LeashRange)
                    DecideChase(idx, pos, dist, canSee, combat);
                else
                {
                    // Return home
                    Decisions[idx] = new AIDecision
                    {
                        Action = AIAction.MoveTo,
                        TargetX = behavior.SpawnX,
                        TargetY = behavior.SpawnY
                    };
                }
            }
            else
            {
                Decisions[idx] = new AIDecision { Action = AIAction.None };
            }
        }

        private void DecidePatrol(int idx, AIBehaviorComponent behavior, PositionComponent pos,
            int dist, bool canSee, CombatStateComponent combat)
        {
            if (canSee && dist <= behavior.DetectRange)
            {
                DecideChase(idx, pos, dist, canSee, combat);
                return;
            }

            // Continue patrol (waypoint movement handled by patrol system)
            Decisions[idx] = new AIDecision
            {
                Action = AIAction.Patrol,
                TargetX = behavior.SpawnX, // Patrol home for now
                TargetY = behavior.SpawnY
            };
        }

        private void DecideFlee(int idx, PositionComponent pos, StatsComponent stats,
            int dist, bool canSee)
        {
            if (canSee && dist <= 8)
            {
                // Run away from player
                int fleeX = pos.X + (pos.X > PlayerPosition.x ? 1 : -1) * stats.Speed;
                int fleeY = pos.Y + (pos.Y > PlayerPosition.y ? 1 : -1) * stats.Speed;
                Decisions[idx] = new AIDecision
                {
                    Action = AIAction.MoveTo,
                    TargetX = fleeX,
                    TargetY = fleeY
                };
            }
            else
            {
                Decisions[idx] = new AIDecision { Action = AIAction.None };
            }
        }
    }

    public enum AIAction : byte
    {
        None = 0,
        MoveTo = 1,
        Attack = 2,
        Patrol = 3,
        Flee = 4,
        Interact = 5
    }

    public struct AIDecision
    {
        public AIAction Action;
        public int TargetX;
        public int TargetY;
    }
}
