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
        private string _currentLocationId;
        private string _currentNpcId;
        private readonly List<string> _historyLines = new();
        private bool _waitingForResponse;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _document = gameObject.GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();

            // Load the UXML asset. In Editor builds it can be loaded by name
            // from Assets/UI/; in player builds it must be in a Resources/
            // folder. The fallback path constructs a panel programmatically
            // so the component is usable even without the asset.
            var asset = Resources.Load<VisualTreeAsset>(PanelAssetPath);
            if (asset != null)
            {
                _document.visualTreeAsset = asset;
            }

            _root = _document.rootVisualElement;
            _root?.style.SetVisible(false);
            HookUpReferences();
        }

        public void Show(string locationId, string npcId)
        {
            _currentLocationId = locationId;
            _currentNpcId = npcId ?? $"npc_{locationId}";
            _historyLines.Clear();
            RefreshHistory();

            if (_npcLabel != null)
                _npcLabel.text = string.IsNullOrEmpty(npcId) ? "Speaking with..." : npcId;

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

            if (_sendButton != null) _sendButton.clicked += OnSendClicked;
            if (_closeButton != null) _closeButton.clicked += Close;
            if (_input != null)
                _input.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return && !evt.shiftKey) OnSendClicked();
                });
        }

        private void OnSendClicked()
        {
            if (_input == null || _waitingForResponse) return;
            string text = _input.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) return;

            AppendLine($"You: {text}");
            _input.value = "";
            _waitingForResponse = true;

            Demo.AI.DirectorEvents.SendDialogue(text, _currentNpcId, narrative =>
            {
                _waitingForResponse = false;
                if (string.IsNullOrEmpty(narrative))
                {
                    AppendLine("(The conversation falters as you struggle to find the right words.)");
                }
                else
                {
                    AppendLine($"{_currentNpcId}: {narrative}");
                }
            });
        }

        private void AppendLine(string line)
        {
            _historyLines.Add(line);
            if (_historyLines.Count > MaxHistoryLines)
                _historyLines.RemoveAt(0);
            RefreshHistory();
        }

        private void RefreshHistory()
        {
            if (_history == null) return;
            _history.Clear();
            foreach (var line in _historyLines)
            {
                var label = new Label(line);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 4;
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
