using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleInputController : UnityEngine.MonoBehaviour
    {
        private BattleRenderer3D _renderer;
        private BattleManager _battle;
        private Camera _cam;
        private (int x, int y) _hoveredCell = (-1, -1);
        private (int x, int y) _lastPathCell = (-1, -1);

        public (int x, int y) HoveredCell => _hoveredCell;
        public BattleCombatant HoveredEnemy { get; private set; }

        public void Initialize(BattleRenderer3D renderer, BattleManager battle, Camera cam)
        {
            _renderer = renderer;
            _battle = battle;
            _cam = cam;
        }

        private void Update()
        {
            if (_battle == null || _battle.BattleOver) return;

            UpdateHover();

            // Show path preview during player's turn
            if (_battle.CurrentTurn != null && _battle.CurrentTurn.IsPlayer)
            {
                UpdatePathPreview();
                HandleClick();
            }
            else
            {
                _renderer.ClearPathPreview();
                _lastPathCell = (-1, -1);
            }
        }

        private void UpdateHover()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) { _hoveredCell = (-1, -1); HoveredEnemy = null; return; }

            Vector3 hitPoint = ray.origin + ray.direction * t;
            _hoveredCell = _renderer.WorldToGrid(hitPoint);

            HoveredEnemy = null;
            foreach (var c in _battle.Combatants)
            {
                if (!c.IsAlive || c.IsPlayer) continue;
                if (c.X == _hoveredCell.x && c.Y == _hoveredCell.y)
                {
                    HoveredEnemy = c;
                    break;
                }
            }
        }

        private void UpdatePathPreview()
        {
            if (_hoveredCell.x < 0 || (_hoveredCell.x == _lastPathCell.x && _hoveredCell.y == _lastPathCell.y))
                return;

            _lastPathCell = _hoveredCell;
            _renderer.ShowPathPreview(_battle.Grid, _battle.CurrentTurn,
                _hoveredCell.x, _hoveredCell.y, _battle.Combatants);
        }

        private void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_hoveredCell.x < 0) return;

            var current = _battle.CurrentTurn;
            if (current == null || !current.IsPlayer) return;

            // Click on enemy = attack
            if (HoveredEnemy != null && current.HasAction)
            {
                int dist = Mathf.Abs(current.X - HoveredEnemy.X) + Mathf.Abs(current.Y - HoveredEnemy.Y);
                if (dist <= 1)
                {
                    _battle.PlayerAttack(HoveredEnemy);
                    return;
                }
            }

            // Click on walkable tile = move
            if (_battle.Grid.IsWalkable(_hoveredCell.x, _hoveredCell.y))
            {
                int dist = Mathf.Abs(current.X - _hoveredCell.x) + Mathf.Abs(current.Y - _hoveredCell.y);
                if (dist <= current.MovementRemaining)
                {
                    bool occupied = false;
                    foreach (var c in _battle.Combatants)
                    {
                        if (c.IsAlive && c.X == _hoveredCell.x && c.Y == _hoveredCell.y)
                        { occupied = true; break; }
                    }
                    if (!occupied)
                    {
                        _battle.PlayerMoveTo(_hoveredCell.x, _hoveredCell.y);
                        _lastPathCell = (-1, -1); // Force path refresh
                    }
                }
            }
        }
    }
}
