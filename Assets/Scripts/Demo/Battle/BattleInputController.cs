using UnityEngine;
using ForeverEngine.Core.Enums;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.Battle
{
    public class BattleInputController : UnityEngine.MonoBehaviour
    {
        private BattleRenderer _battle;
        private Camera _cam;
        private (int x, int y) _hoveredCell = (-1, -1);

        public (int x, int y) HoveredCell => _hoveredCell;
        public BattleCombatantDto HoveredEnemy { get; private set; }

        public void Initialize(BattleRenderer battle, Camera cam)
        {
            _battle = battle;
            _cam = cam;
        }

        private void Update()
        {
            if (_battle == null || _battle.BattleOver) return;

            UpdateHover();

            if (_battle.IsPlayerTurn && !_battle.BattleOver)
            {
                HandleClick();
            }
        }

        private void UpdateHover()
        {
            if (_cam == null) { _hoveredCell = (-1, -1); HoveredEnemy = null; return; }

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) { _hoveredCell = (-1, -1); HoveredEnemy = null; return; }

            Vector3 hitPoint = ray.origin + ray.direction * t;
            _hoveredCell = _battle.WorldToGrid(hitPoint);

            HoveredEnemy = null;
            var combatant = _battle.GetCombatantAt(_hoveredCell.x, _hoveredCell.y);
            if (combatant != null && !combatant.IsPlayer && combatant.Hp > 0)
            {
                HoveredEnemy = combatant;
            }
        }

        private void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (!_battle.IsPlayerTurn || _battle.BattleOver) return;

            if (HoveredEnemy != null)
            {
                NetworkClient.Instance.Send(new BattleActionMessage
                {
                    BattleId = _battle.BattleId,
                    ActionType = BattleActionType.MeleeAttack,
                    TargetId = HoveredEnemy.Id
                });
                return;
            }

            if (_hoveredCell.x >= 0 && _hoveredCell.y >= 0
                && _battle.IsValidMoveTarget(_hoveredCell.x, _hoveredCell.y))
            {
                NetworkClient.Instance.Send(new BattleActionMessage
                {
                    BattleId = _battle.BattleId,
                    ActionType = BattleActionType.Move,
                    TargetX = _hoveredCell.x,
                    TargetY = _hoveredCell.y
                });
            }
        }
    }
}
