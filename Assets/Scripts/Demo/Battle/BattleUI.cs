using UnityEngine;
using UnityEngine.UIElements;

namespace ForeverEngine.Demo.Battle
{
    public class BattleUI : UnityEngine.MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        private Label _playerName;
        private VisualElement _hpBar;
        private Label _hpText;
        private Label _conditionsLabel;
        private Label _actionLabel;

        private Button _btnMove, _btnAttack, _btnSpell, _btnPotion, _btnEndTurn;

        private VisualElement _tooltip;
        private Label _tooltipName;
        private Label _tooltipConditions;

        public void Initialize(BattleCombatant player)
        {
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = Resources.Load<PanelSettings>("UI/BattlePanelSettings");

            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _doc.rootVisualElement.Add(_root);

            BuildPlayerHUD(player);
            BuildActionBar();
            BuildTooltip();
        }

        private void BuildPlayerHUD(BattleCombatant player)
        {
            var hud = new VisualElement();
            hud.style.position = Position.Absolute;
            hud.style.left = 16; hud.style.bottom = 16;
            hud.style.width = 220; hud.style.height = 100;
            hud.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            hud.style.borderTopLeftRadius = hud.style.borderTopRightRadius =
                hud.style.borderBottomLeftRadius = hud.style.borderBottomRightRadius = 8;
            hud.style.paddingLeft = hud.style.paddingRight =
                hud.style.paddingTop = hud.style.paddingBottom = 8;

            _playerName = new Label(player.Name);
            _playerName.style.fontSize = 16;
            _playerName.style.color = new Color(0.9f, 0.85f, 0.6f);
            hud.Add(_playerName);

            var hpContainer = new VisualElement();
            hpContainer.style.height = 12;
            hpContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            hpContainer.style.borderTopLeftRadius = hpContainer.style.borderTopRightRadius =
                hpContainer.style.borderBottomLeftRadius = hpContainer.style.borderBottomRightRadius = 4;
            hpContainer.style.marginTop = 4;

            _hpBar = new VisualElement();
            _hpBar.style.height = new Length(100, LengthUnit.Percent);
            _hpBar.style.backgroundColor = Color.green;
            _hpBar.style.borderTopLeftRadius = _hpBar.style.borderBottomLeftRadius = 4;
            hpContainer.Add(_hpBar);
            hud.Add(hpContainer);

            _hpText = new Label($"{player.HP}/{player.MaxHP}");
            _hpText.style.fontSize = 11;
            _hpText.style.color = Color.white;
            hud.Add(_hpText);

            _conditionsLabel = new Label("");
            _conditionsLabel.style.fontSize = 11;
            _conditionsLabel.style.color = new Color(1f, 0.7f, 0.3f);
            hud.Add(_conditionsLabel);

            _actionLabel = new Label("");
            _actionLabel.style.fontSize = 11;
            _actionLabel.style.color = new Color(0.7f, 0.9f, 1f);
            hud.Add(_actionLabel);

            _root.Add(hud);
        }

        private void BuildActionBar()
        {
            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.bottom = 16;
            bar.style.left = new Length(50, LengthUnit.Percent);
            bar.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius =
                bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = 8;
            bar.style.paddingLeft = bar.style.paddingRight =
                bar.style.paddingTop = bar.style.paddingBottom = 6;

            _btnMove = MakeButton("Move [WASD]", () => { });
            _btnAttack = MakeButton("Attack [F]", () => BattleManager.Instance?.AttackNearestEnemy());
            _btnSpell = MakeButton("Spell [Q]", () => BattleManager.Instance?.ToggleSpellMenu());
            _btnPotion = MakeButton("Potion [H]", () => BattleManager.Instance?.UseHealthPotion());
            _btnEndTurn = MakeButton("End Turn [Space]", () => BattleManager.Instance?.PlayerEndTurn());

            bar.Add(_btnMove); bar.Add(_btnAttack); bar.Add(_btnSpell);
            bar.Add(_btnPotion); bar.Add(_btnEndTurn);
            _root.Add(bar);
        }

        private Button MakeButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick);
            btn.text = text;
            btn.style.fontSize = 12;
            btn.style.marginLeft = btn.style.marginRight = 4;
            btn.style.paddingLeft = btn.style.paddingRight = 10;
            btn.style.paddingTop = btn.style.paddingBottom = 6;
            btn.style.backgroundColor = new Color(0.25f, 0.22f, 0.2f);
            btn.style.color = new Color(0.9f, 0.85f, 0.7f);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
            return btn;
        }

        private void BuildTooltip()
        {
            _tooltip = new VisualElement();
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.width = 180;
            _tooltip.style.backgroundColor = new Color(0.12f, 0.1f, 0.1f, 0.9f);
            _tooltip.style.borderTopLeftRadius = _tooltip.style.borderTopRightRadius =
                _tooltip.style.borderBottomLeftRadius = _tooltip.style.borderBottomRightRadius = 6;
            _tooltip.style.paddingLeft = _tooltip.style.paddingRight =
                _tooltip.style.paddingTop = _tooltip.style.paddingBottom = 8;
            _tooltip.style.display = DisplayStyle.None;

            _tooltipName = new Label("");
            _tooltipName.style.fontSize = 14;
            _tooltipName.style.color = new Color(1f, 0.85f, 0.5f);
            _tooltip.Add(_tooltipName);

            _tooltipConditions = new Label("");
            _tooltipConditions.style.fontSize = 11;
            _tooltipConditions.style.color = new Color(1f, 0.6f, 0.3f);
            _tooltip.Add(_tooltipConditions);

            _root.Add(_tooltip);
        }

        public void UpdateHUD(BattleCombatant player)
        {
            if (player == null) return;

            float hpPct = (float)player.HP / player.MaxHP;
            _hpBar.style.width = new Length(hpPct * 100f, LengthUnit.Percent);
            _hpBar.style.backgroundColor = Color.Lerp(Color.red, Color.green, hpPct);
            _hpText.text = $"{player.HP}/{player.MaxHP}";

            string conds = "";
            if (player.Conditions != null)
            {
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Poisoned)) conds += "Poisoned ";
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Stunned)) conds += "Stunned ";
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Paralyzed)) conds += "Paralyzed ";
                if (player.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Frightened)) conds += "Frightened ";
            }
            _conditionsLabel.text = conds;

            string action = player.HasAction ? "Action ready" : "Action used";
            action += $" | Move: {player.MovementRemaining}";
            _actionLabel.text = action;
        }

        public void ShowTooltip(BattleCombatant enemy, Vector2 screenPos)
        {
            if (enemy == null)
            {
                _tooltip.style.display = DisplayStyle.None;
                return;
            }

            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.style.left = screenPos.x + 20;
            _tooltip.style.top = Screen.height - screenPos.y;
            _tooltipName.text = enemy.Name;

            string conds = "";
            if (enemy.Conditions != null)
            {
                if (enemy.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Poisoned)) conds += "Poisoned ";
                if (enemy.Conditions.Has(ForeverEngine.RPG.Enums.Condition.Stunned)) conds += "Stunned ";
            }
            _tooltipConditions.text = conds.Length > 0 ? conds : "No visible conditions";
        }

        public void HideTooltip()
        {
            _tooltip.style.display = DisplayStyle.None;
        }
    }
}
