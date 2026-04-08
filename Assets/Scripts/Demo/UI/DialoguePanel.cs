using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.Bridges;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// In-game dialogue overlay. Singleton MonoBehaviour wrapping a UIDocument
    /// loaded from Assets/UI/DialoguePanel.uxml. Replaces the archived
    /// 588-line DialogueOverlay with a minimal viable surface that routes
    /// player text through Director Hub via DirectorEvents.SendDialogue.
    ///
    /// Spec: C:/Dev/.shared/docs/superpowers/specs/2026-04-06-dialogue-ui-restoration-design.md
    /// </summary>
    public class DialoguePanel : UnityEngine.MonoBehaviour
    {
        public static DialoguePanel Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private const string PanelAssetPath = "DialoguePanel"; // Resources path or Addressables key
        private const int MaxHistoryLines = 100;

        private UIDocument _document;
        private VisualElement _root;
        private Label _npcLabel;
        private Label _offlineBanner;
        private ScrollView _history;
        private TextField _input;
        private Button _sendButton;
        private Button _closeButton;
        private Button _micButton;
        private Button _muteButton;
        private string _currentLocationId;
        private string _currentNpcId;
        private readonly List<string> _historyLines = new();
        private bool _waitingForResponse;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Ensure VoiceInput + VoiceOutput singletons exist on the same
            // GameObject. They self-register via their own Awake. Created
            // lazily so projects that don't need voice features pay no cost.
            if (VoiceInput.Instance == null) gameObject.AddComponent<VoiceInput>();
            if (VoiceOutput.Instance == null) gameObject.AddComponent<VoiceOutput>();

            _document = gameObject.GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();

            // UIDocument needs PanelSettings BEFORE it can render visuals to
            // screen. Without it the visual tree may instantiate but no
            // rendered panel exists, so the player sees nothing. Try the
            // Resources cache first; fall back to a programmatic instance
            // with sensible defaults if no asset has been authored.
            var settings = Resources.Load<PanelSettings>("DialoguePanelSettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "DialoguePanelSettings (auto)";
                settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
                settings.referenceResolution = new Vector2Int(1920, 1080);
                settings.sortingOrder = 100; // above world geometry
            }
            _document.panelSettings = settings;

            // Load the UXML asset. Must live under a Resources/ folder so
            // Resources.Load can find it in player builds (the asset was
            // moved from Assets/UI/ to Assets/Resources/ in commit 63032d4).
            var asset = Resources.Load<VisualTreeAsset>(PanelAssetPath);
            if (asset != null)
            {
                _document.visualTreeAsset = asset;
            }

            // Try to grab the root immediately. If UIDocument is not yet
            // initialized this will be null; EnsureRootInitialized() in
            // Show() retries on demand so callers don't have to think about
            // the timing.
            _root = _document.rootVisualElement;
            if (_root != null)
            {
                _root.style.SetVisible(false);
                HookUpReferences();
            }
        }

        /// <summary>
        /// Lazy fallback: if Awake couldn't grab the root because UIDocument
        /// hadn't instantiated the visual tree yet, retry on the next call
        /// to Show. Idempotent — does nothing once _root is set.
        /// </summary>
        private void EnsureRootInitialized()
        {
            if (_root != null) return;
            if (_document == null) return;
            _root = _document.rootVisualElement;
            if (_root != null)
            {
                _root.style.SetVisible(false);
                HookUpReferences();
            }
        }

        public void Show(string locationId, string npcId)
        {
            // If Awake couldn't initialize the root (UIDocument timing race),
            // retry now. Without this, Show() runs against null fields and
            // the panel appears as a 1-pixel-tall element with no content,
            // which is what was happening when the player pressed Enter at
            // safe locations and "nothing happened."
            EnsureRootInitialized();

            _currentLocationId = locationId;
            _currentNpcId = npcId ?? $"npc_{locationId}";
            _historyLines.Clear();
            RefreshHistory();

            if (_npcLabel != null)
            {
                // Prefer the named NPC from NPCData when one exists for this
                // location, fall back to the raw npcId, fall back to a placeholder.
                string display = "Speaking with...";
                var npc = NPCData.GetForLocation(locationId);
                if (npc != null)
                {
                    display = string.IsNullOrEmpty(npc.Role) ? npc.Name : $"{npc.Name} — {npc.Role}";
                }
                else if (!string.IsNullOrEmpty(npcId))
                {
                    display = npcId;
                }
                _npcLabel.text = display;
            }

            // Banner reflects watchdog state if available
            if (_offlineBanner != null)
            {
                bool offline = false;
                var watchdog = GameManager.Instance?.Watchdog;
                if (watchdog != null && !watchdog.AllOk) offline = true;
                _offlineBanner.style.display = offline ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _root?.style.SetVisible(true);
            IsOpen = true;
            _input?.Focus();
        }

        public void Close()
        {
            _root?.style.SetVisible(false);
            IsOpen = false;
            // Release the mic + stop any in-flight TTS so we don't keep
            // talking after the player has dismissed the conversation.
            if (VoiceInput.Instance != null && VoiceInput.Instance.IsListening)
                VoiceInput.Instance.StopListening();
            if (VoiceOutput.Instance != null)
                VoiceOutput.Instance.StopSpeaking();
            UpdateMicButtonAppearance();
        }

        private void HookUpReferences()
        {
            if (_root == null) return;
            _npcLabel = _root.Q<Label>("npc-name");
            _offlineBanner = _root.Q<Label>("offline-banner");
            _history = _root.Q<ScrollView>("history");
            _input = _root.Q<TextField>("input");
            _sendButton = _root.Q<Button>("send");
            _closeButton = _root.Q<Button>("close");
            _micButton = _root.Q<Button>("mic");
            _muteButton = _root.Q<Button>("mute");

            if (_sendButton != null) _sendButton.clicked += OnSendClicked;
            if (_closeButton != null) _closeButton.clicked += Close;
            if (_micButton != null) _micButton.clicked += OnMicClicked;
            if (_muteButton != null) _muteButton.clicked += OnMuteClicked;

            // Subscribe to VoiceInput results so spoken text lands in the
            // input field as the user dictates. Final results auto-trigger
            // Send so the user doesn't have to click after speaking.
            if (VoiceInput.Instance != null)
            {
                VoiceInput.Instance.OnPartialResult += OnVoicePartial;
                VoiceInput.Instance.OnFinalResult += OnVoiceFinal;
                VoiceInput.Instance.OnError += OnVoiceError;
                // Refresh the mic visual whenever the listening state flips,
                // including auto-stops from silence timeout / cancel / error.
                // Without this the button stays visually red after dictation
                // ends and the player thinks the panel is frozen.
                VoiceInput.Instance.OnStateChanged += UpdateMicButtonAppearance;
            }
            if (_input != null)
            {
                _input.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return && !evt.shiftKey) OnSendClicked();
                });

                // TextField is a COMPOSITE control. Inline styles on the
                // outer <ui:TextField> element only affect the wrapper, not
                // the inner <TextInput> child that actually renders the
                // text and background. The Unity default theme paints the
                // inner element with light gray bg + dim foreground which
                // is unreadable on our dark dialogue background — the
                // player reported "I couldn't see what I was typing."
                //
                // Defer the inner-element lookup to the next layout pass
                // via schedule.Execute, because composite controls create
                // their inner structure during the first layout, not in
                // the constructor. Re-apply on every subsequent geometry
                // change to be defensive against theme reloads.
                _input.schedule.Execute(StyleInputInner).ExecuteLater(0);
                _input.RegisterCallback<GeometryChangedEvent>(_ => StyleInputInner());
            }
        }

        /// <summary>
        /// Walks into the TextField's inner TextElement and overrides the
        /// theme-default colors so the player's typed input is readable on
        /// the dialogue panel's dark background.
        /// </summary>
        private void StyleInputInner()
        {
            if (_input == null) return;
            // Try the modern Unity 6 USS class first, fall back to the older
            // class name, fall back to any TextElement child.
            VisualElement inner = _input.Q(className: "unity-base-text-field__input")
                                ?? _input.Q(className: "unity-text-field__input")
                                ?? _input.Q<TextElement>();
            if (inner == null) return;

            inner.style.color = new Color(0.97f, 0.97f, 0.97f);
            inner.style.backgroundColor = new Color(0.16f, 0.16f, 0.20f, 0.95f);
            inner.style.fontSize = 15;
            inner.style.paddingLeft = 8;
            inner.style.paddingRight = 8;
            inner.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        private void OnSendClicked()
        {
            if (_input == null || _waitingForResponse) return;
            string text = _input.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) return;

            AppendLine($"You: {text}");
            _input.value = "";
            _waitingForResponse = true;

            // Snapshot the conversation history (last 12 turns) BEFORE
            // appending the current input. The Director Hub LLM uses this
            // to track mood escalation across the conversation — without
            // it, every turn looks like the first joke and the NPC never
            // gets fed up. The cap of 12 keeps token cost bounded.
            const int historyTurnsToSend = 12;
            int historyStart = System.Math.Max(0, _historyLines.Count - historyTurnsToSend);
            int historyCount = _historyLines.Count - historyStart;
            string[] recentHistory = new string[historyCount];
            for (int i = 0; i < historyCount; i++)
                recentHistory[i] = _historyLines[historyStart + i];

            Demo.AI.DirectorEvents.SendDialogue(
                text,
                _currentNpcId,
                narrative =>
                {
                    _waitingForResponse = false;
                    if (string.IsNullOrEmpty(narrative))
                    {
                        AppendLine("(The conversation falters as you struggle to find the right words.)");
                    }
                    else
                    {
                        // Use the NPC's display name if we have a persona for this location;
                        // otherwise fall back to the bare ID so older locations still render.
                        string speaker = _currentNpcId;
                        var npc = NPCData.GetForLocation(_currentLocationId);
                        if (npc != null) speaker = npc.Name;
                        AppendLine($"{speaker}: {narrative}");

                        // Auto-narrate the NPC's response via TTS. Skipped
                        // when the user has muted via the speaker button.
                        // VoiceOutput strips *action* descriptions internally
                        // so only the spoken dialogue gets vocalized.
                        if (VoiceOutput.Instance != null)
                            VoiceOutput.Instance.Speak(narrative);
                    }
                },
                locationId: _currentLocationId,
                recentHistory: recentHistory);
        }

        // ── Voice button handlers ─────────────────────────────────────────

        private void OnMicClicked()
        {
            if (VoiceInput.Instance == null || !VoiceInput.Instance.IsAvailable)
            {
                Debug.LogWarning("[DialoguePanel] voice input unavailable on this platform");
                return;
            }
            VoiceInput.Instance.Toggle();
            UpdateMicButtonAppearance();
        }

        private void OnMuteClicked()
        {
            if (VoiceOutput.Instance == null) return;
            VoiceOutput.Instance.ToggleMute();
            if (_muteButton != null)
                _muteButton.text = VoiceOutput.Instance.MuteEnabled ? "🔇" : "🔊";
        }

        private void UpdateMicButtonAppearance()
        {
            if (_micButton == null || VoiceInput.Instance == null) return;
            // Highlight the mic button while listening so the player knows
            // their speech is being captured.
            if (VoiceInput.Instance.IsListening)
            {
                _micButton.text = "🔴";
                _micButton.style.backgroundColor = new Color(0.7f, 0.2f, 0.2f, 0.95f);
            }
            else
            {
                _micButton.text = "🎤";
                _micButton.style.backgroundColor = new Color(0.24f, 0.27f, 0.43f, 0.95f);
            }
        }

        private void OnVoicePartial(string text)
        {
            // Stream hypothesis into the input field as the user speaks
            if (_input != null) _input.value = text;
        }

        private void OnVoiceFinal(string text)
        {
            // Final commit: place the recognized text in the input. We do
            // NOT auto-send because dictation often picks up filler words
            // or mishears, so the player gets a chance to edit before
            // pressing Send.
            if (_input != null) _input.value = text;
            UpdateMicButtonAppearance();
        }

        private void OnVoiceError(string message)
        {
            Debug.LogWarning($"[DialoguePanel] voice input error: {message}");
            UpdateMicButtonAppearance();

            // Disable the mic button visually so the player knows it's not
            // available, and surface the error to the offline-banner row
            // (re-purposed as a "voice unavailable" indicator).
            if (_micButton != null)
            {
                _micButton.text = "🚫";
                _micButton.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.95f);
                _micButton.SetEnabled(false);
                _micButton.tooltip = $"Voice input unavailable: {message}";
            }
            if (_offlineBanner != null)
            {
                // Detect the well-known "dictation not enabled" Windows error
                // and tell the player exactly how to fix it instead of dumping
                // the raw error string.
                if (message != null && message.Contains("Dictation support is not enabled"))
                {
                    _offlineBanner.text =
                        "🎤 Voice input disabled. Enable in Windows: " +
                        "Settings → Privacy & Security → Speech → Online speech recognition.";
                }
                else
                {
                    _offlineBanner.text = $"🎤 Voice input error: {message}";
                }
                _offlineBanner.style.color = new Color(0.95f, 0.7f, 0.45f);
                _offlineBanner.style.display = DisplayStyle.Flex;
            }
        }

        private void AppendLine(string line)
        {
            _historyLines.Add(line);
            if (_historyLines.Count > MaxHistoryLines)
                _historyLines.RemoveAt(0);
            RefreshHistory();
        }

        // Color used for player turns ("You: ..."). Slightly cooler/blue
        // tone so the player can scan their own input quickly.
        private static readonly Color _playerLineColor = new Color(0.78f, 0.88f, 1f);
        // Color used for NPC turns. Warm off-white for high contrast on
        // the dark scrollview background.
        private static readonly Color _npcLineColor = new Color(0.96f, 0.94f, 0.88f);
        // Color used for system / fallback messages. Muted gold.
        private static readonly Color _systemLineColor = new Color(0.85f, 0.78f, 0.55f);

        private void RefreshHistory()
        {
            if (_history == null) return;
            _history.Clear();
            foreach (var line in _historyLines)
            {
                var label = new Label(line);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 8;
                label.style.fontSize = 15;
                // Tint based on the line's prefix so player vs NPC reads
                // at a glance. The default theme color is too dim against
                // the dark scrollview background to be readable.
                if (line.StartsWith("You:"))
                {
                    label.style.color = _playerLineColor;
                }
                else if (line.StartsWith("("))
                {
                    label.style.color = _systemLineColor;
                    label.style.unityFontStyleAndWeight = FontStyle.Italic;
                }
                else
                {
                    label.style.color = _npcLineColor;
                }
                _history.Add(label);
            }
            // Auto-scroll to the most recent entry. ScrollTo(null) throws,
            // so guard against the empty case (which Show() hits every time
            // because it clears _historyLines first).
            int childCount = _history.contentContainer.childCount;
            if (childCount > 0)
            {
                _history.ScrollTo(_history.contentContainer[childCount - 1]);
            }
        }
    }

    internal static class StyleExtensions
    {
        public static void SetVisible(this IStyle style, bool visible)
        {
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
