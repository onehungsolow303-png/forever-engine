using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.MonoBehaviour.Dialogue;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class DialogueUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _dialoguePanel;
        private Label _speakerLabel;
        private Label _textLabel;
        private VisualElement _choiceContainer;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _dialoguePanel = root.Q<VisualElement>("dialogue-panel");
            _speakerLabel = root.Q<Label>("dialogue-speaker");
            _textLabel = root.Q<Label>("dialogue-text");
            _choiceContainer = root.Q<VisualElement>("dialogue-choices");
        }

        private void Update()
        {
            var mgr = DialogueManager.Instance;
            if (mgr == null) return;
            if (_dialoguePanel != null) _dialoguePanel.style.display = mgr.InDialogue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!mgr.InDialogue) return;
            var node = mgr.ActiveState.CurrentNode;
            if (node == null) return;
            if (_speakerLabel != null) _speakerLabel.text = node.Speaker ?? "";
            if (_textLabel != null) _textLabel.text = node.Text ?? "";
            if (_choiceContainer != null)
            {
                _choiceContainer.Clear();
                if (node.IsTerminal) { _choiceContainer.Add(new Button(() => mgr.EndDialogue()) { text = "[Continue]" }); }
                else { var choices = mgr.ActiveState.GetAvailableChoices(new HashSet<string>()); for (int i = 0; i < choices.Count; i++) { int idx = i; _choiceContainer.Add(new Button(() => mgr.Choose(idx)) { text = choices[idx].Text }); } }
            }
        }
    }
}
