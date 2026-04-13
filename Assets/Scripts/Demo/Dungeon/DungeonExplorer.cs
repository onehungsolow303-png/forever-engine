using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.Camera;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Main controller for the dungeon exploration layer.
    /// Singleton — one instance lives on the DungeonExplorer GameObject
    /// created by DungeonSceneSetup.
    ///
    /// Responsibilities:
    ///   - Calls DungeonAssembler to build the room layout
    ///   - Spawns the player model (via ModelRegistry, with capsule fallback)
    ///   - Wires PerspectiveCameraController follow target
    ///   - Handles WASD movement (camera-relative, same pattern as OverworldManager)
    ///   - Fog of war: enables room lights when the player is nearby
    ///   - EnterBattle(): saves DungeonState, hands off to GameManager.EnterBattle
    ///   - OnBattleWon(): checks boss defeat, completes or continues dungeon
    /// </summary>
    public class DungeonExplorer : UnityEngine.MonoBehaviour
    {
        public static DungeonExplorer Instance { get; private set; }

        // ── Config ──────────────────────────────────────────────────────────
        private const float MoveSpeed      = 6f;
        private const float FogRevealRange = 10f;   // World units within which rooms light up
        private const int   DefaultRoomCount = 7;

        // ── State ───────────────────────────────────────────────────────────
        private string    _locationId;
        private RoomCatalog _catalog;
        private DungeonAssembler _assembler;
        private DungeonAssembler.RoomInstance[] _rooms;

        private Transform _playerTransform;
        private PerspectiveCameraController _camera;
        private Rigidbody _playerRb;

        private bool _initialized;

        // ── Unity lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_initialized || _playerTransform == null) return;
            HandleMovement();
            UpdateFogOfWar();
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Build the dungeon and set up the player + camera.
        /// Called by DungeonSceneSetup.Start().
        /// </summary>
        public void Initialize(string locationId, RoomCatalog catalog)
        {
            _locationId = locationId;
            _catalog    = catalog;

            var gm = GameManager.Instance;

            // Restore or create DungeonState
            DungeonState state = gm?.PendingDungeonState;
            if (state == null || state.LocationId != locationId)
                state = new DungeonState { LocationId = locationId };

            int seed = gm != null ? gm.CurrentSeed + locationId.GetHashCode() : 42;

            // Determine room count from GameConfig if available, else default
            int roomCount = DefaultRoomCount;
            var gc = Resources.Load<GameConfig>("GameConfig");
            // GameConfig doesn't expose a dungeon room count yet — use default.
            // When added, read it here: roomCount = gc.DungeonRoomCount;

            // Assemble dungeon
            var assemblerGO = new GameObject("DungeonAssembler");
            _assembler = assemblerGO.AddComponent<DungeonAssembler>();
            _rooms = _assembler.Assemble(catalog, roomCount, seed);

            state.RoomCount    = _rooms.Length;
            state.BossRoomIndex = _rooms.Length - 1;

            if (gm != null) gm.PendingDungeonState = state;

            // Spawn player
            SpawnPlayer(state);

            // Wire camera
            SetupCamera(state);

            _initialized = true;
            Debug.Log($"[DungeonExplorer] Initialized '{locationId}' — {_rooms.Length} rooms.");
        }

        /// <summary>
        /// Called by EncounterZone when the player walks into a combat trigger.
        /// Saves current dungeon state then loads the BattleMap scene.
        /// </summary>
        public void EnterBattle(string encounterId, int zoneIndex, bool isBoss)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Persist current player state into DungeonState
            SaveDungeonState(zoneIndex);

            Debug.Log($"[DungeonExplorer] Entering battle: {encounterId} (zone {zoneIndex}, boss={isBoss})");
            gm.PendingEncounterId = encounterId;
            SceneManager.LoadScene("BattleMap");
        }

        /// <summary>
        /// Called by DungeonSceneSetup after returning from a won battle.
        /// If the defeated encounter was the boss, complete the dungeon;
        /// otherwise continue exploration.
        /// </summary>
        public void OnBattleWon(string encounterId)
        {
            var gm = GameManager.Instance;
            var state = gm?.PendingDungeonState;
            if (state == null) return;

            // Check if the won battle was the boss encounter
            bool wasBoss = encounterId != null &&
                           encounterId.Contains($"room_{state.BossRoomIndex}");

            if (wasBoss)
            {
                state.BossDefeated = true;
                Debug.Log($"[DungeonExplorer] Boss defeated — dungeon '{_locationId}' cleared!");
                CompleteDungeon();
            }
            else
            {
                Debug.Log($"[DungeonExplorer] Battle won, continuing exploration.");
            }
        }

        // ── Private helpers ─────────────────────────────────────────────────

        private void SpawnPlayer(DungeonState state)
        {
            // Try to load the player model from ModelRegistry
            var playerGO = TrySpawnModelFromRegistry();

            if (playerGO == null)
            {
                // Capsule fallback
                playerGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerGO.name = "DungeonPlayer";
                playerGO.transform.localScale = Vector3.one;
            }

            // Position: restore from state if we've been here before, else entrance room
            if (state.VisitedRooms.Count > 0 && state.PlayerPosition != Vector3.zero)
                playerGO.transform.position = state.PlayerPosition;
            else if (_rooms != null && _rooms.Length > 0)
                playerGO.transform.position = _rooms[0].WorldPosition + Vector3.up * 1f;

            playerGO.transform.rotation = Quaternion.Euler(0, state.PlayerRotationY, 0);

            // Tag for EncounterZone trigger detection
            playerGO.tag = "Player";

            // Add physics components for trigger detection
            var rb = playerGO.GetComponent<Rigidbody>();
            if (rb == null) rb = playerGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
            _playerRb = rb;

            var col = playerGO.GetComponent<CapsuleCollider>();
            if (col == null) col = playerGO.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.4f;
            col.center = new Vector3(0, 0.9f, 0);

            _playerTransform = playerGO.transform;
        }

        private static GameObject TrySpawnModelFromRegistry()
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;

            // Derive model key from CharacterSheet if available
            string modelKey = null;
            if (gm.Character != null)
                modelKey = gm.Character.ModelId
                    ?? $"{gm.Character.Species?.Name?.Replace(" ", "")}_{(gm.Character.ClassLevels.Count > 0 ? gm.Character.ClassLevels[0].ClassRef?.Name : "Fighter")}";
            else if (gm.Player != null)
                modelKey = "Default_Player";

            if (string.IsNullOrEmpty(modelKey)) return null;

            var (path, scale) = Battle.ModelRegistry.Resolve(modelKey);
            if (string.IsNullOrEmpty(path)) return null;

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return null;

            var go = Instantiate(prefab);
            go.name = "DungeonPlayer";
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private void SetupCamera(DungeonState state)
        {
            _camera = FindFirstObjectByType<PerspectiveCameraController>();
            if (_camera == null)
            {
                // Create a camera with PerspectiveCameraController if none exists
                var camGO = new GameObject("DungeonCamera");
                var cam = camGO.AddComponent<UnityEngine.Camera>();
                cam.orthographic = false;
                cam.fieldOfView  = 45f;
                _camera = camGO.AddComponent<PerspectiveCameraController>();
                Debug.Log("[DungeonExplorer] Created PerspectiveCameraController.");
            }

            _camera.FollowTarget = _playerTransform;

            // Restore or apply defaults
            float orbitAngle = state.VisitedRooms.Count > 0 ? state.CameraOrbitAngle : 45f;
            float distance   = state.VisitedRooms.Count > 0 ? state.CameraDistance   : 12f;

            _camera.SetOrbitAngle(orbitAngle);
            _camera.SetDistance(distance);
            _camera.SnapToTarget();
        }

        private void HandleMovement()
        {
            float inputX = 0f, inputZ = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    inputZ += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  inputZ -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  inputX -= 1f;

            if (inputX == 0f && inputZ == 0f) return;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                Vector3 camFwd   = cam.transform.forward;
                Vector3 camRight = cam.transform.right;
                camFwd.y = 0f;  camFwd.Normalize();
                camRight.y = 0f; camRight.Normalize();

                Vector3 moveDir = (camFwd * inputZ + camRight * inputX).normalized;
                _playerRb.MovePosition(_playerTransform.position + moveDir * MoveSpeed * Time.deltaTime);

                if (moveDir.sqrMagnitude > 0.001f)
                    _playerTransform.rotation = Quaternion.LookRotation(moveDir);
            }
            else
            {
                _playerRb.MovePosition(_playerTransform.position +
                    new Vector3(inputX, 0, inputZ).normalized * MoveSpeed * Time.deltaTime);
            }
        }

        private void UpdateFogOfWar()
        {
            if (_rooms == null || _playerTransform == null) return;

            Vector3 playerPos = _playerTransform.position;
            var state = GameManager.Instance?.PendingDungeonState;

            foreach (var room in _rooms)
            {
                if (room.RoomLight == null) continue;

                float dist = Vector3.Distance(playerPos, room.WorldPosition);
                bool isCurrent = dist <= FogRevealRange;

                if (isCurrent)
                {
                    // Fully illuminate the current room
                    room.RoomLight.enabled   = true;
                    room.RoomLight.intensity = room.OriginalLightIntensity;

                    state?.VisitRoom(room.Index);
                }
                else if (state != null && state.HasVisited(room.Index))
                {
                    // Previously visited rooms are visible but dimmed to 50%
                    room.RoomLight.enabled   = true;
                    room.RoomLight.intensity = room.OriginalLightIntensity * 0.5f;
                }
                // Unvisited rooms remain disabled (fog of war)
            }
        }

        private void SaveDungeonState(int triggeredZone)
        {
            var gm = GameManager.Instance;
            var state = gm?.PendingDungeonState;
            if (state == null) return;

            state.PlayerPosition  = _playerTransform != null ? _playerTransform.position  : Vector3.zero;
            state.PlayerRotationY = _playerTransform != null ? _playerTransform.eulerAngles.y : 0f;
            state.CameraOrbitAngle = _camera != null ? _camera.OrbitAngle : 45f;
            state.CameraDistance   = _camera != null ? _camera.Distance   : 12f;
            state.TriggerEncounter(triggeredZone);
        }

        private void CompleteDungeon()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            Debug.Log($"[DungeonExplorer] Dungeon complete — returning to overworld.");
            gm.PendingDungeonState = null;
            gm.ReturnToOverworld();
        }
    }
}
