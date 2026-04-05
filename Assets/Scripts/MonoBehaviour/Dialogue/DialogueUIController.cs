using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace ForeverEngine.MonoBehaviour.Dialogue
{
    /// <summary>
    /// Thin UI Toolkit bridge for the AI dialogue panel.
    /// Expects a UIDocument with elements:
    ///   - dialogue-panel (VisualElement)
    ///   - npc-name       (Label)
    ///   - npc-dialogue   (Label)
    ///   - player-input   (TextField)
    ///   - send-button    (Button)
    /// </summary>
    public class DialogueUIController : UnityEngine.MonoBehaviour
    {
        public event Action<string> OnPlayerSubmit;

        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private VisualElement _dialoguePanel;
        private Label         _npcNameLabel;
        private Label         _npcDialogueLabel;
        private TextField     _playerInput;
        private Button        _sendButton;

        // ── Unity ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (uiDocument == null) return;

            _root           = uiDocument.rootVisualElement;
            _dialoguePanel  = _root?.Q("dialogue-panel");
            _npcNameLabel   = _root?.Q<Label>("npc-name");
            _npcDialogueLabel = _root?.Q<Label>("npc-dialogue");
            _playerInput    = _root?.Q<TextField>("player-input");
            _sendButton     = _root?.Q<Button>("send-button");

            _sendButton?.RegisterCallback<ClickEvent>(_ => SubmitInput());
            _playerInput?.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return) SubmitInput();
            });

            Hide();
        }

        // ── Public API ────────────────────────────────────────────────────

        public void Show(string npcName)
        {
            if (_dialoguePanel != null) _dialoguePanel.style.display = DisplayStyle.Flex;
            if (_npcNameLabel   != null) _npcNameLabel.text   = npcName;
            if (_npcDialogueLabel != null) _npcDialogueLabel.text = $"{npcName} looks at you expectantly.";
            if (_playerInput != null) { _playerInput.value = ""; _playerInput.Focus(); }
        }

        public void Hide()
        {
            if (_dialoguePanel != null) _dialoguePanel.style.display = DisplayStyle.None;
        }

        public void ShowNPCResponse(string text)
        {
            if (_npcDialogueLabel != null) _npcDialogueLabel.text = text;
            if (_playerInput != null) { _playerInput.value = ""; _playerInput.Focus(); }
        }

        public void ShowThinking()
        {
            if (_npcDialogueLabel != null) _npcDialogueLabel.text = "...";
        }

        // ── Private ───────────────────────────────────────────────────────

        private void SubmitInput()
        {
            if (_playerInput == null || string.IsNullOrWhiteSpace(_playerInput.value)) return;
            OnPlayerSubmit?.Invoke(_playerInput.value.Trim());
        }
    }
}
