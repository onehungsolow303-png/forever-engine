using ForeverEngine.Core.Messages;
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
    public class ConnectionManager : MonoBehaviour
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

        // ── UI references ──────────────────────────────────────────────────
        private UIDocument _uiDocument;
        private Label _statusText;
        private Label _detailText;

        // ── Internal ───────────────────────────────────────────────────────
        private NetworkClient _client;

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

            // Begin connecting
            _client.Connect(Host, Port);
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
