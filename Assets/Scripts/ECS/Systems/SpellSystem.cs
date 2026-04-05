using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Resolves spell cast requests each frame.
    /// Reads SpellCastRequestComponent, validates slot availability,
    /// applies damage/healing/conditions, consumes the spell slot,
    /// and writes SpellCastResultComponent for the UI layer.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct SpellSystem : ISystem
    {
        private uint _rngSeed;

        public void OnCreate(ref SystemState state)
        {
            _rngSeed = (uint)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ 0xABCD1234u;
            state.RequireForUpdate<GameStateSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();

            // Spell casting is only valid during exploration or combat
            if (gameState.CurrentState != GameState.Exploration &&
                gameState.CurrentState != GameState.Combat) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, sheet, stats, entity) in
                SystemAPI.Query<
                    RefRO<SpellCastRequestComponent>,
                    RefRW<CharacterSheetComponent>,
                    RefRO<StatsComponent>>()
                .WithEntityAccess())
            {
                TryCastSpell(ref state, ref ecb, entity, request, ref sheet.ValueRW, stats.ValueRO);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void TryCastSpell(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity caster,
            RefRO<SpellCastRequestComponent> request,
            ref CharacterSheetComponent sheet,
            StatsComponent stats)
        {
            // Remove the request regardless of outcome
            ecb.RemoveComponent<SpellCastRequestComponent>(caster);

            int slotLevel = request.ValueRO.SlotLevel;

            // ── Validate: incapacitated or stunned? ──────────────────────
            if (HasBlockingCondition(ref state, caster))
            {
                AddResult(ref ecb, caster, request.ValueRO.SpellId, false, "incapacitated", 0, 0);
                return;
            }

            // ── Validate: cantrip (level 0) — no slot needed ─────────────
            bool isCantrip = slotLevel == 0;

            if (!isCantrip)
            {
                if (!sheet.HasSlotAvailable(slotLevel))
                {
                    AddResult(ref ecb, caster, request.ValueRO.SpellId, false, "no_slot", 0, 0);
                    return;
                }

                // Consume the slot
                ConsumeSlot(ref sheet, slotLevel);
            }

            // ── Resolve effect ────────────────────────────────────────────
            int damage = 0;
            int healing = 0;

            // TODO: look up SpellDatabase singleton for full effect resolution.
            // For now: produce a dice roll placeholder so the system compiles and runs.
            // Full implementation in Plan 5 when SpellDatabase is wired as a singleton.
            damage = DiceRoller.Roll(slotLevel + 1, 6, stats.IntMod, ref _rngSeed);

            // ── Apply to target ────────────────────────────────────────────
            if (request.ValueRO.Target != Entity.Null &&
                state.EntityManager.HasComponent<StatsComponent>(request.ValueRO.Target))
            {
                var targetStats = state.EntityManager.GetComponentData<StatsComponent>(request.ValueRO.Target);
                targetStats.HP = math.max(0, targetStats.HP - damage);
                ecb.SetComponent(request.ValueRO.Target, targetStats);
            }

            AddResult(ref ecb, caster, request.ValueRO.SpellId, true, "", damage, healing);
        }

        private static bool HasBlockingCondition(ref SystemState state, Entity entity)
        {
            if (!state.EntityManager.HasBuffer<ConditionBufferElement>(entity)) return false;
            var conditions = state.EntityManager.GetBuffer<ConditionBufferElement>(entity, true);
            for (int i = 0; i < conditions.Length; i++)
            {
                var name = conditions[i].ConditionName.ToString();
                if (name is "incapacitated" or "paralyzed" or "stunned" or "unconscious" or "petrified")
                    return true;
            }
            return false;
        }

        private static void ConsumeSlot(ref CharacterSheetComponent sheet, int level)
        {
            switch (level)
            {
                case 1: sheet.SpellSlot1Used++; break;
                case 2: sheet.SpellSlot2Used++; break;
                case 3: sheet.SpellSlot3Used++; break;
                case 4: sheet.SpellSlot4Used++; break;
                case 5: sheet.SpellSlot5Used++; break;
                case 6: sheet.SpellSlot6Used++; break;
                case 7: sheet.SpellSlot7Used++; break;
                case 8: sheet.SpellSlot8Used++; break;
                case 9: sheet.SpellSlot9Used++; break;
            }
        }

        private static void AddResult(
            ref EntityCommandBuffer ecb,
            Entity caster,
            FixedString64Bytes spellId,
            bool success,
            FixedString64Bytes failReason,
            int damage,
            int healing)
        {
            ecb.AddComponent(caster, new SpellCastResultComponent
            {
                SpellId = spellId,
                Success = success,
                FailReason = failReason,
                DamageDealt = damage,
                HealingDone = healing
            });
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
