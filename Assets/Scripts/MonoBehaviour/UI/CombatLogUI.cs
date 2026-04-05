using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class CombatLogUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private int _maxVisibleLines = 8;

        private ScrollView _logContainer;
        private EntityManager _em;
        private int _lastLogCount;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _logContainer = root.Q<ScrollView>("combat-log");
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        public void AddMessage(string message, Color color)
        {
            if (_logContainer == null) return;

            var label = new Label(message);
            label.style.color = color;
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            _logContainer.Add(label);

            while (_logContainer.childCount > _maxVisibleLines)
                _logContainer.RemoveAt(0);

            _logContainer.scrollOffset = new Vector2(0, float.MaxValue);
        }

        public void Clear()
        {
            _logContainer?.Clear();
        }
    }
}
