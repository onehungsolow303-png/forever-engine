using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.RPG.Character;
using ForeverEngine.Bridges;

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
        public bool LastBattleWon { get; set; }
        public int LastBattleGoldEarned { get; set; }
        public int LastBattleXPEarned { get; set; }

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

        public void EnterBattle(string encounterId)
        {
            PendingEncounterId = encounterId;
            SceneManager.LoadScene("BattleMap");
        }

        public void ReturnToOverworld()
        {
            PendingEncounterId = null;
            PendingLocationId = null;
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
