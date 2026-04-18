using ForeverEngine.Core.Messages;
using ForeverEngine.Demo.Battle;
using ForeverEngine.Demo.UI;
using ForeverEngine.Procedural;
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
        public string Host = "127.0.0.1";
        public int Port = 7900;
        public string PlayerName = "Player";
        public string PlayerToken = "player_1";

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
                _moveSendCoroutine = StartCoroutine(MoveSendLoop());
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

        private void OnPlayerUpdate(PlayerUpdate msg)
        {
            ServerStateCache.Instance.ApplyPlayerUpdate(msg);

            // Spec 7 Phase 1: route each snapshot to local-pose or remote-cache.
            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            double now = UnityEngine.Time.timeAsDouble;
            string localId = LocalPlayerId;

            foreach (var p in msg.Players)
            {
                var pos = new UnityEngine.Vector3(p.X, p.Y, p.Z);
                if (!string.IsNullOrEmpty(localId) && p.Id == localId)
                {
                    cache.LocalPlayerPosition = pos;
                    cache.LocalPlayerYaw = p.Yaw;
                    ReconcileLocalPlayer(pos);
                }
                else
                {
                    cache.RemotePlayerPoses[p.Id] = (pos, p.Yaw, now);
                }
            }
        }

        private void ReconcileLocalPlayer(UnityEngine.Vector3 serverPos)
        {
            var player = _localPlayer;
            if (player == null) return;

            var clientPos = player.transform.position;
            float delta = UnityEngine.Vector3.Distance(clientPos, serverPos);

            if (delta < 1f)
                return; // Prediction is close enough — ignore.

            if (delta < 5f)
            {
                // Small divergence — lerp 25% per PlayerUpdate (~200ms at 20Hz).
                player.transform.position = UnityEngine.Vector3.Lerp(clientPos, serverPos, 0.25f);
                return;
            }

            // Hard snap — teleport, death, respawn, or egregious desync.
            player.transform.position = serverPos;
            var rb = player.GetComponent<UnityEngine.Rigidbody>();
            if (rb != null) rb.linearVelocity = UnityEngine.Vector3.zero;
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

        private void OnChunkData(ChunkDataMessage msg)
        {
            if (msg.Chunk == null) return;

            var localChunk = ChunkDataMapper.MapServerToLocal(msg.Chunk);
            var chunkMgr = ChunkManager.Instance;
            if (chunkMgr != null)
                chunkMgr.ReceiveServerChunk(localChunk);
            else
                Debug.LogWarning("[ConnectionManager] ChunkDataMessage received but ChunkManager not ready.");
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

        private void ShowOverlay(string status, string detail = "")
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
