using UnityEngine;
using UnityEngine.UIElements;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class OptionsUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _optionsPanel;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _optionsPanel = root.Q<VisualElement>("options-panel");
            root.Q<Slider>("slider-master-vol")?.RegisterValueChangedCallback(e => AudioListener.volume = e.newValue);
            root.Q<Button>("btn-options-back")?.RegisterCallback<ClickEvent>(_ => Hide());
        }

        public void Show() { if (_optionsPanel != null) _optionsPanel.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_optionsPanel != null) _optionsPanel.style.display = DisplayStyle.None; }
    }
}
