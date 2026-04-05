using UnityEngine;
using UnityEngine.UIElements;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class MainMenuUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _menuPanel;
        public System.Action OnNewGame;
        public System.Action OnLoadGame;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _menuPanel = root.Q<VisualElement>("main-menu-panel");
            root.Q<Button>("btn-new-game")?.RegisterCallback<ClickEvent>(_ => OnNewGame?.Invoke());
            root.Q<Button>("btn-load")?.RegisterCallback<ClickEvent>(_ => OnLoadGame?.Invoke());
            root.Q<Button>("btn-quit")?.RegisterCallback<ClickEvent>(_ => Application.Quit());
        }

        public void Show() { if (_menuPanel != null) _menuPanel.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_menuPanel != null) _menuPanel.style.display = DisplayStyle.None; }
    }
}
