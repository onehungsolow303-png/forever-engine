using System.Collections.Generic;
using ForeverEngine.Core.Messages;
using ForeverEngine.Demo.Battle;
using ForeverEngine.Demo.UI;
using ForeverEngine.Procedural;
using ForeverEngine.World.Voxel;
using UnityEngine;
using UnityEngine.UIElements;

namespace ForeverEngine.Network
{
    /// <summary>
    /// MonoBehaviour singleton (DontDestroyOnLoad) that owns the multiplayer
    /// connection lifecycle: connects to the game server, performs the login
    /// handshake, wires all server-state message handlers into ServerStateCache,
    /// and manages the connection-status overlay (ConnectionUI.uxml).
    ///
    /// Part of Spec 3B — multiplayer thin-client wiring.
    /// </summary>
    public class ConnectionManager : UnityEngine.MonoBehaviour
    {
        // ── Inspector fields ───────────────────────────────────────────────
        public string Host = "192.168.1.110";
        public int Port = 7900;
        public string PlayerName = "Player";
        // Resolved in Awake(); command-line --player <id> wins, otherwise a
        // process-unique id so two clients on one machine don't collide.
        public string PlayerToken;

        // ── Public state ───────────────────────────────────────────────────
        public static ConnectionManager Instance { get; private set; }
        public bool IsLoggedIn { get; private set; }
        public string PlayerId { get; private set; } = "";

        /// <summary>Spec 7 Phase 1: canonical local-player id for reconciliation.</summary>
        public string LocalPlayerId => PlayerId;

        // ── UI references ──────────────────────────────────────────────────
        private UIDocument _uiDocument;
        private Label _statusText;
        private Label _detailText;

        // ── Internal ───────────────────────────────────────────────────────
        private NetworkClient _client;

