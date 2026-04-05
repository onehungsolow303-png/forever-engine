using UnityEngine;
using ForeverEngine.AI.Director;
using ForeverEngine.AI.Learning;
using ForeverEngine.AI.PlayerModeling;
using ForeverEngine.AI.Memory;
using ForeverEngine.Demo.Encounters;
using ForeverEngine.Demo.Overworld;

namespace ForeverEngine.Demo.AI
{
    /// <summary>
    /// Wires all AI systems into the Shattered Kingdom demo.
    /// Attach to a persistent GameObject alongside GameManager.
    /// Connects: AI Director ↔ Encounters, Player Profiler ↔ Combat,
    /// Dynamic Difficulty ↔ Enemy scaling, Memory ↔ NPC reactions.
    /// </summary>
    public class DemoAIIntegration : UnityEngine.MonoBehaviour
    {
        public static DemoAIIntegration Instance { get; private set; }

        // Runtime tracking
        private int _totalKills;
        private int _totalDeaths;
        private int _encounterCount;
        private float _combatTimeAccum;
        private float _exploreTimeAccum;
        private bool _inCombat;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;

            // Track time in combat vs exploration
            if (_inCombat)
                _combatTimeAccum += Time.deltaTime;
            else
                _exploreTimeAccum += Time.deltaTime;

            // Feed profiler with time data every frame
            var profiler = PlayerProfiler.Instance;
            if (profiler != null)
            {
                profiler.RecordCombatTime(_combatTimeAccum);
                profiler.RecordExploreTime(_exploreTimeAccum);
                _combatTimeAccum = 0;
                _exploreTimeAccum = 0;
            }
        }

        // === Called by OverworldManager when player moves ===
        public void OnPlayerMoved(int hexQ, int hexR)
        {
            _inCombat = false;

            // Director: decrease intensity during exploration
            var director = AIDirector.Instance;
            if (director != null)
                director.Pacing.AddIntensity(-0.02f);
        }

        // === Called by OverworldManager when entering a location ===
        public void OnLocationDiscovered(string locationId)
        {
            // Memory: record discovery
            var mem = MemoryManager.Instance;
            if (mem != null)
            {
                mem.Episodic.Record(new Episode
                {
                    Timestamp = Time.time,
                    Actor = "Player",
                    Action = "discovered",
                    Target = locationId,
                    Importance = 0.7f
                });
                mem.LongTerm.Set($"discovered.{locationId}", "true");
            }

            // Director: discovering a location is a drama event
            var director = AIDirector.Instance;
            if (director != null)
                director.Drama.TriggerDrama(0.3f);
        }

        // === Called by BattleManager when combat starts ===
        public void OnCombatStarted(string encounterId)
        {
            _inCombat = true;
            _encounterCount++;

            var director = AIDirector.Instance;
            if (director != null)
                director.Pacing.AddIntensity(0.3f);

            var mem = MemoryManager.Instance;
            if (mem != null)
            {
                mem.ShortTerm.RecordEvent("combat_started", Vector3.zero, data: encounterId);
                mem.Episodic.Record(new Episode
                {
                    Timestamp = Time.time,
                    Actor = "Player",
                    Action = "entered_combat",
                    Target = encounterId,
                    Importance = 0.5f
                });
            }
        }

        // === Called by BattleManager when player attacks ===
        public void OnPlayerAttacked(bool hit, int damage, string targetName)
        {
            var profiler = PlayerProfiler.Instance;
            if (profiler != null)
                profiler.RecordMeleeAttack();

            // Director: combat intensity spike
            var director = AIDirector.Instance;
            if (director != null)
                director.Pacing.AddIntensity(hit ? 0.05f : 0.02f);
        }

        // === Called by BattleManager when player kills an enemy ===
        public void OnEnemyKilled(string enemyName)
        {
            _totalKills++;

            var dda = DynamicDifficulty.Instance;
            if (dda != null)
                dda.RecordKill();

            var mem = MemoryManager.Instance;
            if (mem != null)
            {
                mem.Episodic.Record(new Episode
                {
                    Timestamp = Time.time,
                    Actor = "Player",
                    Action = "killed",
                    Target = enemyName,
                    Importance = 0.6f
                });
                int kills = mem.LongTerm.GetInt("stats.total_kills");
                mem.LongTerm.Set("stats.total_kills", kills + 1);
            }

            // Director: killing is dramatic
            var director = AIDirector.Instance;
            if (director != null)
                director.Pacing.AddIntensity(0.1f);
        }

        // === Called by BattleManager when player takes damage ===
        public void OnPlayerDamaged(int damage)
        {
            var director = AIDirector.Instance;
            if (director != null)
                director.Pacing.AddIntensity(0.08f);

            var mem = MemoryManager.Instance;
            if (mem != null)
                mem.ShortTerm.RecordEvent("player_damaged", Vector3.zero, data: damage.ToString());
        }

