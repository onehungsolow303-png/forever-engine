using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;
using ForeverEngine.Network;
using ForeverEngine.Core.Messages;

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
        private Overworld3DRenderer _renderer3D;

        [SerializeField] private float _moveSpeed = 8f;

        /// <summary>True when a 3D renderer has been registered (by Overworld3DSetup).</summary>
        public bool Is3D => _renderer3D != null;

        /// <summary>
        /// Called by Overworld3DSetup to register the 3D renderer.
        /// When set, Update() routes visuals through the 3D renderer instead of the 2D one.
        /// </summary>
        public void Set3DRenderer(Overworld3DRenderer renderer)
        {
            _renderer3D = renderer;
        }

        // Phase 3 pivot: DialogueOverlay archived to _archive/forever-engine-pre-pivot/.
        // Will be reintroduced in a follow-up that uses DirectorClient for NPC dialogue.

        private void Awake() => Instance = this;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[Overworld] No GameManager!"); return; }

            // Generate overworld
            Tiles = OverworldGenerator.Generate(20, 20, gm.CurrentSeed);

            // Setup fog
            // Larger reveal radius for 3D top-down view (camera sees more of the map)
            bool is3D = FindAnyObjectByType<Overworld3DSetup>() != null;
            Fog = new OverworldFog(is3D ? 5 : (IsNight ? 1 : 2));
            if (gm.Player.ExploredHexes.Count > 0)
                Fog.LoadExplored(gm.Player.ExploredHexes);

            // Setup renderer -- 3D handled by Overworld3DSetup if present
            if (FindAnyObjectByType<Overworld3DSetup>() == null)
            {
                var rendererGO = new GameObject("OverworldRenderer");
                _renderer = rendererGO.AddComponent<OverworldRenderer>();
                _renderer.Initialize(Tiles, Camera.main);
            }

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

            // Phase 3 pivot: DialogueOverlay setup removed. The NPC dialogue
            // path will be replaced by Director Hub /dialogue calls in a
            // follow-up.

            // Handle returning from battle
            if (gm.LastBattleWon)
            {
                gm.Player.Gold += gm.LastBattleGoldEarned;
                // Full heal after victory
                gm.Player.HP = gm.Player.MaxHP;
                gm.LastBattleWon = false;
                SaveManager.Save();
            }

            // Initial visual update (3D renderer may not be set yet; Overworld3DSetup
            // runs after this Start(), so the first frame is handled by Update()).
            if (_renderer != null)
                _renderer.UpdateVisuals(gm.Player.HexQ, gm.Player.HexR, Fog, IsNight);

            // Register for server-authoritative position updates
            if (NetworkClient.Instance != null)
                NetworkClient.Instance.RegisterHandler<PlayerUpdate>(OnServerPlayerUpdate);

            Debug.Log($"[Overworld] Initialized at ({gm.Player.HexQ},{gm.Player.HexR}), {Tiles.Count} tiles");
        }

        private void Update()
        {
            if (Fog == null) return; // Not initialized (no GameManager)
            // Day/night and visuals always run
            DayTime = (DayTime + Time.deltaTime / _dayLengthSeconds) % 1f;
            Fog.SetRevealRadius(IsNight ? 1 : 2);
            if (GameManager.Instance?.Player != null)
            {
                int pq = GameManager.Instance.Player.HexQ;
                int pr = GameManager.Instance.Player.HexR;
                if (_renderer3D != null)
                    _renderer3D.UpdateVisuals(pq, pr, Fog, IsNight);
                else if (_renderer != null)
                    _renderer.UpdateVisuals(pq, pr, Fog, IsNight);
            }

            // Q/E rotation for player model in overworld
            if (_renderer3D != null && _renderer3D.PlayerTransform != null)
            {
                float rotSpeed = 120f;
                if (Input.GetKey(KeyCode.Q))
                    _renderer3D.PlayerTransform.Rotate(0f, -rotSpeed * Time.deltaTime, 0f);
                if (Input.GetKey(KeyCode.E))
                    _renderer3D.PlayerTransform.Rotate(0f, rotSpeed * Time.deltaTime, 0f);
            }

            // Suppress overworld input while a modal dialogue panel is open.
            // Without this, typing into the dialogue panel's TextField also
            // moved the player on the hex grid (W/A/S/D leak), and pressing
            // Enter to send a message also re-triggered TryEnterLocation.
            // The dialogue panel is the player's exclusive input target
            // until they Close it.
            if (UI.DialoguePanel.Instance != null && UI.DialoguePanel.Instance.IsOpen)
                return;

            if (GameManager.Instance?.IsInCombat == true) return;

            // Send movement input to server — server validates and responds with PlayerUpdate
            float inputX = 0f, inputZ = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    inputZ += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  inputZ -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  inputX -= 1f;

            if (inputX != 0f || inputZ != 0f)
            {
                // Transform WASD to camera-relative direction for the server
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camFwd = cam.transform.forward;
                    Vector3 camRight = cam.transform.right;
                    camFwd.y = 0f; camFwd.Normalize();
                    camRight.y = 0f; camRight.Normalize();

                    Vector3 moveDir = (camFwd * inputZ + camRight * inputX).normalized;

                    var client = NetworkClient.Instance;
                    if (client != null && client.State == ConnectionState.Connected)
                    {
                        client.Send(new MoveInput { Dx = moveDir.x, Dy = moveDir.z });
                    }
                    else
                    {
                        // Offline fallback: apply movement locally (Phase 2 cleanup)
                        Transform playerT = _renderer3D != null ? _renderer3D.PlayerTransform : null;
                        if (playerT != null)
                        {
                            playerT.position += moveDir * _moveSpeed * Time.deltaTime;
                            playerT.rotation = Quaternion.LookRotation(moveDir);

                            float hexSize = 4f;
                            var (newQ, newR) = WorldToHex(playerT.position, hexSize);
                            if (newQ != Player.Q || newR != Player.R)
                            {
                                if (Tiles.TryGetValue((newQ, newR), out var tile) && tile.Type != TileType.Water)
                                {
                                    Player.SetHex(newQ, newR, Fog);
                                    OnPlayerMoved(newQ, newR);
                                }
                                else
                                {
                                    Vector3 validPos = Overworld3DRenderer.HexToWorld3D(Player.Q, Player.R, 0, hexSize, 0f);
                                    playerT.position = new Vector3(
                                        Mathf.Lerp(playerT.position.x, validPos.x, 0.5f),
                                        playerT.position.y,
                                        Mathf.Lerp(playerT.position.z, validPos.z, 0.5f));
                                }
                            }
                        }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.F)) Player.Forage();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TryEnterLocation();
        }

        /// <summary>
        /// Convert a world XZ position to hex grid coordinates.
        /// Inverse of Overworld3DRenderer.HexToWorld3D.
        /// </summary>
        private static (int q, int r) WorldToHex(Vector3 pos, float hexSize)
        {
            float q = (2f / 3f * pos.x) / hexSize;
            float r = (-1f / 3f * pos.x + Mathf.Sqrt(3f) / 3f * pos.z) / hexSize;
            return (Mathf.RoundToInt(q), Mathf.RoundToInt(r));
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

                // Safe locations open the dialogue panel routed through
                // Director Hub. Created on demand so the panel doesn't have
                // to be in the scene at boot.
                if (loc.IsSafe)
                {
                    var panel = ForeverEngine.Demo.UI.DialoguePanel.Instance;
                    if (panel == null)
                    {
                        var dpGo = new GameObject("DialoguePanel");
                        panel = dpGo.AddComponent<ForeverEngine.Demo.UI.DialoguePanel>();
                    }
                    panel.Show(loc.Id, $"npc_{loc.Id}");
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

        /// <summary>
        /// Visual-only callback for local hex changes. Server handles game logic
        /// (encounters, location discovery, survival drain).
        /// </summary>
        private void OnPlayerMoved(int q, int r)
        {
            // Keep PlayerData in sync with the player's actual hex position so
            // that save/load always sees the current location.
            var gmp = GameManager.Instance?.Player;
            if (gmp != null) { gmp.HexQ = q; gmp.HexR = r; }

            if (Fog != null) Fog.Reveal(q, r);
        }

        /// <summary>
        /// Server-authoritative position update. Moves the player visual to the
        /// server-validated hex position.
        /// </summary>
        private void OnServerPlayerUpdate(PlayerUpdate update)
        {
            var cache = ServerStateCache.Instance;
            if (cache == null || Player == null) return;

            // Apply snapshot to cache
            cache.ApplyPlayerUpdate(update);

            var local = cache.GetLocalPlayer();
            if (local == null) return;

            int serverQ = (int)local.X;  // X maps to HexQ
            int serverR = (int)local.Y;  // Y maps to HexR

            if (Player.Q != serverQ || Player.R != serverR)
            {
                Player.SetHexFromServer(serverQ, serverR);
                OnPlayerMoved(serverQ, serverR);

                // Update the 3D player model transform to match server position
                Transform playerT = _renderer3D != null ? _renderer3D.PlayerTransform : null;
                if (playerT != null)
                {
                    float hexSize = 4f;
                    Vector3 worldPos = Overworld3DRenderer.HexToWorld3D(serverQ, serverR, 0, hexSize, 0f);
                    playerT.position = worldPos;
                }
            }
        }
    }
}
