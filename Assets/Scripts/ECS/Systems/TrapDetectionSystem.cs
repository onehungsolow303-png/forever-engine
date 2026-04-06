using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Passive trap detection: each exploration frame, compares the player's
    /// passive perception against each undetected trap's PerceptionDC.
    ///
    /// If detected:    sets TrapComponent.Detected = true (UI can now show an indicator).
    /// If player moves onto a trap tile while undetected: triggers the trap.
    ///   - Target makes a saving throw vs SaveDC.
    ///   - On failure: takes full Damage. On success: half damage (or no damage for some traps).
    ///
    /// Active Investigation checks (player action) are handled by the InputSystem
    /// which adds an InvestigateTileRequest component — resolved here as well.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameStateSystem))]
    public partial struct TrapDetectionSystem : ISystem
    {
        private uint _rngSeed;
        private const int DetectionRange = 2; // tiles; passive perception checks within this range

        public void OnCreate(ref SystemState state)
        {
            _rngSeed = (uint)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ 0xC0DE5AFEu;
            state.RequireForUpdate<GameStateSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingleton<GameStateSingleton>();
            if (gameState.CurrentState != GameState.Exploration) return;

            // ── Gather player data once (avoid nested queries) ─────────────
            int playerX = 0, playerY = 0;
            int passivePerception = 10;
            Entity playerEntity = Entity.Null;
            StatsComponent playerStats = default;
            CharacterSheetComponent playerSheet = default;
            bool foundPlayer = false;

            foreach (var (stats, sheet, combat, pos, entity) in
                SystemAPI.Query<
                    RefRO<StatsComponent>,
                    RefRO<CharacterSheetComponent>,
                    RefRO<CombatStateComponent>,
                    RefRO<PositionComponent>>()
                .WithAll<PlayerTag>()
                .WithEntityAccess())
            {
                if (!combat.ValueRO.Alive) continue;
                playerX           = pos.ValueRO.X;
                playerY           = pos.ValueRO.Y;
                playerEntity      = entity;
                playerStats       = stats.ValueRO;
                playerSheet       = sheet.ValueRO;

                int wisMod = DiceRoller.AbilityModifier(stats.ValueRO.Wisdom);
                // Assume perception proficiency for player; full skill system in Plan 5
                passivePerception = 10 + wisMod + sheet.ValueRO.ProficiencyBonus;
                foundPlayer       = true;
                break;
            }

            if (!foundPlayer) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (trap, pos, entity) in
                SystemAPI.Query<RefRW<TrapComponent>, RefRO<PositionComponent>>()
                .WithEntityAccess())
            {
                if (trap.ValueRO.Triggered || trap.ValueRO.Disarmed) continue;

                int dx = math.abs(pos.ValueRO.X - playerX);
                int dy = math.abs(pos.ValueRO.Y - playerY);

                // ── Passive detection check ────────────────────────────────
                if (!trap.ValueRO.Detected && dx <= DetectionRange && dy <= DetectionRange)
                {
                    if (passivePerception >= trap.ValueRO.PerceptionDC)
                    {
                        var t = trap.ValueRO;
                        t.Detected = true;
                        ecb.SetComponent(entity, t);
                        // UI system will pick up Detected=true and show indicator
                        continue;
                    }
                }

                // ── Trigger check: player steps on the trap (undetected) ────
                if (!trap.ValueRO.Detected && dx == 0 && dy == 0)
                {
                    TriggerTrap(ref ecb, entity, trap, playerEntity, playerStats, playerSheet);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void TriggerTrap(
            ref EntityCommandBuffer ecb,
            Entity trapEntity,
            RefRW<TrapComponent> trap,
            Entity playerEntity,
            StatsComponent playerStats,
            CharacterSheetComponent playerSheet)
        {
            var t = trap.ValueRO;
            t.Triggered = true;
            ecb.SetComponent(trapEntity, t);

            if (playerEntity == Entity.Null) return;

            // Saving throw
            int saveMod  = GetSaveMod(playerStats, t.SaveType);
            int saveRoll = DiceRoller.Roll(1, 20, saveMod, ref _rngSeed);
            bool saved   = saveRoll >= t.SaveDC;

            // Parse and roll trap damage
            DiceRoller.Parse(in t.Damage, out int count, out int sides, out int bonus);
            int fullDamage = DiceRoller.Roll(count, sides, bonus, ref _rngSeed);
            int damage     = saved ? fullDamage / 2 : fullDamage;

            playerStats.HP = math.max(0, playerStats.HP - damage);
            ecb.SetComponent(playerEntity, playerStats);
        }

        private static int GetSaveMod(StatsComponent stats, FixedString32Bytes saveType)
        {
            // In Plan 5, saving throw proficiencies will be stored per-entity.
            // For now, use raw ability modifier (no proficiency bonus).
            if (saveType == "DEX") return DiceRoller.AbilityModifier(stats.Dexterity);
            if (saveType == "CON") return DiceRoller.AbilityModifier(stats.Constitution);
            if (saveType == "STR") return DiceRoller.AbilityModifier(stats.Strength);
            if (saveType == "INT") return DiceRoller.AbilityModifier(stats.Intelligence);
            if (saveType == "WIS") return DiceRoller.AbilityModifier(stats.Wisdom);
            if (saveType == "CHA") return DiceRoller.AbilityModifier(stats.Charisma);
            return 0;
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