        // === Called by BattleManager when player dies ===
        public void OnPlayerDied()
        {
            _totalDeaths++;

            var dda = DynamicDifficulty.Instance;
            if (dda != null)
                dda.RecordDeath();

            _inCombat = false;

            var director = AIDirector.Instance;
            if (director != null)
            {
                director.Pacing.SetIntensity(0f); // Reset after death
                director.Drama.TriggerDrama(1f);  // Death is very dramatic
            }

            var mem = MemoryManager.Instance;
            if (mem != null)
            {
                mem.Episodic.Record(new Episode
                {
                    Timestamp = Time.time,
                    Actor = "Player",
                    Action = "died",
                    Target = "combat",
                    Importance = 1f
                });
                int deaths = mem.LongTerm.GetInt("stats.total_deaths");
                mem.LongTerm.Set("stats.total_deaths", deaths + 1);
            }
        }

        // === Called by BattleManager when combat ends in victory ===
        public void OnCombatVictory(int goldEarned, int xpEarned)
        {
            _inCombat = false;

            var director = AIDirector.Instance;
            if (director != null)
            {
                director.Pacing.AddIntensity(-0.15f); // Relief after victory
                director.Drama.TriggerDrama(0.5f);
            }

            var mem = MemoryManager.Instance;
            if (mem != null)
            {
                mem.LongTerm.Set("stats.total_gold_earned",
                    mem.LongTerm.GetInt("stats.total_gold_earned") + goldEarned);
            }
        }

        // === Called by EncounterManager to check if Director wants to suppress ===
        public bool ShouldSuppressEncounter()
        {
            var director = AIDirector.Instance;
            if (director == null) return false;

            // Suppress if intensity is high (player just had a fight)
            if (director.Pacing.CurrentIntensity > 0.6f) return true;

            // Suppress if player just died recently (give them a break)
            var mem = MemoryManager.Instance;
            if (mem != null)
            {
                var recentDeaths = mem.ShortTerm.GetEventsByType("player_died");
                if (recentDeaths.Count > 0) return true;
            }

            return false;
        }

        // === Called by EncounterManager to scale encounter based on profiling ===
        public EncounterData AdaptEncounter(EncounterData baseData)
        {
            var profile = PlayerProfiler.CurrentProfile;
            var dda = DynamicDifficulty.Instance;

            if (profile != null)
            {
                // If player is aggressive, add more enemies for challenge
                if (profile.Playstyle.AggressiveVsCautious > 0.7f && baseData.Enemies.Count < 5)
                {
                    var extra = baseData.Enemies[baseData.Enemies.Count - 1]; // Clone last enemy type
                    baseData.Enemies.Add(new Encounters.EnemyDef
                    {
                        Name = extra.Name, HP = extra.HP, AC = extra.AC,
                        Str = extra.Str, Dex = extra.Dex, Spd = extra.Spd,
                        AtkDice = extra.AtkDice, Behavior = extra.Behavior
                    });
                    baseData.GoldReward = (int)(baseData.GoldReward * 1.3f);
                    Debug.Log("[AI] Added extra enemy for aggressive player");
                }

                // If player is cautious, reduce enemy count slightly
                if (profile.Playstyle.AggressiveVsCautious < 0.3f && baseData.Enemies.Count > 1)
                {
                    baseData.Enemies.RemoveAt(baseData.Enemies.Count - 1);
                    Debug.Log("[AI] Removed an enemy for cautious player");
                }
            }

            // Apply DDA scaling
            if (dda != null)
            {
                foreach (var enemy in baseData.Enemies)
                {
                    enemy.HP = Mathf.RoundToInt(enemy.HP * dda.EnemyHealthMult);
                    enemy.HP = Mathf.Max(1, enemy.HP);
                }
                baseData.GoldReward = Mathf.RoundToInt(baseData.GoldReward * dda.LootQualityMult);
            }

            return baseData;
        }

        // === Debug info for HUD ===
        public string GetAIStatusText()
        {
            var director = AIDirector.Instance;
            var dda = DynamicDifficulty.Instance;
            var profile = PlayerProfiler.CurrentProfile;

            string text = "";
            if (director != null)
                text += $"Pacing: {director.Pacing.CurrentIntensity:F2} | Drama: {director.Drama.DramaNeed:F2}\n";
            if (dda != null)
                text += $"Difficulty: {dda.CurrentLevel:F2} (HP×{dda.EnemyHealthMult:F1} DMG×{dda.EnemyDamageMult:F1})\n";
            if (profile != null)
                text += $"Style: {profile.GetPrimaryArchetype()}\n";
            text += $"K/D: {_totalKills}/{_totalDeaths} | Encounters: {_encounterCount}";
            return text;
        }
    }
}
