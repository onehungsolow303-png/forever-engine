using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Systems;
using ForeverEngine.MonoBehaviour.Rendering;

namespace ForeverEngine.MonoBehaviour.Input
{
    public class PlayerMovement : UnityEngine.MonoBehaviour
    {
        private EntityManager _em;
        private EntityQuery _playerQuery;
        private EntityQuery _transitionQuery;
        private EntityQuery _gameStateQuery;
        private EntityQuery _mapSingletonQuery;

        private void Start()
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _playerQuery = _em.CreateEntityQuery(
                typeof(PlayerTag), typeof(PositionComponent), typeof(CombatStateComponent));
            _transitionQuery = _em.CreateEntityQuery(
                typeof(TransitionComponent), typeof(PositionComponent));
            _gameStateQuery = _em.CreateEntityQuery(typeof(GameStateSingleton));
            _mapSingletonQuery = _em.CreateEntityQuery(typeof(MapDataSingleton));
        }

        private void Update()
        {
            var input = InputManager.Instance;
            if (input == null || input.MoveInput == Vector2Int.zero) return;

            var store = MapDataStore.Instance;
            if (store == null) return;

            if (_gameStateQuery.IsEmpty) return;

            var gameState = _gameStateQuery.GetSingleton<GameStateSingleton>();
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

            // Check for z-level transition at new position
            CheckTransition(playerEntity, pos);

            if (gameState.CurrentState == GameState.Combat)
            {
                combat.MovementRemaining--;
                _em.SetComponentData(playerEntity, combat);
            }
        }
        private void CheckTransition(Entity playerEntity, PositionComponent playerPos)
        {
            var transitions = _transitionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < transitions.Length; i++)
            {
                var tPos = _em.GetComponentData<PositionComponent>(transitions[i]);
                var tComp = _em.GetComponentData<TransitionComponent>(transitions[i]);

                if (tPos.X == playerPos.X && tPos.Y == playerPos.Y && tComp.FromZ == playerPos.Z)
                {
                    var store = MapDataStore.Instance;
                    if (store == null || !store.HasLevel(tComp.ToZ)) continue;

                    // Swap to new z-level
                    store.SwapToLevel(tComp.ToZ);

                    // Update player Z
                    playerPos.Z = tComp.ToZ;
                    _em.SetComponentData(playerEntity, playerPos);

                    // Update map singleton
                    if (!_mapSingletonQuery.IsEmpty)
                    {
                        var mapSingleton = _mapSingletonQuery.GetSingleton<MapDataSingleton>();
                        mapSingleton.CurrentZ = tComp.ToZ;
                        _mapSingletonQuery.SetSingleton(mapSingleton);
                    }

                    // Re-render tiles and fog
                    var tileRenderer = UnityEngine.Object.FindAnyObjectByType<TileRenderer>();
                    if (tileRenderer != null) tileRenderer.RenderLevel(tComp.ToZ);

                    var fogRenderer = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
                    if (fogRenderer != null) fogRenderer.Initialize(store.Width, store.Height);

                    string dir = tComp.TransitionType == 1 ? "up" : "down";
                    UnityEngine.Debug.Log($"[PlayerMovement] Transitioned {dir} to z={tComp.ToZ}");
                    ForeverEngine.Demo.Audio.SoundManager.Instance?.PlayStairs();
                    break;
                }
            }
            transitions.Dispose();
        }
    }
}
