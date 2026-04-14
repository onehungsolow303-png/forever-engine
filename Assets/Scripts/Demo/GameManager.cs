using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.RPG.Character;
using ForeverEngine.Bridges;
using ForeverEngine.Demo.Dungeon;
using ForeverEngine.Demo.UI;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.Demo
{
    public class GameManager : UnityEngine.MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerData Player { get; set; }
        public CharacterData CharacterData { get; private set; }
        public CharacterSheet Character { get; set; }
        public int CurrentSeed { get; set; } = 42;
        public string PendingEncounterId { get; set; }
        public string PendingLocationId { get; set; }
        public string PendingMapDataPath { get; set; }
        public DungeonState PendingDungeonState { get; set; }
        public bool LastBattleWon { get; set; }
        public int LastBattleGoldEarned { get; set; }
        public int LastBattleXPEarned { get; set; }
        public bool IsInCombat { get; private set; }
        private Battle.BattleManager _activeBattleManager;
        private readonly List<Battle.BattleZone> _activeZones = new();

        [Header("3D Transition")]
        [Tooltip("When true, loads the Overworld3D scene instead of the 2D Overworld scene.")]
        public bool Use3DOverworld = true;

        // Phase 3 pivot: HTTP bridges to Asset Manager (port 7801) and
        // Director Hub (port 7802). The C# brain (AIDirector, AIGameMaster,
        // MemoryManager) was archived to _archive/forever-engine-pre-pivot/
        // and replaced by these out-of-process Python services.
        public AssetClient Assets { get; private set; }
        public DirectorClient Director { get; private set; }
        public ServiceWatchdog Watchdog { get; private set; }
        public GameStateServer StateServer { get; private set; }
        public string SessionId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Assets = new AssetClient();
            Director = new DirectorClient();
            Watchdog = gameObject.AddComponent<ServiceWatchdog>();
            // GameStateServer exposes live engine state on 127.0.0.1:7803
            // for Director Hub's game_state_tool. Auto-starts on Awake.
            StateServer = gameObject.AddComponent<GameStateServer>();
            // Inventory screen — Tab-toggled, persists across scenes with GameManager.
            gameObject.AddComponent<InventoryScreen>();
        }

        private IEnumerator Start()
        {
            // Boot-time service health check. If either Python service is
            // down, log loudly but do not halt — the rest of the engine
            // (per-frame AI, RPG rules, rendering) is still functional and
            // the user can play in deterministic-fallback mode.
            yield return Watchdog.CheckAll();
            if (!Watchdog.AllOk)
            {
                Debug.LogError(
                    $"[GameManager] Backend services not fully available. " +
                    $"AssetManager={Watchdog.AssetManagerOk} " +
                    $"DirectorHub={Watchdog.DirectorHubOk}. " +
                    $"Game will run in deterministic-fallback mode.");
            }
        }

        public void NewGame(int seed = 42)
        {
            CurrentSeed = seed;
            Player = new PlayerData { HexQ = 2, HexR = 2 };
            Player.DiscoveredLocations.Add("camp");
            StartCoroutine(StartDirectorSession());
            SceneManager.LoadScene(Use3DOverworld ? "Overworld3D" : "Overworld");
        }

        /// <summary>
        /// Called by CharacterCreationUI when the player confirms their character.
        /// Converts CharacterData to PlayerData, then loads the Overworld.
        /// </summary>
        public void StartGameWithCharacter(CharacterData characterData, int seed = 0)
        {
            CharacterData = characterData;
            CurrentSeed   = seed > 0 ? seed : Random.Range(1, 99999);
            Player        = PlayerData.FromCharacterData(characterData);
            Player.DiscoveredLocations.Add("camp");
            StartCoroutine(StartDirectorSession());
            SceneManager.LoadScene(Use3DOverworld ? "Overworld3D" : "Overworld");
        }

        /// <summary>
        /// Called by the premade character selection buttons.
        /// Creates PlayerData from the CharacterSheet, then loads Overworld.
        /// </summary>
        public void StartGameWithSheet(CharacterSheet sheet, int seed = 0)
        {
            Character     = sheet;
            CurrentSeed   = seed > 0 ? seed : Random.Range(1, 99999);
            Player        = new PlayerData { HexQ = 2, HexR = 2 };
            Player.DiscoveredLocations.Add("camp");
            SyncPlayerFromCharacter();
            StartCoroutine(StartDirectorSession());
            SceneManager.LoadScene(Use3DOverworld ? "Overworld3D" : "Overworld");
        }

        /// <summary>
        /// Open a Director Hub session via /session/start so subsequent
        /// /interpret_action calls anchor to a real session_id instead of
        /// the "no-session" fallback. The Director Hub uses session_id to
        /// anchor its 4-tier memory (short / episodic / semantic / long)
        /// for conversation continuity across player turns.
        ///
        /// Fault-tolerant: if the Director is unreachable or returns an
        /// error, SessionId stays null and DirectorEvents falls back to
        /// the literal "no-session" string (which Director Hub still
        /// accepts for stateless requests).
        /// </summary>
        private IEnumerator StartDirectorSession()
        {
            if (Director == null || Player == null) yield break;

            var req = new DirectorClient.SessionStartRequestDto
            {
                PlayerProfile = new
                {
                    name = "Hero",
                    hp = Player.HP,
                    max_hp = Player.MaxHP,
                    level = Player.Level,
                    weapon = Player.WeaponName,
                    armor = Player.ArmorName,
                },
                MapMeta = new
                {
                    seed = CurrentSeed,
                },
            };

            yield return Director.StartSession(
                req,
                resp =>
                {
                    SessionId = resp?.SessionId;
                    Debug.Log($"[GameManager] Director session started: {SessionId}");
                },
                err =>
                {
                    Debug.LogWarning($"[GameManager] Director session start failed: {err}. Continuing with no-session fallback.");
                });
        }

        /// <summary>
        /// Copy CharacterSheet state into PlayerData for backward compatibility.
        /// Call after character creation, level up, equip changes, and rest.
        /// </summary>
        public void SyncPlayerFromCharacter()
        {
            if (Character == null || Player == null) return;
            RPGBridge.SyncPlayerFromCharacter(Character, Player);
        }

        /// <summary>
        /// Parses a Director Hub response for embedded quest markers and registers
        /// + starts the quest via QuestSystem. Markers:
        ///   QUEST_TITLE: &lt;title&gt;
        ///   QUEST_DESC: &lt;description&gt;
        ///   QUEST_OBJ: &lt;description&gt;|&lt;count&gt;
        ///   QUEST_REWARD_GOLD: &lt;amount&gt;
        ///   QUEST_REWARD_XP: &lt;amount&gt;
        /// If no QUEST_TITLE or QUEST_OBJ markers are found the response is
        /// treated as normal dialogue and this method returns silently.
        /// </summary>
        public void AcceptQuestFromResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return;

            string title = null, desc = null, objDesc = null;
            int objCount = 1, gold = 0, xp = 0;

            foreach (var line in response.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("QUEST_TITLE:"))
                    title = trimmed.Substring(12).Trim();
                else if (trimmed.StartsWith("QUEST_DESC:"))
                    desc = trimmed.Substring(11).Trim();
                else if (trimmed.StartsWith("QUEST_OBJ:"))
                {
                    var parts = trimmed.Substring(10).Trim().Split('|');
                    objDesc = parts[0].Trim();
                    if (parts.Length > 1) int.TryParse(parts[1].Trim(), out objCount);
                }
                else if (trimmed.StartsWith("QUEST_REWARD_GOLD:"))
                    int.TryParse(trimmed.Substring(18).Trim(), out gold);
                else if (trimmed.StartsWith("QUEST_REWARD_XP:"))
                    int.TryParse(trimmed.Substring(16).Trim(), out xp);
            }

            // If the response contains no quest markers it is normal dialogue — ignore.
            if (title == null || objDesc == null) return;

            var qs = QuestSystem.Instance;
            if (qs == null)
            {
                Debug.LogWarning("[GameManager] QuestSystem not available — quest not started.");
                return;
            }

            // Build a deterministic ID from the title so re-offering the same
            // quest by name doesn't create duplicate registrations.
            string questId = "quest_" + title.ToLower().Replace(' ', '_');

            var def = new QuestDefinition
            {
                Id          = questId,
                Title       = title,
                Description = desc ?? string.Empty,
                Objectives  = new QuestObjective[]
                {
                    new QuestObjective { Id = "kill", Description = objDesc, RequiredCount = objCount }
                },
                GoldReward  = gold,
                XPReward    = xp,
            };

            qs.RegisterQuest(def);
            var instance = qs.StartQuest(questId);
            if (instance != null)
                Debug.Log($"[GameManager] Quest accepted: '{title}' — {objDesc} x{objCount} for {gold}g / {xp} XP");
            else
                Debug.LogWarning($"[GameManager] StartQuest failed for id='{questId}'");
        }

        public void EnterBattle(string encounterId)
        {
            PendingEncounterId = encounterId;
            SceneManager.LoadScene("BattleMap");
        }

        public void StartSeamlessBattle(Vector3 position, string encounterId)
        {
            if (IsInCombat) return;

            var encounterData = Encounters.EncounterData.Get(encounterId);
            encounterData = Encounters.EncounterManager.Instance?.ScaleEncounter(encounterData) ?? encounterData;

            _activeZones.Clear();
            var rng = new System.Random(encounterId.GetHashCode());
            float offset = 0f;
            foreach (var enemyDef in encounterData.Enemies)
            {
                var zoneGO = new GameObject($"BattleZone_{enemyDef.Name}");
                var zone = zoneGO.AddComponent<Battle.BattleZone>();
                Vector3 enemyPos = position + new Vector3(offset, 0, offset * 0.5f);
                var combatant = Battle.BattleCombatant.FromEnemy(enemyDef, 0, 0);
                zone.Activate(combatant, enemyPos);

                var (gx, gy) = zone.WorldToGrid(enemyPos);
                combatant.X = gx;
                combatant.Y = gy;

                _activeZones.Add(zone);
                offset += 2f;
            }

            if (_activeBattleManager == null)
            {
                var bmGO = new GameObject("BattleManager");
                _activeBattleManager = bmGO.AddComponent<Battle.BattleManager>();
            }

            Battle.BattleCombatant playerCombatant;
            if (Character != null)
                playerCombatant = Battle.BattleCombatant.FromCharacterSheet(Character);
            else
                playerCombatant = Battle.BattleCombatant.FromPlayer(Player);

            if (_activeZones.Count > 0)
            {
                playerCombatant.X = Battle.BattleZone.GridSize / 2;
                playerCombatant.Y = Battle.BattleZone.GridSize / 2 - 2;
            }

            var enemies = new List<Battle.BattleCombatant>();
            foreach (var zone in _activeZones)
                enemies.Add(zone.OwnerEnemy);

            _activeBattleManager.StartSeamlessBattle(_activeZones, enemies, playerCombatant, encounterData);

            PendingEncounterId = encounterId;
            IsInCombat = true;
        }

        public void OnBattleComplete(bool playerWon, Encounters.EncounterData encounterData)
        {
            if (playerWon && encounterData != null)
            {
                int goldPer = encounterData.GoldReward / Mathf.Max(1, encounterData.Enemies.Count);
                int xpPer = encounterData.XPReward / Mathf.Max(1, encounterData.Enemies.Count);
                foreach (var zone in _activeZones)
                {
                    if (zone != null && zone.OwnerEnemy != null && !zone.OwnerEnemy.IsAlive)
                    {
                        var lootGO = new GameObject("WorldLoot");
                        lootGO.transform.position = zone.GridToWorld(zone.OwnerEnemy.X, zone.OwnerEnemy.Y) + Vector3.up * 0.5f;
                        var loot = lootGO.AddComponent<Battle.WorldLoot>();
                        loot.GoldAmount = goldPer;
                        loot.XPAmount = xpPer;
                    }
                }

                // Generate equipment drops via LootGenerator
                // Parse tier from encounter ID for rarity scaling
                int lootTier = 1;
                string encId = encounterData.Id ?? "";
                if (encId.Contains("_t1_")) lootTier = 1;
                else if (encId.Contains("_t2_")) lootTier = 2;
                else if (encId.Contains("_t3_")) lootTier = 3;
                var lootItems = Battle.LootGenerator.GenerateLoot(encounterData.XPReward, encounterData.GoldReward, lootTier);
                foreach (var itemName in lootItems)
                {
                    Debug.Log($"[GameManager] Equipment drop: {itemName}");
                    // Register the name so ItemRegistry can reverse-lookup this hash later
                    ItemRegistry.Register(itemName);
                    // Add to player inventory if available
                    if (Player?.Inventory != null)
                        Player.Inventory.Add(new ForeverEngine.ECS.Data.ItemInstance { ItemId = itemName.GetHashCode(), StackCount = 1, MaxStack = 1 });
                }

                // Persist all battle rewards (gold, XP, loot) before returning
                // to the overworld or dungeon, so a crash/quit cannot roll them back.
                SaveManager.Save();

                // Progress active quest objectives for this encounter's enemy kills.
                // All active quests use the "kill" objective ID (set by AcceptQuestFromResponse).
                var qs = QuestSystem.Instance;
                if (qs != null)
                {
                    int killCount = encounterData.Enemies.Count;
                    foreach (var quest in qs.GetActiveQuests())
                        qs.ProgressQuest(quest.Definition.Id, "kill", killCount);
                }
            }

            foreach (var zone in _activeZones)
                if (zone != null) zone.Deactivate();
            _activeZones.Clear();

            if (_activeBattleManager != null)
            {
                Destroy(_activeBattleManager.gameObject);
                _activeBattleManager = null;
            }

            LastBattleWon = playerWon;
            IsInCombat = false;
            PendingEncounterId = null;

            if (!playerWon)
                PlayerDied();
        }

        /// <summary>
        /// Load (or resume) a dungeon exploration session for a location.
        /// Creates a fresh DungeonState if none exists for this location.
        /// </summary>
        public void EnterDungeon(string locationId)
        {
            PendingLocationId = locationId;
            if (PendingDungeonState == null || PendingDungeonState.LocationId != locationId)
                PendingDungeonState = new DungeonState { LocationId = locationId };
            SceneManager.LoadScene("DungeonExploration");
        }

        public void ReturnToOverworld()
        {
            // If we won a battle while inside a dungeon, return to dungeon exploration
            // instead of the overworld so the player can continue to the next room.
            if (LastBattleWon && PendingDungeonState != null)
            {
                LastBattleWon = false;
                SceneManager.LoadScene("DungeonExploration");
                return;
            }

            PendingEncounterId = null;
            PendingLocationId = null;
            PendingDungeonState = null;
            SceneManager.LoadScene(Use3DOverworld ? "Overworld3D" : "Overworld");
        }

        public void PlayerDied()
        {
            // Respawn at last safe location, fully restored. Previously
            // restored to half HP / half resources, which compounded into
            // "die in second combat with no turn" because the next encounter
            // could one-shot a 10/20-HP player. Roguelike difficulty is fine,
            // but losing without an action is universally frustrating.
            Player.HP = Player.MaxHP;
            Player.Hunger = Player.MaxHunger;
            Player.Thirst = Player.MaxThirst;
            var loc = LocationData.Get(Player.LastSafeLocation);
            if (loc != null) { Player.HexQ = loc.HexQ; Player.HexR = loc.HexR; }
            ReturnToOverworld();
        }

        public void GameComplete()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
