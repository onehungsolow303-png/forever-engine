using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.RPG.Character;

namespace ForeverEngine.Demo
{
    public class GameManager : UnityEngine.MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerData Player { get; private set; }
        public CharacterData CharacterData { get; private set; }
        public CharacterSheet Character { get; set; }
        public int CurrentSeed { get; private set; } = 42;
        public string PendingEncounterId { get; set; }
        public string PendingLocationId { get; set; }
        public string PendingMapDataPath { get; set; }
        public bool LastBattleWon { get; set; }
        public int LastBattleGoldEarned { get; set; }
        public int LastBattleXPEarned { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void NewGame(int seed = 42)
        {
            CurrentSeed = seed;
            Player = new PlayerData { HexQ = 2, HexR = 2 };
            Player.DiscoveredLocations.Add("camp");
            SceneManager.LoadScene("Overworld");
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
            SceneManager.LoadScene("Overworld");
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
            SceneManager.LoadScene("Overworld");
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

        public void EnterLocation(string locationId)
        {
            PendingLocationId = locationId;
            var loc = LocationData.Get(locationId);
            if (loc != null && (loc.Type == "town" || loc.Type == "camp" || loc.Type == "fortress"))
                return;
            SceneManager.LoadScene("BattleMap");
        }

        public void ReturnToOverworld()
        {
            PendingEncounterId = null;
            PendingLocationId = null;
            SceneManager.LoadScene("Overworld");
        }

        public void PlayerDied()
        {
            Player.HP = Player.MaxHP / 2;
            Player.Hunger = 50;
            Player.Thirst = 50;
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