        // ── Spec 7 Phase 1: 20Hz MoveInput send loop ──────────────────────
        private UnityEngine.GameObject _localPlayer;
        private ForeverEngine.Procedural.SimplePlayerController _localPlayerController;
        private bool _diagHadIntent;
        private UnityEngine.Coroutine _moveSendCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Resolve player token, server host, and port from CLI.
            // --player <id> / --host <addr> / --port <n> override inspector values.
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.IsNullOrEmpty(PlayerToken) && (args[i] == "--player" || args[i] == "-player"))
                    PlayerToken = args[i + 1];
                else if (args[i] == "--host" || args[i] == "-host")
                    Host = args[i + 1];
                else if (args[i] == "--port" || args[i] == "-port")
                {
                    if (int.TryParse(args[i + 1], out int p)) Port = p;
                }
            }
            if (string.IsNullOrEmpty(PlayerToken))
                PlayerToken = $"player_{System.Diagnostics.Process.GetCurrentProcess().Id}";
            Debug.Log($"[ConnectionManager] PlayerToken={PlayerToken} Host={Host} Port={Port}");
        }

        private void Start()
        {
            // Create the shared state cache that all UI reads from
            ServerStateCache.CreateInstance();

            // Build the connection UI overlay
            InitConnectionUI();
            ShowOverlay("Connecting to server...", $"{Host}:{Port}");

            // Get or create NetworkClient on this GameObject
            _client = NetworkClient.Instance;
            if (_client == null)
                _client = gameObject.AddComponent<NetworkClient>();

            // Wire connection lifecycle events
            _client.OnConnected += HandleConnected;
            _client.OnDisconnected += HandleDisconnected;

            // Register server message handlers
            _client.RegisterHandler<LoginResponse>(OnLoginResponse);
            _client.RegisterHandler<PlayerUpdate>(OnPlayerUpdate);
            _client.RegisterHandler<StatUpdateMessage>(OnStatUpdate);
            _client.RegisterHandler<InventoryUpdateMessage>(OnInventoryUpdate);
            _client.RegisterHandler<QuestUpdateMessage>(OnQuestUpdate);
            _client.RegisterHandler<CharacterSheetDataMessage>(OnCharacterSheet);
            _client.RegisterHandler<NarrativeMessage>(OnNarrative);
            _client.RegisterHandler<BattleStartMessage>(OnBattleStart);
            _client.RegisterHandler<ShopOpenMessage>(OnShopOpen);
            _client.RegisterHandler<ShopUpdateMessage>(OnShopUpdate);
            _client.RegisterHandler<ChunkDataMessage>(OnChunkData);
            _client.RegisterHandler<PartyUpdateMessage>(OnPartyUpdate);
            _client.RegisterHandler<PartyInviteReceivedMessage>(OnPartyInviteReceived);
            _client.RegisterHandler<DungeonEnteredMessage>(OnDungeonEntered);
            _client.RegisterHandler<DungeonExitedMessage>(OnDungeonExited);
            _client.RegisterHandler<VoxelChunkSync>(OnVoxelChunkSync);
            _client.RegisterHandler<VoxelChunkUnsubscribe>(OnVoxelChunkUnsubscribe);
            _client.RegisterHandler<AtmosphericsUpdateMessage>(OnAtmosphericsUpdate);

            // Begin connecting
            _client.Connect(Host, Port);
        }

        // ── Spec 7 Phase 1: local player registration ─────────────────────

        /// <summary>
        /// Called by WorldBootstrap after the player is spawned. Starts the
        /// 20Hz MoveInput send loop. If called with null (e.g., battle scene
        /// swap), stops the loop.
        /// </summary>
        public void RegisterLocalPlayer(UnityEngine.GameObject player)
        {
            _localPlayer = player;
            _localPlayerController = player != null
                ? player.GetComponent<ForeverEngine.Procedural.SimplePlayerController>()
                : null;

            if (_moveSendCoroutine != null)
            {
                StopCoroutine(_moveSendCoroutine);
                _moveSendCoroutine = null;
            }
            if (_localPlayerController != null)
            {
                Debug.Log("[REGISTER-DIAG] SimplePlayerController found — MoveSendLoop starting.");
                _moveSendCoroutine = StartCoroutine(MoveSendLoop());
            }
            else
            {
                Debug.LogError("[REGISTER-DIAG] SimplePlayerController is NULL on registered player — server will never receive input.");
            }
        }

        public UnityEngine.GameObject LocalPlayer => _localPlayer;

        private System.Collections.IEnumerator MoveSendLoop()
        {
            var wait = new UnityEngine.WaitForSeconds(0.05f); // 20 Hz
            while (true)
            {
                yield return wait;
                if (_localPlayerController == null) continue;
                if (!IsLoggedIn) continue;

                var msg = new ForeverEngine.Core.Messages.MoveInput
                {
                    InputX = _localPlayerController.LastInputX,
                    InputZ = _localPlayerController.LastInputZ,
                    Yaw    = _localPlayerController.Yaw,
                    Sprint = _localPlayerController.LastSprint,
                    Jump   = _localPlayerController.JumpPressedThisFrame,
                };
                _client.Send(msg);

                // DIAG: log on intent-edge to confirm non-zero input reaches the wire.
                bool intentNow = UnityEngine.Mathf.Abs(msg.InputX) > 0.01f || UnityEngine.Mathf.Abs(msg.InputZ) > 0.01f;
                if (intentNow != _diagHadIntent)
                {
                    _diagHadIntent = intentNow;
                    Debug.Log($"[SEND-DIAG] MoveInput edge: x={msg.InputX} z={msg.InputZ} loggedIn={IsLoggedIn}");
                }

                // Edge-trigger reset — exactly one MoveInput per Space press carries Jump=true.
                _localPlayerController.JumpPressedThisFrame = false;
            }
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                _client.OnConnected -= HandleConnected;
                _client.OnDisconnected -= HandleDisconnected;
            }

            if (Instance == this)
                Instance = null;
        }

        // ── Connection lifecycle ───────────────────────────────────────────

        private void HandleConnected()
        {
            Debug.Log("[ConnectionManager] Connected — sending LoginRequest.");
            ShowOverlay("Logging in...", $"as {PlayerName}");

            _client.Send(new LoginRequest
            {
                PlayerName = PlayerName,
                Token = PlayerToken,
            });
        }

        private void HandleDisconnected()
        {
            if (!IsLoggedIn)
            {
                // Never completed login — don't enable reconnect loop
                ShowOverlay("Connection Lost", $"Could not reach {Host}:{Port}");
                return;
            }

            IsLoggedIn = false;
            ShowOverlay("Connection Lost — Reconnecting...", $"{Host}:{Port}");
            _client.EnableReconnect();
            Debug.Log("[ConnectionManager] Disconnected — reconnect enabled.");
        }

        // ── Message handlers ───────────────────────────────────────────────

        private void OnLoginResponse(LoginResponse msg)
        {
            if (!msg.Ok)
            {
                string reason = string.IsNullOrEmpty(msg.Reason) ? "unknown reason" : msg.Reason;
                Debug.LogWarning($"[ConnectionManager] Login failed: {reason}");
                ShowOverlay("Login Failed", reason);
                _client.DisableReconnect();
                return;
            }

            PlayerId = msg.PlayerId;
            ServerStateCache.Instance.LocalPlayerId = PlayerId;
            IsLoggedIn = true;

            Debug.Log($"[ConnectionManager] Logged in as player '{PlayerId}'.");
            HideOverlay();

            // Request an initial character sheet snapshot
            _client.Send(new RequestCharacterSheetMessage());
        }

        // Hard-desync threshold: above this horizontal delta between the
        // client's predicted position and the authoritative server pose we
        // teleport-and-settle. Normal localhost drift is sub-meter.
        private const float HardDesyncMeters = 100f;

        // Vertical rescue threshold: if the client falls more than this far
        // below the server's authoritative Y (ground + capsule half-height),
        // assume we've tunneled through un-cooked terrain and snap back up.
        private const float VerticalRescueMeters = 50f;

        // Name of the GaiaRuntimeBridge GameObject; mirrors GaiaRuntimeBridge.GameObjectName.
        // Assembly-CSharp can't take an asmdef ref on the Gaia-isolated bridge, so we
        // dispatch via Find + SendMessage instead of a typed call. The bridge self-spawns
        // via [RuntimeInitializeOnLoadMethod] so it's reliably present after scene load.
        private const string BridgeGameObjectName = "GaiaRuntimeBridge";

        // Director Hub-emitted atmospherics → apply via Unity SendMessage to the
        // bridge GameObject. The bridge's Apply(Dictionary<string,object>) signature
        // handles partial updates + missing-field-skip; we just construct the dict
        // verbatim from the wire message. Server only broadcasts on change so this
        // fires sparingly.
        private void OnAtmosphericsUpdate(AtmosphericsUpdateMessage msg)
        {
            var bridgeGO = GameObject.Find(BridgeGameObjectName);
            if (bridgeGO == null)
            {
                Debug.LogWarning($"[ConnectionManager] AtmosphericsUpdate received but no '{BridgeGameObjectName}' in scene; ignoring");
                return;
            }

            var dict = new Dictionary<string, object>(11);
            if (msg.SunPitchDeg.HasValue)       dict["sun_pitch_deg"]       = msg.SunPitchDeg.Value;
            if (msg.SunRotationDeg.HasValue)    dict["sun_rotation_deg"]    = msg.SunRotationDeg.Value;
            if (msg.SunIntensity.HasValue)      dict["sun_intensity"]       = msg.SunIntensity.Value;
            if (msg.SunColorKelvin.HasValue)    dict["sun_color_kelvin"]    = msg.SunColorKelvin.Value;
            if (msg.WindSpeed.HasValue)         dict["wind_speed"]          = msg.WindSpeed.Value;
            if (msg.WindDirectionNorm.HasValue) dict["wind_direction_norm"] = msg.WindDirectionNorm.Value;
            if (msg.SkyboxExposure.HasValue)    dict["skybox_exposure"]     = msg.SkyboxExposure.Value;
            if (msg.SkyboxTintR.HasValue && msg.SkyboxTintG.HasValue && msg.SkyboxTintB.HasValue)
            {
                dict["skybox_tint_rgb"] = new List<object>
                {
                    msg.SkyboxTintR.Value, msg.SkyboxTintG.Value, msg.SkyboxTintB.Value,
                };
            }
            dict["transition_seconds"] = msg.TransitionSeconds;

            bridgeGO.SendMessage("Apply", dict, SendMessageOptions.DontRequireReceiver);
        }

        private void OnPlayerUpdate(PlayerUpdate msg)
        {
            ServerStateCache.Instance.ApplyPlayerUpdate(msg);

            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            double clientNow = UnityEngine.Time.timeAsDouble;
            string localId = LocalPlayerId;

            // Shared server-time estimate drives every per-player SnapshotBuffer.
            cache.Clock.Observe(msg.ServerTick, clientNow);

            foreach (var p in msg.Players)
            {
                var pos = new UnityEngine.Vector3(p.X, p.Y, p.Z);

                if (!cache.PlayerSnapshots.TryGetValue(p.Id, out var buf))
                {
                    buf = new ForeverEngine.Core.Network.SnapshotBuffer(tickRateHz: 20.0);
                    cache.PlayerSnapshots[p.Id] = buf;
                }
                buf.Push(new ForeverEngine.Core.Network.PoseSample(
                    msg.ServerTick, p.X, p.Y, p.Z, p.Yaw));
                cache.PlayerLastArrivalClientTime[p.Id] = clientNow;

                if (!string.IsNullOrEmpty(localId) && p.Id == localId)
                {
                    cache.LocalPlayerPosition = pos;
                    cache.LocalPlayerYaw = p.Yaw;
                    HardDesyncRecover(pos);
                }
            }
        }

        /// <summary>
        /// Only runs when the client has drifted dramatically from the
        /// authoritative pose — cheat detection catch-up, respawn, teleport.
        /// No per-packet transform writes: reconciliation is handled by the
        /// render-time SnapshotBuffer interpolation for remotes, and by trust
        /// in client-side prediction for the local player.
        /// </summary>
        private void HardDesyncRecover(UnityEngine.Vector3 serverPos)
        {
            var player = _localPlayer;
            if (player == null) return;

            var clientPos = player.transform.position;
            float horizDelta = UnityEngine.Vector2.Distance(
                new UnityEngine.Vector2(clientPos.x, clientPos.z),
                new UnityEngine.Vector2(serverPos.x, serverPos.z));
            float vertFallDelta = serverPos.y - clientPos.y; // >0 when client is below server

            bool horizDesync = horizDelta >= HardDesyncMeters;
            bool verticalRescue = vertFallDelta >= VerticalRescueMeters;

            if (!horizDesync && !verticalRescue) return;

            // Build the snap target axis-by-axis. Horizontal desync implies
            // the server has moved us (teleport/respawn) — snap X/Z. Vertical
            // rescue implies we tunneled below un-cooked terrain — snap Y.
            // Both can be true simultaneously (falling through while also
            // having drifted off from server input): apply both without one
            // branch hiding the other.
            float snapX = horizDesync ? serverPos.x : clientPos.x;
            float snapZ = horizDesync ? serverPos.z : clientPos.z;
            float snapY = verticalRescue ? serverPos.y : clientPos.y;

            string reason = horizDesync && verticalRescue
                ? $"Full resync: {horizDelta:F1}m horiz + {vertFallDelta:F1}m vertical"
                : horizDesync
                    ? $"Hard desync recover: {horizDelta:F1}m horizontal drift"
                    : $"Vertical rescue: fell {vertFallDelta:F1}m below server Y";
            Debug.LogWarning($"[ConnectionManager] {reason}, snapping to server pose.");

            player.transform.position = new UnityEngine.Vector3(snapX, snapY, snapZ);
            var rb = player.GetComponent<UnityEngine.Rigidbody>();
            if (rb != null)
                rb.linearVelocity = UnityEngine.Vector3.zero;
        }

        private void OnStatUpdate(StatUpdateMessage msg)
        {
            ServerStateCache.Instance.ApplyStatUpdate(msg);
        }

        private void OnInventoryUpdate(InventoryUpdateMessage msg)
        {
            ServerStateCache.Instance.ApplyInventoryUpdate(msg);
        }

        private void OnQuestUpdate(QuestUpdateMessage msg)
        {
            ServerStateCache.Instance.ApplyQuestUpdate(msg);
        }

        private void OnCharacterSheet(CharacterSheetDataMessage msg)
        {
            ServerStateCache.Instance.ApplyCharacterSheet(msg);
        }

        private void OnNarrative(NarrativeMessage msg)
        {
            // DialoguePanel.IsOpen is an instance property — check via the singleton.
            // If the panel is open, DialoguePanel handles its own narrative display;
            // we just log when it's closed so nothing is silently swallowed.
            bool panelOpen = Demo.UI.DialoguePanel.Instance != null
                             && Demo.UI.DialoguePanel.Instance.IsOpen;

            if (!panelOpen)
            {
                string speaker = string.IsNullOrEmpty(msg.Speaker) ? "Narrator" : msg.Speaker;
                Debug.Log($"[ConnectionManager] Narrative from '{speaker}': {msg.Text}");
            }
        }

        private void OnBattleStart(BattleStartMessage msg)
        {
            // Create BattleRenderer if not exists
            if (BattleRenderer.Instance == null)
            {
                var go = new GameObject("BattleRenderer");
                var br = go.AddComponent<BattleRenderer>();
                br.RegisterHandlers();
            }
            // Forward the first message
            BattleRenderer.Instance.HandleBattleStart(msg);

            if (Demo.GameManager.Instance != null)
                Demo.GameManager.Instance.OnServerBattleStart();
        }

        private void OnShopOpen(ShopOpenMessage msg)
        {
            if (ShopPanel.Instance == null)
                gameObject.AddComponent<ShopPanel>();
            ShopPanel.Instance.Open(msg.NpcId, msg.ShopName, msg.Items, msg.PlayerGold);
        }

        private void OnShopUpdate(ShopUpdateMessage msg)
        {
            ShopPanel.Instance?.UpdateShop(msg.Items, msg.PlayerGold);
        }

        // Chunks streamed before the World scene has a ChunkManager are buffered
        // here and flushed once the ChunkManager singleton comes online.
        private readonly System.Collections.Generic.List<ForeverEngine.Procedural.ChunkData> _pendingChunks =
            new System.Collections.Generic.List<ForeverEngine.Procedural.ChunkData>();

        private void OnChunkData(ChunkDataMessage msg)
        {
            if (msg.Chunk == null) return;

            var localChunk = ChunkDataMapper.MapServerToLocal(msg.Chunk);
            var chunkMgr = ChunkManager.Instance;
            if (chunkMgr != null)
            {
                chunkMgr.ReceiveServerChunk(localChunk);
            }
            else
            {
                _pendingChunks.Add(localChunk);
            }
        }

        private void Update()
        {
            if (_pendingChunks.Count == 0) return;
            var chunkMgr = ChunkManager.Instance;
            if (chunkMgr == null) return;

            for (int i = 0; i < _pendingChunks.Count; i++)
                chunkMgr.ReceiveServerChunk(_pendingChunks[i]);
            Debug.Log($"[ConnectionManager] Flushed {_pendingChunks.Count} buffered chunk(s) to ChunkManager.");
            _pendingChunks.Clear();
        }

        // ── Spec 7 Phase 3 Task 6: party + dungeon handlers ───────────────

        private void OnPartyUpdate(PartyUpdateMessage msg)
        {
            ServerStateCache.Instance.CurrentParty = msg.Party;
            Debug.Log($"[Party] Update: {msg.Party.MemberIds.Length} members, leader={msg.Party.LeaderId}");
        }

        private void OnPartyInviteReceived(PartyInviteReceivedMessage msg)
        {
            ServerStateCache.Instance.PendingInvite = (msg.FromPlayerId, msg.PartyId);
            Debug.Log($"[Party] Invite from {msg.FromPlayerId}");
        }

        private void OnDungeonEntered(DungeonEnteredMessage msg)
        {
            var cache = ServerStateCache.Instance;
            cache.CurrentDungeonInstanceId = msg.InstanceId;
            // Pass seed + template to GameManager so DungeonSceneSetup picks them up after scene load.
            Demo.GameManager.PendingDungeonSeed = msg.Seed;
            Demo.GameManager.PendingDungeonTemplate = msg.TemplateName;
            Debug.Log($"[Dungeon] Entered {msg.InstanceId} seed={msg.Seed} template={msg.TemplateName}");
            UnityEngine.SceneManagement.SceneManager.LoadScene("DungeonExploration");
        }

        private void OnDungeonExited(DungeonExitedMessage msg)
        {
            ServerStateCache.Instance.CurrentDungeonInstanceId = "";
            Debug.Log($"[Dungeon] Exited {msg.InstanceId}");
            UnityEngine.SceneManagement.SceneManager.LoadScene("World");
        }

        // Cache the VoxelWorldManager so FindFirstObjectByType isn't called on every chunk.
        private VoxelWorldManager _voxelWorldManager;

        private VoxelWorldManager GetVoxelWorldManager()
        {
            if (_voxelWorldManager == null)
            {
                _voxelWorldManager = UnityEngine.Object.FindFirstObjectByType<VoxelWorldManager>();
                if (_voxelWorldManager == null)
                {
                    // Auto-bootstrap a scene-root VoxelWorldManager if none was placed.
                    // Chunk GameObjects are positioned in world-space so the manager's
                    // own transform just needs to exist at origin.
                    var go = new GameObject("VoxelWorld");
                    _voxelWorldManager = go.AddComponent<VoxelWorldManager>();
                    UnityEngine.Debug.Log("[ConnectionManager] Auto-bootstrapped VoxelWorldManager; no scene-placed instance found.");
                }
            }
            return _voxelWorldManager;
        }

        private void OnVoxelChunkSync(VoxelChunkSync msg)
        {
            var vwm = GetVoxelWorldManager();
            if (vwm != null) vwm.Streamer.OnSync(msg);
        }

        private void OnVoxelChunkUnsubscribe(VoxelChunkUnsubscribe msg)
        {
            var vwm = GetVoxelWorldManager();
            if (vwm != null) vwm.Streamer.OnUnsubscribe(msg);
        }

        /// <summary>
        /// Public send wrapper for UI panels and other MonoBehaviours.
        /// No-op if the client is not connected.
        /// </summary>
        public void Send(ForeverEngine.Core.Messages.ClientMessage msg)
        {
            if (_client != null) _client.Send(msg);
        }

        // ── Connection UI ──────────────────────────────────────────────────

        private void InitConnectionUI()
        {
            _uiDocument = gameObject.GetComponent<UIDocument>();
            if (_uiDocument == null)
                _uiDocument = gameObject.AddComponent<UIDocument>();

            // Reuse the DialoguePanelSettings or create a sensible default
            var settings = Resources.Load<PanelSettings>("DialoguePanelSettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "ConnectionPanelSettings (auto)";
                settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
                settings.referenceResolution = new Vector2Int(1920, 1080);
                settings.sortingOrder = 200; // above everything else
            }
            _uiDocument.panelSettings = settings;

            var asset = Resources.Load<VisualTreeAsset>("ConnectionUI");
            if (asset != null)
            {
                _uiDocument.visualTreeAsset = asset;
            }
            else
            {
                Debug.LogWarning("[ConnectionManager] ConnectionUI.uxml not found in Resources/.");
            }

            // Grab label references after the visual tree is set
            var root = _uiDocument.rootVisualElement;
            if (root != null)
            {
                _statusText = root.Q<Label>("status-text");
                _detailText = root.Q<Label>("detail-text");
                root.style.display = DisplayStyle.None;
            }
        }

        public void ShowOverlay(string status, string detail = "")
        {
            var root = _uiDocument?.rootVisualElement;
            if (root == null) return;

            // Labels may not have resolved yet on the first call — retry
            if (_statusText == null) _statusText = root.Q<Label>("status-text");
            if (_detailText == null) _detailText = root.Q<Label>("detail-text");

            if (_statusText != null) _statusText.text = status;
            if (_detailText != null) _detailText.text = detail;

            root.style.display = DisplayStyle.Flex;
        }

        private void HideOverlay()
        {
            var root = _uiDocument?.rootVisualElement;
            if (root != null)
                root.style.display = DisplayStyle.None;
        }
    }
}
