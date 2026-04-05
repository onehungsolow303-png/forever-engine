using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class HUDManager : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private Label _modeLabel;
        private Label _zLevelLabel;
        private Label _nameLabel;
        private Label _hpLabel;
        private VisualElement _hpBar;
        private Label _statsLabel;

        private EntityManager _em;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;

            _modeLabel = root.Q<Label>("mode-label");
            _zLevelLabel = root.Q<Label>("zlevel-label");
            _nameLabel = root.Q<Label>("player-name");
            _hpLabel = root.Q<Label>("hp-text");
            _hpBar = root.Q<VisualElement>("hp-bar-fill");
            _statsLabel = root.Q<Label>("stats-text");

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private void Update()
        {
            if (_em == null) return;

            var stateQuery = _em.CreateEntityQuery(typeof(GameStateSingleton));
            if (!stateQuery.IsEmpty)
            {
                var gs = stateQuery.GetSingleton<GameStateSingleton>();
                if (_modeLabel != null)
                {
                    _modeLabel.text = gs.CurrentState.ToString().ToUpper();
                    _modeLabel.style.color = gs.CurrentState switch
                    {
                        GameState.Exploration => new Color(0.2f, 0.8f, 0.2f),
                        GameState.Combat => new Color(1f, 0.84f, 0f),
                        GameState.GameOver => new Color(1f, 0.2f, 0.2f),
                        _ => new Color(0.7f, 0.7f, 0.7f)
                    };
                }
            }

            var playerQuery = _em.CreateEntityQuery(typeof(PlayerTag), typeof(StatsComponent));
            if (!playerQuery.IsEmpty)
            {
                var stats = playerQuery.GetSingleton<StatsComponent>();
                if (_hpLabel != null)
                    _hpLabel.text = $"HP: {stats.HP}/{stats.MaxHP}";
                if (_hpBar != null)
                    _hpBar.style.width = new Length(stats.HPPercent * 100f, LengthUnit.Percent);
                if (_statsLabel != null)
                    _statsLabel.text = $"AC:{stats.AC}  STR:{stats.Strength}  DEX:{stats.Dexterity}  SPD:{stats.Speed}";
            }
        }
    }
}
