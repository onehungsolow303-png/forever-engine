using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using ForeverEngine.ECS.Components;
using ForeverEngine.Data;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Awards XP to player characters when encounters are defeated,
    /// checks for level-up thresholds, and updates ProficiencyBonus.
    /// Also distributes XP from EncounterComponent.TotalXP when the encounter completes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateSystem))]
    public partial struct ExperienceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();

            // Only award XP when transitioning out of combat (enemies just died)
            if (gameState.CurrentState != GameState.Exploration) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Collect XP from any completed (activated but not yet awarded) encounters
            int totalXPToAward = 0;
            foreach (var (encounter, entity) in
                SystemAPI.Query<RefRW<EncounterComponent>>().WithEntityAccess())
            {
                if (encounter.ValueRO.Activated && encounter.ValueRO.TotalXP > 0)
                {
                    totalXPToAward += encounter.ValueRO.TotalXP;
                    // Zero out so we don't double-award
                    var enc = encounter.ValueRO;
                    enc.TotalXP = 0;
                    ecb.SetComponent(entity, enc);
                }
            }

            if (totalXPToAward > 0)
            {
                // Award XP to all living player characters, split equally
                int playerCount = CountLivingPlayers(ref state);
                int xpEach = playerCount > 0 ? totalXPToAward / playerCount : 0;

                if (xpEach > 0)
                {
                    foreach (var (combat, sheet, entity) in
                        SystemAPI.Query<
                            RefRO<CombatStateComponent>,
                            RefRW<CharacterSheetComponent>>()
                        .WithAll<PlayerTag>()
                        .WithEntityAccess())
                    {
                        if (!combat.ValueRO.Alive) continue;

                        var s = sheet.ValueRW;
                        s.ExperiencePoints += xpEach;
                        CheckAndApplyLevelUp(ref s);
                        ecb.SetComponent(entity, s);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int CountLivingPlayers(ref SystemState state)
        {
            int count = 0;
            foreach (var combat in SystemAPI.Query<RefRO<CombatStateComponent>>()
                .WithAll<PlayerTag>())
            {
                if (combat.ValueRO.Alive) count++;
            }
            return count;
        }

        /// <summary>
        /// Checks if the character has enough XP to level up (capped at level 20).
        /// Applies proficiency bonus update on each level gained.
        /// </summary>
        private static void CheckAndApplyLevelUp(ref CharacterSheetComponent sheet)
        {
            int maxLevel = InfinityRPGData.XPByLevel.Length - 1; // 20

            while (sheet.Level < maxLevel)
            {
                int nextLevel = sheet.Level + 1;
                if (sheet.ExperiencePoints >= InfinityRPGData.XPByLevel[nextLevel])
                {
                    sheet.Level = nextLevel;
                    sheet.ProficiencyBonus = InfinityRPGData.GetProficiencyBonus(sheet.Level);
                    // HP gain is handled by RestSystem/LevelUpManager (requires class data)
                    // Spell slot updates are handled by LevelUpManager (MonoBehaviour)
                }
                else
                {
                    break;
                }
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
