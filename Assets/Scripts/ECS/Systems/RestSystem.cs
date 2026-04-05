using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Utility;
using ForeverEngine.Data;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Processes short rest and long rest requests for player entities.
    ///
    /// Short rest:
    ///   - Player spends any number of hit dice to regain HP (HP += roll + CON mod per die).
    ///   - Some class features recharge (tracked via CharacterSheetComponent, extended in Plan 5).
    ///
    /// Long rest:
    ///   - Restore all HP to MaxHP.
    ///   - Recover half of spent hit dice (round down, minimum 1).
    ///   - Restore all expended spell slots.
    ///   - Remove most ongoing conditions (not exhaustion reduction).
    ///
    /// The RestManager MonoBehaviour adds RestRequestComponent to trigger these.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateSystem))]
    public partial struct RestSystem : ISystem
    {
        private uint _rngSeed;

        public void OnCreate(ref SystemState state)
        {
            _rngSeed = (uint)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ 0xF00D1234u;
            state.RequireForUpdate<GameStateSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();

            // Resting is only valid during exploration (not mid-combat)
            if (gameState.CurrentState != GameState.Exploration &&
                gameState.CurrentState != GameState.Paused) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (restReq, sheet, stats, entity) in
                SystemAPI.Query<
                    RefRO<RestRequestComponent>,
                    RefRW<CharacterSheetComponent>,
                    RefRW<StatsComponent>>()
                .WithEntityAccess())
            {
                switch (restReq.ValueRO.Type)
                {
                    case RestType.Short:
                        ProcessShortRest(ref sheet.ValueRW, ref stats.ValueRW,
                            restReq.ValueRO.HitDiceToSpend, ref _rngSeed);
                        break;

                    case RestType.Long:
                        ProcessLongRest(ref sheet.ValueRW, ref stats.ValueRW, entity, ref ecb);
                        break;
                }

                // Remove the request so it doesn't re-process next frame
                ecb.RemoveComponent<RestRequestComponent>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void ProcessShortRest(
            ref CharacterSheetComponent sheet,
            ref StatsComponent stats,
            int hitDiceToSpend,
            ref uint rng)
        {
            int availableDice = sheet.HitDiceTotal - sheet.HitDiceUsed;
            int diceToSpend = hitDiceToSpend <= availableDice ? hitDiceToSpend : availableDice;
            if (diceToSpend <= 0) return;

            int hitDieSides = InferHitDieSides(sheet.HitDieType);
            int conMod = DiceRoller.AbilityModifier(stats.Constitution);

            int hpGained = 0;
            for (int i = 0; i < diceToSpend; i++)
            {
                int roll = DiceRoller.Roll(1, hitDieSides, conMod, ref rng);
                hpGained += roll > 0 ? roll : 1; // minimum 1 HP per die
            }

            stats.HP = UnityEngine.Mathf.Min(stats.HP + hpGained, stats.MaxHP);
            sheet.HitDiceUsed += diceToSpend;
        }

        private static void ProcessLongRest(
            ref CharacterSheetComponent sheet,
            ref StatsComponent stats,
            Entity entity,
            ref EntityCommandBuffer ecb)
        {
            // Restore all HP
            stats.HP = stats.MaxHP;

            // Recover half spent hit dice (minimum 1 die recovered)
            int spent = sheet.HitDiceUsed;
            int recovered = spent / 2;
            if (recovered < 1 && spent > 0) recovered = 1;
            sheet.HitDiceUsed = UnityEngine.Mathf.Max(0, sheet.HitDiceUsed - recovered);

            // Restore all spell slots
            sheet.SpellSlot1Used = 0; sheet.SpellSlot2Used = 0;
            sheet.SpellSlot3Used = 0; sheet.SpellSlot4Used = 0;
            sheet.SpellSlot5Used = 0; sheet.SpellSlot6Used = 0;
            sheet.SpellSlot7Used = 0; sheet.SpellSlot8Used = 0;
            sheet.SpellSlot9Used = 0;

            // Clear concentration
            sheet.IsConcentrating = false;
            sheet.ConcentrationSpellId = default;

            // Remove non-permanent conditions (long rest clears most conditions)
            // We append a marker component; ConditionSystem handles the buffer clear
            ecb.AddComponent(entity, new LongRestConditionClearTag());
        }

        private static int InferHitDieSides(FixedString32Bytes hitDieType)
        {
            if (hitDieType == "d6")  return 6;
            if (hitDieType == "d10") return 10;
            if (hitDieType == "d12") return 12;
            return 8; // d8 default
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// One-frame tag added by RestSystem during a long rest.
    /// ConditionSystem removes all non-permanent conditions from the entity
    /// and then removes this tag.
    /// </summary>
    public struct LongRestConditionClearTag : IComponentData { }
}
