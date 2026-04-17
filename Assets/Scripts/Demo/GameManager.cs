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
        private Battle.BattleZoneManager _activeZoneManager;
        private Dictionary<string, object> _lastDecisionEncounterTemplate;

        [Header("3D Transition")]
        [Tooltip("When true, loads the Overworld3D scene instead of the 2D Overworld scene.")]
        public bool Use3DOverworld = true;

        [Header("Procedural World")]
        [Tooltip("When true, loads the procedural World scene instead of Overworld3D.")]
        public bool UseProceduralWorld = true;

        // Phase 3 pivot: HTTP bridges to Asset Manager (port 7801) and
        // Director Hub (port 7802). The C# brain (AIDirector, AIGameMaster,
        // MemoryManager) was archived to _archive/forever-engine-pre-pivot/
        // and replaced by these out-of-process Python services.
        public AssetClient Assets { get; private set; }
        public DirectorClient Director { get; private set; }
        public ServiceWatchdog Watchdog { get; private set; }
        public string SessionId { get; set; }

        /// <summary>
        /// True when the multiplayer ConnectionManager has completed login.
        /// Server handles Director Hub communication; GameManager no longer
        /// boots or sessions the Director directly.
        /// </summary>
        public bool IsConnected => ForeverEngine.Network.ConnectionManager.Instance != null &&
                                   ForeverEngine.Network.ConnectionManager.Instance.IsLoggedIn;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Assets = new AssetClient();
            Director = new DirectorClient();
            Watchdog = gameObject.AddComponent<ServiceWatchdog>();
            // Inventory screen — Tab-toggled, persists across scenes with GameManager.
            gameObject.AddComponent<InventoryScreen>();
            // Level-up screen — shown when XP threshold is reached after loot collection.
            gameObject.AddComponent<LevelUpScreen>();
            // Victory screen — shown on defeat of the castle_boss (The Rot King).
            gameObject.AddComponent<VictoryScreen>();
            // Pause menu — Escape-toggled, with save/load/quit. F5/F9 quicksave/quickload.
            gameObject.AddComponent<PauseMenu>();
        }

        private IEnumerator Start()
        {
            // Wait for multiplayer login. The server handles Director Hub
            // boot and session management via DirectorBridge on the server side.
            while (ForeverEngine.Network.ConnectionManager.Instance == null ||
                   !ForeverEngine.Network.ConnectionManager.Instance.IsLoggedIn)
                yield return null;

            Debug.Log("[GameManager] Connected and logged in.");

            // SessionId can be sourced from the server's player identity.
            SessionId = ForeverEngine.Network.ConnectionManager.Instance.PlayerId;
        }

        public void NewGame(int seed = 42)
        {
            CurrentSeed = seed;
            Player = new PlayerData { HexQ = 2, HexR = 2 };
            Player.DiscoveredLocations.Add("camp");
            SceneManager.LoadScene(UseProceduralWorld ? "World" : Use3DOverworld ? "Overworld3D" : "Overworld");
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
            SceneManager.LoadScene(UseProceduralWorld ? "World" : Use3DOverworld ? "Overworld3D" : "Overworld");
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
            SceneManager.LoadScene(UseProceduralWorld ? "World" : Use3DOverworld ? "Overworld3D" : "Overworld");
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

        /// <summary>Store an encounter template from Director Hub for the next battle.</summary>
        public void SetEncounterTemplate(Dictionary<string, object> template)
        {
            _lastDecisionEncounterTemplate = template;
        }

        public void EnterBattle(string encounterId)
        {
            PendingEncounterId = encounterId;
            SceneManager.LoadScene("BattleMap");
        }

        public void StartSeamlessBattle(Vector3 position, string encounterId)
        {
            if (IsInCombat) return;

            Encounters.EncounterData encounterData;
            if (_lastDecisionEncounterTemplate != null)
            {
                string biome = _lastDecisionEncounterTemplate.ContainsKey("biome")
                    ? _lastDecisionEncounterTemplate["biome"].ToString() : "forest";
                encounterData = Encounters.EncounterData.FromDirectorTemplate(_lastDecisionEncounterTemplate, biome);
                _lastDecisionEncounterTemplate = null;
            }
            else
            {
                encounterData = Encounters.EncounterData.Get(encounterId);
                encounterData = Encounters.EncounterManager.Instance?.ScaleEncounter(encounterData) ?? encounterData;
            }

            // Build player combatant
            Battle.BattleCombatant playerCombatant;
            if (Character != null)
                playerCombatant = Battle.BattleCombatant.FromCharacterSheet(Character);
            else
                playerCombatant = Battle.BattleCombatant.FromPlayer(Player);
            int playerSpeed = playerCombatant.Speed > 0 ? playerCombatant.Speed : 6;

            // Place enemies in a circle around the player position
            var enemies = new List<Battle.BattleCombatant>();
            int count = encounterData.Enemies.Count;
            float radius = 3f + count; // Scale circle with enemy count
            for (int i = 0; i < count; i++)
            {
                float angle = (2f * Mathf.PI * i) / Mathf.Max(1, count);
                Vector3 enemyPos = position + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                var combatant = Battle.BattleCombatant.FromEnemy(encounterData.Enemies[i], 0, 0);
                combatant.SpawnWorldPos = enemyPos;
                enemies.Add(combatant);
            }

            // Create per-NPC BattleZoneManager
            var zmGO = new GameObject("BattleZoneManager");
            _activeZoneManager = zmGO.AddComponent<Battle.BattleZoneManager>();
            _activeZoneManager.Initialize(enemies, position);

            // Place enemies on the unified grid
            foreach (var enemy in enemies)
            {
                var (gx, gy) = _activeZoneManager.WorldToGrid(enemy.SpawnWorldPos);
                enemy.X = gx;
                enemy.Y = gy;
            }

            // Place player at their world position on the unified grid
            var (px, py) = _activeZoneManager.WorldToGrid(position);
            playerCombatant.X = px;
            playerCombatant.Y = py;
            playerCombatant.SpawnWorldPos = position;

            if (_activeBattleManager == null)
            {
                var bmGO = new GameObject("BattleManager");
                _activeBattleManager = bmGO.AddComponent<Battle.BattleManager>();
            }

            _activeBattleManager.StartSeamlessBattle(_activeZoneManager, enemies, playerCombatant, encounterData);

            PendingEncounterId = encounterId;
            IsInCombat = true;
        }

        public void OnBattleComplete(bool playerWon, Encounters.EncounterData encounterData)
        {
            if (playerWon && encounterData != null)
            {
                int goldPer = encounterData.GoldReward / Mathf.Max(1, encounterData.Enemies.Count);
                int xpPer = encounterData.XPReward / Mathf.Max(1, encounterData.Enemies.Count);
                if (_activeZoneManager != null && _activeBattleManager != null)
                {
                    foreach (var c in _activeBattleManager.Combatants)
                    {
                        if (c != null && !c.IsPlayer && !c.IsAlive)
                        {
                            var lootGO = new GameObject("WorldLoot");
                            lootGO.transform.position = _activeZoneManager.GridToWorld(c.X, c.Y) + Vector3.up * 0.5f;
                            var loot = lootGO.AddComponent<Battle.WorldLoot>();
                            loot.GoldAmount = goldPer;
                            loot.XPAmount = xpPer;
                        }
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

                // Progress active quest objectives for this encounter's enemy kills.
                var qs = QuestSystem.Instance;
                if (qs != null)
                {
                    int killCount = encounterData.Enemies.Count;
                    foreach (var quest in qs.GetActiveQuests())
                        qs.ProgressQuest(quest.Definition.Id, "kill", killCount);
                }

                // Victory condition: defeating the final boss triggers the victory screen.
                if ((encounterData.Id ?? "") == "castle_boss")
                {
                    VictoryScreen.Show();
                    Debug.Log("[GameManager] The Rot King defeated — victory!");
                }
            }

            _activeZoneManager?.Deactivate();
            _activeZoneManager = null;

            // Clean up ModelAnimator from the player model (added during combat)
            // so it doesn't interfere with overworld movement
            var renderer3D = FindAnyObjectByType<Overworld.Overworld3DRenderer>();
            if (renderer3D != null && renderer3D.PlayerTransform != null)
            {
                var combatAnim = renderer3D.PlayerTransform.GetComponent<Battle.ModelAnimator>();
                if (combatAnim != null) Destroy(combatAnim);
            }

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
            SceneManager.LoadScene(UseProceduralWorld ? "World" : Use3DOverworld ? "Overworld3D" : "Overworld");
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

        /// <summary>Load a saved PlayerData and return to the overworld.</summary>
        public void LoadFromSave(PlayerData player)
        {
            Player = player;
            if (Character != null)
            {
                Character.HP = player.HP;
            }
            SceneManager.LoadScene(UseProceduralWorld ? "World" : Use3DOverworld ? "Overworld3D" : "Overworld");
        }

        public void GameComplete()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
