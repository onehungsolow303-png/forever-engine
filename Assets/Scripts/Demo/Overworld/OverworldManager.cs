using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    public class OverworldManager : UnityEngine.MonoBehaviour
    {
        public static OverworldManager Instance { get; private set; }

        public Dictionary<(int,int), HexTile> Tiles { get; private set; }
        public OverworldFog Fog { get; private set; }
        public OverworldPlayer Player { get; private set; }
        public float DayTime { get; private set; }
        public bool IsNight => DayTime > 0.75f || DayTime < 0.25f;
        public int EncountersSinceRest { get; set; }

        [SerializeField] private float _dayLengthSeconds = 600f; // 10 min

        private void Awake() => Instance = this;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[Overworld] No GameManager!"); return; }

            // Generate overworld
            Tiles = OverworldGenerator.Generate(20, 20, gm.CurrentSeed);

            // Setup fog
            Fog = new OverworldFog(IsNight ? 1 : 2);
            if (gm.Player.ExploredHexes.Count > 0)
                Fog.LoadExplored(gm.Player.ExploredHexes);

            // Setup player
            var playerGO = new GameObject("OverworldPlayer");
            Player = playerGO.AddComponent<OverworldPlayer>();
            Player.Initialize(gm.Player, Fog, Tiles, OnPlayerMoved);

            // Handle returning from battle
            if (gm.LastBattleWon)
            {
                gm.Player.Gold += gm.LastBattleGoldEarned;
                gm.LastBattleWon = false;
            }

            Debug.Log($"[Overworld] Initialized at ({gm.Player.HexQ},{gm.Player.HexR}), {Tiles.Count} tiles");
        }

        private void Update()
        {
            // Day/night
            DayTime = (DayTime + Time.deltaTime / _dayLengthSeconds) % 1f;
            Fog.SetRevealRadius(IsNight ? 1 : 2);

            // Input: hex movement (WASD mapped to hex directions)
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Player.TryMove(0, 1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Player.TryMove(0, -1);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Player.TryMove(-1, 0);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Player.TryMove(1, 0);
            else if (Input.GetKeyDown(KeyCode.Q)) Player.TryMove(-1, 1); // hex NW
            else if (Input.GetKeyDown(KeyCode.E)) Player.TryMove(1, -1); // hex SE
            else if (Input.GetKeyDown(KeyCode.F)) Player.Forage();
        }

        private void OnPlayerMoved(int q, int r)
        {
            // Check for location
            foreach (var loc in LocationData.GetAll())
            {
                if (loc.HexQ == q && loc.HexR == r)
                {
                    GameManager.Instance.Player.DiscoveredLocations.Add(loc.Id);
                    if (loc.IsSafe) GameManager.Instance.Player.LastSafeLocation = loc.Id;
                    Debug.Log($"[Overworld] Arrived at {loc.Name}");
                    return;
                }
            }

            // Random encounter check
            if (!Tiles.TryGetValue((q, r), out var tile)) return;
            float chance = tile.Type switch
            {
                TileType.Plains => 0.05f,
                TileType.Forest => 0.15f,
                TileType.Road => 0.25f, // Ruins
                _ => 0f
            };
            if (IsNight) chance += 0.15f;

            if (Random.Range(0f, 1f) < chance)
            {
                EncountersSinceRest++;
                GameManager.Instance.EnterBattle($"random_{tile.Type}_{(IsNight ? "night" : "day")}");
            }
        }
    }
}
