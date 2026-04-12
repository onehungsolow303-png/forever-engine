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

        private OverworldRenderer _renderer;
        private Overworld3DRenderer _renderer3D;
        private ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController _camCtrl;

        /// <summary>True when a 3D renderer has been registered (by Overworld3DSetup).</summary>
        public bool Is3D => _renderer3D != null;

        /// <summary>
        /// Called by Overworld3DSetup to register the 3D renderer.
        /// When set, Update() routes visuals through the 3D renderer instead of the 2D one.
        /// </summary>
        public void Set3DRenderer(Overworld3DRenderer renderer)
        {
            _renderer3D = renderer;
            _camCtrl = FindAnyObjectByType<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
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

            // Suppress overworld input while a modal dialogue panel is open.
            // Without this, typing into the dialogue panel's TextField also
            // moved the player on the hex grid (W/A/S/D leak), and pressing
            // Enter to send a message also re-triggered TryEnterLocation.
            // The dialogue panel is the player's exclusive input target
            // until they Close it.
            if (UI.DialoguePanel.Instance != null && UI.DialoguePanel.Instance.IsOpen)
                return;

            // Block all input while the player model is animating between hexes
            if (_renderer3D != null && _renderer3D.IsMoving) return;

            // Movement input: check held keys so W+D etc. produces diagonal (strafe)
            float inputX = 0f, inputZ = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    inputZ += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  inputZ -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  inputX -= 1f;

            bool moved = false;
            if (inputX != 0f || inputZ != 0f)
                moved = TryCameraRelativeMove(inputX, inputZ);

            if (!moved)
            {
                if (Input.GetKeyDown(KeyCode.F)) Player.Forage();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TryEnterLocation();
            }

            // Trigger smooth animation for 3D renderer
            if (moved && _renderer3D != null)
                _renderer3D.StartMoveAnimation(Player.Q, Player.R);
        }

        // All 6 hex directions with their world-space XZ vectors (from HexToWorld3D formula)
        // Angles: 0°, 60°, 120°, 180°, 240°, 300° from +Z axis
        private static readonly (int dq, int dr, float x, float z)[] HEX_DIRS =
        {
            ( 0,  1,  0f,      1f),      // 0°   (+Z)
            ( 1,  0,  0.866f,  0.5f),    // 60°
            ( 1, -1,  0.866f, -0.5f),    // 120°
            ( 0, -1,  0f,     -1f),      // 180° (-Z)
            (-1,  0, -0.866f, -0.5f),    // 240°
            (-1,  1, -0.866f,  0.5f),    // 300°
        };

        /// <summary>
        /// Move the player in the hex direction closest to the camera-relative input.
        /// inputX/inputZ are in camera space: +Z = forward (into screen), +X = right.
        /// Uses the orbit angle (not camera transform) for a stable direction that
        /// doesn't jitter from the follow-damping each frame.
        /// </summary>
        private bool TryCameraRelativeMove(float inputX, float inputZ)
        {
            // Use cached camera controller's orbit angle for a stable direction
            // that doesn't jitter from follow-damping between moves
            if (_camCtrl == null)
                _camCtrl = FindAnyObjectByType<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();

            float fwdX, fwdZ, rightX, rightZ;
            if (_camCtrl != null)
            {
                float orbRad = _camCtrl.OrbitAngle * Mathf.Deg2Rad;
                // Camera looks opposite to its orbit offset
                fwdX = -Mathf.Sin(orbRad);
                fwdZ = -Mathf.Cos(orbRad);
                rightX = Mathf.Cos(orbRad);
                rightZ = -Mathf.Sin(orbRad);
            }
            else
            {
                var cam = Camera.main;
                if (cam == null)
                    return Player.TryMove(Mathf.RoundToInt(inputX), Mathf.RoundToInt(inputZ));
                Vector3 camFwd = cam.transform.forward;
                Vector3 camRight = cam.transform.right;
                camFwd.y = 0f; camFwd.Normalize();
                camRight.y = 0f; camRight.Normalize();
                fwdX = camFwd.x; fwdZ = camFwd.z;
                rightX = camRight.x; rightZ = camRight.z;
            }

            float desiredX = fwdX * inputZ + rightX * inputX;
            float desiredZ = fwdZ * inputZ + rightZ * inputX;

            // Find the hex direction with the highest dot product (closest match)
            float bestDot = -2f;
            int bestQ = 0, bestR = 0;
            foreach (var (dq, dr, hx, hz) in HEX_DIRS)
            {
                float dot = desiredX * hx + desiredZ * hz;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestQ = dq;
                    bestR = dr;
                }
            }

            return Player.TryMove(bestQ, bestR);
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

        private void OnPlayerMoved(int q, int r)
        {
            // Check for location
            foreach (var loc in LocationData.GetAll())
            {
                if (loc.HexQ == q && loc.HexR == r)
                {
                    GameManager.Instance.Player.DiscoveredLocations.Add(loc.Id);
                    if (loc.IsSafe) GameManager.Instance.Player.LastSafeLocation = loc.Id;
                    Demo.AI.DirectorEvents.Send($"discovered location: {loc.Id}");
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

            // No encounters in first 10 moves (tutorial grace period)
            if (Player.MovesTaken <= 10) return;

            float chance = tile.Type switch
            {
                TileType.Plains => 0.02f,
                TileType.Forest => 0.04f,
                TileType.Road => 0.06f, // Ruins
                _ => 0f
            };
            if (IsNight) chance += 0.04f;

            if (Random.Range(0f, 1f) < chance)
            {
                EncountersSinceRest++;
                GameManager.Instance.EnterBattle($"random_{tile.Type}_{(IsNight ? "night" : "day")}");
            }
        }
    }
}
