using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.RPG.Character;
using ForeverEngine.Bridges;
using ForeverEngine.Demo.Dungeon;
using ForeverEngine.Demo.UI;

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
        public bool IsInCombat { get; private set; }

        // ── Spec 7 Phase 3 Task 6: server-driven dungeon routing ──────────
        /// <summary>Seed for next dungeon scene to build (written by ConnectionManager on DungeonEnteredMessage).</summary>
        public static int PendingDungeonSeed;

        /// <summary>Template name for next dungeon scene (written by ConnectionManager on DungeonEnteredMessage).</summary>
        public static string PendingDungeonTemplate = "debug_small";

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
            // Spell panel — V-toggled, out-of-combat spell casting.
            gameObject.AddComponent<SpellPanel>();
            // Shop panel — opened by server ShopOpenMessage when talking to merchant NPCs.
            gameObject.AddComponent<ShopPanel>();
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
            SceneManager.LoadScene("World");
        }

        /// <summary>
        /// Called by CharacterCreationUI when the player confirms their character.
        /// Converts CharacterData to PlayerData, then loads the World scene.
        /// </summary>
        public void StartGameWithCharacter(CharacterData characterData, int seed = 0)
        {
            CharacterData = characterData;
            CurrentSeed   = seed > 0 ? seed : Random.Range(1, 99999);
            Player        = PlayerData.FromCharacterData(characterData);
            Player.DiscoveredLocations.Add("camp");
            SceneManager.LoadScene("World");
        }

        /// <summary>
        /// Called by the premade character selection buttons.
        /// Creates PlayerData from the CharacterSheet, then loads the World scene.
        /// </summary>
        public void StartGameWithSheet(CharacterSheet sheet, int seed = 0)
        {
            Character     = sheet;
            CurrentSeed   = seed > 0 ? seed : Random.Range(1, 99999);
            Player        = new PlayerData { HexQ = 2, HexR = 2 };
            Player.DiscoveredLocations.Add("camp");
            SyncPlayerFromCharacter();
            SceneManager.LoadScene("World");
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

        public void EnterBattle(string encounterId)
        {
            PendingEncounterId = encounterId;
            SceneManager.LoadScene("BattleMap");
        }

        /// <summary>
        /// Called by ConnectionManager when the server sends a BattleStart message.
        /// </summary>
        public void OnServerBattleStart()
        {
            IsInCombat = true;
        }

        /// <summary>
        /// Called by BattleRenderer when the server sends a BattleEnd message.
        /// Server handles rewards/loot; client just updates local state.
        /// </summary>
        public void OnServerBattleEnd(bool playerWon)
        {
            IsInCombat = false;
            LastBattleWon = playerWon;
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
            SceneManager.LoadScene("World");
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
            SceneManager.LoadScene("World");
        }

        public void GameComplete()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
