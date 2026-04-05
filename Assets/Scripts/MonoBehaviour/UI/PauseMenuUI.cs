using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.MonoBehaviour.SaveLoad;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class PauseMenuUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _pausePanel;
        private bool _paused;
        public bool IsPaused => _paused;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _pausePanel = root.Q<VisualElement>("pause-panel");
            root.Q<Button>("btn-resume")?.RegisterCallback<ClickEvent>(_ => Resume());
            root.Q<Button>("btn-save")?.RegisterCallback<ClickEvent>(_ => SaveManager.Instance?.Save());
            root.Q<Button>("btn-quit-menu")?.RegisterCallback<ClickEvent>(_ => Resume());
        }

        public void TogglePause() { if (_paused) Resume(); else Pause(); }
        private void Pause() { _paused = true; Time.timeScale = 0f; if (_pausePanel != null) _pausePanel.style.display = DisplayStyle.Flex; }
        private void Resume() { _paused = false; Time.timeScale = 1f; if (_pausePanel != null) _pausePanel.style.display = DisplayStyle.None; }
    }
}
