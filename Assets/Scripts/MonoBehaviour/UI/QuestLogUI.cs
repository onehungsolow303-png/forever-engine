using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class QuestLogUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _questPanel;
        private ScrollView _questList;
        private bool _visible;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _questPanel = root.Q<VisualElement>("quest-panel");
            _questList = root.Q<ScrollView>("quest-list");
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_questPanel != null) _questPanel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_visible) Refresh();
        }

        public void Refresh()
        {
            if (_questList == null) return;
            _questList.Clear();
            var qs = QuestSystem.Instance;
            if (qs == null) return;
            foreach (var quest in qs.GetActiveQuests())
            {
                var header = new Label($"[Active] {quest.Definition.Title}");
                header.style.color = new Color(1f, 0.84f, 0f); header.style.fontSize = 14;
                _questList.Add(header);
                foreach (var obj in quest.Definition.Objectives)
                {
                    int progress = quest.GetObjectiveProgress(obj.Id);
                    var line = new Label($"  - {obj.Description}: {progress}/{obj.RequiredCount}");
                    line.style.fontSize = 12;
                    line.style.color = progress >= obj.RequiredCount ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.7f, 0.7f, 0.7f);
                    _questList.Add(line);
                }
            }
            foreach (var quest in qs.GetCompletedQuests())
            {
                var header = new Label($"[Done] {quest.Definition.Title}");
                header.style.color = new Color(0.5f, 0.5f, 0.5f); header.style.fontSize = 14;
                _questList.Add(header);
            }
        }
    }
}
