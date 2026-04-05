using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.Input
{
    public class PlayerMovement : UnityEngine.MonoBehaviour
    {
        private EntityManager _em;
        private EntityQuery _playerQuery;

        private void Start()
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _playerQuery = _em.CreateEntityQuery(
                typeof(PlayerTag), typeof(PositionComponent), typeof(CombatStateComponent));
        }

        private void Update()
        {
            var input = InputManager.Instance;
            if (input == null || input.MoveInput == Vector2Int.zero) return;

            var store = MapDataStore.Instance;
            if (store == null) return;

            var stateQuery = _em.CreateEntityQuery(typeof(GameStateSingleton));
            if (stateQuery.IsEmpty) return;

            var gameState = stateQuery.GetSingleton<GameStateSingleton>();
            if (gameState.CurrentState != GameState.Exploration &&
                gameState.CurrentState != GameState.Combat)
                return;

            var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) { entities.Dispose(); return; }

            var playerEntity = entities[0];
            entities.Dispose();

            var pos = _em.GetComponentData<PositionComponent>(playerEntity);
            var combat = _em.GetComponentData<CombatStateComponent>(playerEntity);

            if (gameState.CurrentState == GameState.Combat)
            {
                if (combat.MovementRemaining <= 0) return;
            }

            int newX = pos.X + input.MoveInput.x;
            int newY = pos.Y + input.MoveInput.y;

            if (newX < 0 || newX >= store.Width || newY < 0 || newY >= store.Height)
                return;

            int idx = newY * store.Width + newX;
            if (!store.Walkability[idx]) return;

            pos.X = newX;
            pos.Y = newY;
            _em.SetComponentData(playerEntity, pos);

            if (gameState.CurrentState == GameState.Combat)
            {
                combat.MovementRemaining--;
                _em.SetComponentData(playerEntity, combat);
            }
        }
    }
}
