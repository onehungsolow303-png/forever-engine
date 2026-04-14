using System.Collections.Generic;
using ForeverEngine.Demo.Battle;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Headless battle simulator for pre-training Q-tables without rendering.
    /// Menu: Forever Engine → Train Combat AI (500 battles)
    ///       Forever Engine → Train Combat AI (2000 battles)
    /// </summary>
    public static class SelfPlayTrainer
    {
        // ── Menu items ────────────────────────────────────────────────────────

        [MenuItem("Forever Engine/Train Combat AI (500 battles)")]
        public static void Train500() => RunTraining(500);

        [MenuItem("Forever Engine/Train Combat AI (2000 battles)")]
        public static void Train2000() => RunTraining(2000);

        // ── Core simulator ────────────────────────────────────────────────────

        private static void RunTraining(int battleCount)
        {
            var config = Resources.Load<GameConfig>("GameConfig");
            if (config == null)
            {
                Debug.LogError("[SelfPlayTrainer] GameConfig not found in Resources/. " +
                               "Run 'Forever Engine → Create Missing Assets' first.");
                return;
            }

            // Load the existing Q-table (or start fresh).
            float[] existingTable = QTableStore.Load();
            int priorEpisodes = QTableStore.LoadedEpisodes;

            // One shared brain for all enemies across the training run.
            // Seeds cycle so exploration varies battle-to-battle.
            var brain = new CombatBrain(existingTable, seed: 42);

            // Give full exploration during pre-training regardless of episode count.
            brain.SetExploration(config.QExplorationRate);

            var rng = new System.Random(1337);
            int totalEpisodes = priorEpisodes;

            for (int battle = 0; battle < battleCount; battle++)
            {
                totalEpisodes++;
                SimulateBattle(brain, config, rng);

                // Progress feedback every 100 battles.
                if ((battle + 1) % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "Training Combat AI",
                        $"Battle {battle + 1} / {battleCount}  (total episodes: {totalEpisodes})",
                        (float)(battle + 1) / battleCount);
                }
            }

            EditorUtility.ClearProgressBar();

            // Persist the updated Q-table.
            QTableStore.Save(brain.SaveQTable(), totalEpisodes);

            Debug.Log($"[SelfPlayTrainer] Training complete. " +
                      $"{battleCount} battles run. Total episodes stored: {totalEpisodes}.");
        }

        // ── Battle simulation ─────────────────────────────────────────────────

        private static void SimulateBattle(CombatBrain brain, GameConfig cfg, System.Random rng)
        {
            // --- Create simulated player ---
            int level = rng.Next(1, 6); // 1-5
            var player = new BattleCombatant
            {
                Name       = "Player",
                IsPlayer   = true,
                X          = 1, Y = 1,
                HP         = 8 + level * 4,
                MaxHP      = 8 + level * 4,
                AC         = 12 + rng.Next(0, 3), // 12-14
                Strength   = 12 + level,
                Dexterity  = 10,
                Speed      = 6,
                AtkCount   = 1,
                AtkSides   = 8,
                AtkBonus   = level,
                Behavior   = "player",
            };
            player.MaxHP = player.HP;

            // --- Create 1-4 simulated enemies ---
            int enemyCount = rng.Next(1, 5);
            var enemies = new List<BattleCombatant>(enemyCount);
            var enemyBrains = new List<CombatBrain>(enemyCount);

            for (int i = 0; i < enemyCount; i++)
            {
                bool hasRanged = rng.NextDouble() < 0.3;
                var enemy = new BattleCombatant
                {
                    Name            = $"Enemy{i}",
                    IsPlayer        = false,
                    X               = 4 + rng.Next(-2, 3),
                    Y               = 4 + rng.Next(-2, 3),
                    HP              = 6 + rng.Next(0, level * 3 + 1),
                    MaxHP           = 6 + rng.Next(0, level * 3 + 1),
                    AC              = 10 + rng.Next(0, 5), // 10-14
                    Strength        = 10 + rng.Next(0, 4),
                    Dexterity       = 10 + rng.Next(0, 4),
                    Speed           = 5,
                    AtkCount        = 1,
                    AtkSides        = 6,
                    AtkBonus        = rng.Next(0, 4),
                    Behavior        = rng.NextDouble() < 0.3 ? "guard" : "chase",
                    HasRangedAttack = hasRanged,
                    AttackRange     = hasRanged ? 6 : 1,
                };
                enemy.MaxHP = enemy.HP;
                enemies.Add(enemy);
                // Each enemy gets a fresh brain seeded differently but sharing the
                // same underlying Q-table for cooperative training.
                enemyBrains.Add(new CombatBrain(brain.SaveQTable(), seed: rng.Next()));
            }

            // --- Run combat rounds (max 20) ---
            const int MaxRounds = 20;
            uint diceSeed = (uint)rng.Next(1, int.MaxValue);

            for (int round = 0; round < MaxRounds; round++)
            {
                // Player attacks a random alive enemy.
                var aliveEnemies = AliveEnemies(enemies);
                if (aliveEnemies.Count == 0) break;

                var target = aliveEnemies[rng.Next(aliveEnemies.Count)];
                int attackRoll = Roll(1, 20, 0, ref diceSeed) + AbilityMod(player.Strength);
                if (attackRoll >= target.AC)
                {
                    int dmg = Roll(player.AtkCount, player.AtkSides, player.AtkBonus, ref diceSeed);
                    target.TakeDamage(dmg);

                    if (!target.IsAlive)
                    {
                        // Enemy killed — negative terminal reward applied at episode end.
                        // No per-step signal needed here; OnEpisodeEnd handles final signal.
                    }
                }

                // Each alive enemy takes its turn.
                aliveEnemies = AliveEnemies(enemies);
                if (aliveEnemies.Count == 0) break;

                bool allyHpCritical = AnyAllyHpCritical(aliveEnemies);

                for (int ei = 0; ei < enemies.Count; ei++)
                {
                    var enemy = enemies[ei];
                    if (!enemy.IsAlive) continue;

                    int aliveAllyCount = aliveEnemies.Count - 1; // exclude self
                    var action = brain.Decide(
                        enemy, player,
                        aliveAllyCount,
                        enemy.Behavior,
                        hasRanged: enemy.HasRangedAttack,
                        allyHpCritical: allyHpCritical,
                        terrainAdvantage: enemy.Behavior == "guard");

                    ResolveEnemyAction(action, enemy, player, brain, cfg, ref diceSeed, rng);
                }

                // Check win/loss after enemy turns.
                if (!player.IsAlive) break;
                if (AliveEnemies(enemies).Count == 0) break;
            }

            // --- Apply final episode rewards and end all brains ---
            bool playerDead   = !player.IsAlive;
            bool allEnemyDead = AliveEnemies(enemies).Count == 0;

            float finalRewardForEnemies = playerDead   ? cfg.RewardKill : 0f;
            float finalRewardForPlayer  = allEnemyDead ? cfg.RewardKill : 0f;

            // Update shared brain table from all enemy brains.
            brain.OnEpisodeEnd(finalRewardForEnemies);
            foreach (var eb in enemyBrains)
                eb.OnEpisodeEnd(finalRewardForEnemies);
        }

        // ── Action resolver ───────────────────────────────────────────────────

        private static void ResolveEnemyAction(
            CombatBrain.Action action,
            BattleCombatant enemy,
            BattleCombatant player,
            CombatBrain brain,
            GameConfig cfg,
            ref uint seed,
            System.Random rng)
        {
            int dist = Mathf.Abs(enemy.X - player.X) + Mathf.Abs(enemy.Y - player.Y);
            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 0f;

            switch (action)
            {
                case CombatBrain.Action.Attack:
                    if (dist <= 1)
                    {
                        brain.AddReward(cfg.RewardAttackAdjacent);
                        int roll = Roll(1, 20, 0, ref seed) + AbilityMod(enemy.Strength);
                        if (roll >= player.AC)
                        {
                            int dmg = Roll(enemy.AtkCount, enemy.AtkSides, enemy.AtkBonus, ref seed);
                            player.TakeDamage(dmg);
                            brain.AddReward(cfg.RewardHit);
                            brain.AddReward(cfg.PenaltyDamageTaken); // player took damage — enemy reward
                            if (!player.IsAlive)
                                brain.AddReward(cfg.RewardKill);
                        }
                        else
                        {
                            brain.AddReward(cfg.PenaltyMiss);
                        }
                    }
                    else
                    {
                        // Out of melee range — treat as advance.
                        MoveToward(enemy, player);
                        brain.AddReward(cfg.RewardAdvanceHit);
                    }
                    break;

                case CombatBrain.Action.Advance:
                    MoveToward(enemy, player);
                    brain.AddReward(cfg.RewardAdvanceHit);
                    break;

                case CombatBrain.Action.Retreat:
                    MoveAway(enemy, player);
                    if (hpRatio < 0.3f)
                        brain.AddReward(cfg.RewardRetreatLowHP);
                    else
                        brain.AddReward(cfg.PenaltyHoldChase); // retreating while healthy is bad
                    break;

                case CombatBrain.Action.Flank:
                    // Flanking: move perpendicular to approach vector.
                    enemy.X += (rng.NextDouble() < 0.5 ? 1 : -1);
                    brain.AddReward(cfg.RewardAdvanceHit * 0.5f);
                    break;

                case CombatBrain.Action.Hold:
                    if (enemy.Behavior == "guard")
                        brain.AddReward(cfg.RewardHoldGuard);
                    else
                        brain.AddReward(cfg.PenaltyHoldChase);
                    break;

                case CombatBrain.Action.UseAbility:
                    if (enemy.HasRangedAttack && dist <= enemy.AttackRange)
                    {
                        int roll = Roll(1, 20, 0, ref seed) + AbilityMod(enemy.Dexterity);
                        if (roll >= player.AC)
                        {
                            int dmg = Roll(
                                enemy.RangedAtkCount > 0 ? enemy.RangedAtkCount : enemy.AtkCount,
                                enemy.RangedAtkSides > 0 ? enemy.RangedAtkSides : enemy.AtkSides,
                                enemy.RangedAtkBonus > 0 ? enemy.RangedAtkBonus : enemy.AtkBonus,
                                ref seed);
                            player.TakeDamage(dmg);
                            brain.AddReward(cfg.RewardRangedHit);
                            if (!player.IsAlive)
                                brain.AddReward(cfg.RewardKill);
                        }
                        else
                        {
                            brain.AddReward(cfg.PenaltyRangedMiss);
                        }
                    }
                    else
                    {
                        // No ranged or out of range — fall back to advance.
                        MoveToward(enemy, player);
                        brain.AddReward(cfg.RewardAdvanceHit * 0.5f);
                    }
                    break;

                case CombatBrain.Action.ProtectAlly:
                    // Simulate moving toward a low-HP ally (approximated as not moving toward player).
                    brain.AddReward(cfg.RewardProtectAlly);
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<BattleCombatant> AliveEnemies(List<BattleCombatant> enemies)
        {
            var alive = new List<BattleCombatant>(enemies.Count);
            foreach (var e in enemies)
                if (e.IsAlive) alive.Add(e);
            return alive;
        }

        private static bool AnyAllyHpCritical(List<BattleCombatant> aliveEnemies)
        {
            foreach (var e in aliveEnemies)
                if (e.MaxHP > 0 && (float)e.HP / e.MaxHP < 0.25f)
                    return true;
            return false;
        }

        private static void MoveToward(BattleCombatant mover, BattleCombatant target)
        {
            int dx = target.X - mover.X;
            int dy = target.Y - mover.Y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                mover.X += dx > 0 ? 1 : -1;
            else
                mover.Y += dy > 0 ? 1 : -1;
        }

        private static void MoveAway(BattleCombatant mover, BattleCombatant target)
        {
            int dx = target.X - mover.X;
            int dy = target.Y - mover.Y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                mover.X -= dx > 0 ? 1 : -1;
            else
                mover.Y -= dy > 0 ? 1 : -1;
        }

        // Minimal XorShift32 dice roller — matches the DiceRoller seed convention.
        private static int Roll(int count, int sides, int bonus, ref uint seed)
        {
            int total = bonus;
            for (int i = 0; i < count; i++)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                total += (int)(seed % (uint)sides) + 1;
            }
            return total;
        }

        private static int AbilityMod(int score) => (score - 10) / 2;
    }
}
