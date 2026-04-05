using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Monitors player position each frame during Exploration.
    /// When the player steps within EncounterComponent.TriggerRadius of an unactivated
    /// encounter entity, transitions the game to Combat and spawns the monster entities.
    ///
    /// Monster spawning uses the MonsterTemplateId to find the stat block in MonsterDatabase.
    /// Full monster-database lookup is handled by the bootstrap MonoBehaviour bridge
    /// (ContentLoader/HotContentLoader.cs) which listens for EncounterActivatedTag.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateSystem))]
    public partial struct EncounterTriggerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();

            // Only check triggers during exploration
            if (gameState.ValueRO.CurrentState != GameState.Exploration) return;

            // Find the player's position
            int2 playerPos = default;
            bool foundPlayer = false;

            foreach (var (combat, pos) in
                SystemAPI.Query<RefRO<CombatStateComponent>, RefRO<PositionComponent>>()
                .WithAll<PlayerTag>())
            {
                if (combat.ValueRO.Alive)
                {
                    playerPos = new int2(pos.ValueRO.X, pos.ValueRO.Y);
                    foundPlayer = true;
                    break;
                }
            }

            if (!foundPlayer) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (encounter, pos, entity) in
                SystemAPI.Query<RefRW<EncounterComponent>, RefRO<PositionComponent>>()
                .WithEntityAccess())
            {
                if (encounter.ValueRO.Activated) continue;

                float dx = pos.ValueRO.X - playerPos.x;
                float dy = pos.ValueRO.Y - playerPos.y;
                float dist = math.sqrt(dx * dx + dy * dy);

                if (dist <= encounter.ValueRO.TriggerRadius)
                {
                    // Mark as activated
                    var enc = encounter.ValueRO;
                    enc.Activated = true;
                    ecb.SetComponent(entity, enc);

                    // Signal to bootstrap: spawn monsters for this encounter
                    ecb.AddComponent(entity, new EncounterActivatedTag());

                    // Transition to Combat
                    gameState.ValueRW.PreviousState = GameState.Exploration;
                    gameState.ValueRW.CurrentState  = GameState.Combat;
                    gameState.ValueRW.CombatRound   = 1;
                    gameState.ValueRW.AITurnTimer   = 0f;

                    // Only trigger one encounter at a time
                    break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// One-frame tag added to an encounter entity when it fires.
    /// HotContentLoader (MonoBehaviour) watches for this tag to spawn monster GameObjects
    /// and ECS entities, then removes the tag.
    /// </summary>
    public struct EncounterActivatedTag : IComponentData { }
}
