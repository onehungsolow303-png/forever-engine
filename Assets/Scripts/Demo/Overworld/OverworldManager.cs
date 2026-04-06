using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;
using ForeverEngine.Demo.UI;

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

        private OverworldRenderer _renderer;
        private DialogueOverlay   _dialogueOverlay;

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

            // Setup renderer
            var rendererGO = new GameObject("OverworldRenderer");
            _renderer = rendererGO.AddComponent<OverworldRenderer>();
            _renderer.Initialize(Tiles, Camera.main);

            // Setup player
            var playerGO = new GameObject("OverworldPlayer");
            Player = playerGO.AddComponent<OverworldPlayer>();
            Player.Initialize(gm.Player, Fog, Tiles, OnPlayerMoved);

            // Setup interior manager (adds itself as a sibling component on a new GO)
            if (Demo.Locations.LocationInteriorManager.Instance == null)
            {
                var interiorGO = new GameObject("LocationInteriorManager");
                interiorGO.AddComponent<Demo.Locations.LocationInteriorManager>();
            }

            // Setup dialogue overlay
            if (DialogueOverlay.Instance == null)
            {
                var overlayGO = new GameObject("DialogueOverlay");
                _dialogueOverlay = overlayGO.AddComponent<DialogueOverlay>();
            }
            else
            {
                _dialogueOverlay = DialogueOverlay.Instance;
            }

            // Handle returning from battle
            if (gm.LastBattleWon)
            {
                gm.Player.Gold += gm.LastBattleGoldEarned;
                // Partial heal after victory (short rest)
                int healAmount = gm.Player.MaxHP / 4;
                gm.Player.HP = System.Math.Min(gm.Player.HP + healAmount, gm.Player.MaxHP);
                gm.LastBattleWon = false;
            }

            // Initial visual update
            _renderer.UpdateVisuals(gm.Player.HexQ, gm.Player.HexR, Fog, IsNight);

            Debug.Log($"[Overworld] Initialized at ({gm.Player.HexQ},{gm.Player.HexR}), {Tiles.Count} tiles");
        }

        private void Update()
        {
            // Day/night and visuals always run
            DayTime = (DayTime + Time.deltaTime / _dayLengthSeconds) % 1f;
            Fog.SetRevealRadius(IsNight ? 1 : 2);
            if (_renderer != null && GameManager.Instance?.Player != null)
                _renderer.UpdateVisuals(GameManager.Instance.Player.HexQ, GameManager.Instance.Player.HexR, Fog, IsNight);

            // Block movement and location entry while dialogue overlay is open
            if (_dialogueOverlay != null && _dialogueOverlay.IsOpen) return;

            // Input: hex movement (WASD mapped to hex directions)
            if      (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    Player.TryMove(0, 1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  Player.TryMove(0, -1);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  Player.TryMove(-1, 0);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Player.TryMove(1, 0);
            else if (Input.GetKeyDown(KeyCode.Q)) Player.TryMove(-1, 1); // hex NW
            else if (Input.GetKeyDown(KeyCode.E)) Player.TryMove(1, -1); // hex SE
            else if (Input.GetKeyDown(KeyCode.F)) Player.Forage();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TryEnterLocation();
        }

        /// <summary>
        /// Called when the player presses Enter on a location hex.
        /// Safe locations (town, fortress, camp) open NPC dialogue first.
        /// Dungeon/castle locations hand off to LocationInteriorManager for combat entry.
        /// </summary>
        private void TryEnterLocation()
        {
            var p = GameManager.Instance?.Player;
            if (p == null) return;

            foreach (var loc in LocationData.GetAll())
            {
                if (loc.HexQ != p.HexQ || loc.HexR != p.HexR) continue;

                // Safe locations: show NPC dialogue overlay
                if (loc.IsSafe && _dialogueOverlay != null)
                {
                    _dialogueOverlay.Show(loc.Type);
                    return;
                }

                // Dungeon / castle: enter combat via interior manager
                var interior = Demo.Locations.LocationInteriorManager.Instance;
                if (interior != null)
                    interior.EnterLocation(loc);
                else
                    Debug.LogWarning("[Overworld] LocationInteriorManager not found.");
                return;
            }

            Debug.Log("[Overworld] No location at current position.");
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
                    Demo.AI.DemoAIIntegration.Instance?.OnLocationDiscovered(loc.Id);
                    Debug.Log($"[Overworld] Arrived at {loc.Name}");
                    return;
                }
            }

            // Random encounter check
            if (!Tiles.TryGetValue((q, r), out var tile)) return;

            // No encounters near safe locations (2 hex radius)
            foreach (var safeLoc in LocationData.GetAll())
            {
                if (safeLoc.IsSafe && Mathf.Abs(q - safeLoc.HexQ) + Mathf.Abs(r - safeLoc.HexR) <= 2)
                    return;
            }

            // No encounters in first 3 moves (tutorial grace period)
            if (Player.MovesTaken <= 3) return;

            float chance = tile.Type switch
            {
                TileType.Plains => 0.03f,
                TileType.Forest => 0.08f,
                TileType.Road => 0.12f, // Ruins
                _ => 0f
            };
            if (IsNight) chance += 0.08f;

            if (Random.Range(0f, 1f) < chance)
            {
                EncountersSinceRest++;
                GameManager.Instance.EnterBattle($"random_{tile.Type}_{(IsNight ? "night" : "day")}");
            }
        }
    }
}
