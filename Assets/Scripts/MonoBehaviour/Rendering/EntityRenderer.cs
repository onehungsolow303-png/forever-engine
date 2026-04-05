using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.MonoBehaviour.Rendering
{
    public class EntityRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private GameObject _creaturePrefab;

        private EntityManager _em;
        private EntityQuery _entityQuery;
        private Dictionary<Entity, GameObject> _entityObjects = new();

        private void Start()
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _entityQuery = _em.CreateEntityQuery(
                typeof(PositionComponent), typeof(VisualComponent), typeof(CombatStateComponent));
        }

        private void LateUpdate()
        {
            var entities = _entityQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            var activeEntities = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var pos = _em.GetComponentData<PositionComponent>(entity);
                var combat = _em.GetComponentData<CombatStateComponent>(entity);

                if (!combat.Alive) continue;

                activeEntities.Add(entity);

                if (!_entityObjects.TryGetValue(entity, out var go))
                {
                    go = Instantiate(_creaturePrefab, transform);
                    _entityObjects[entity] = go;

                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = combat.TokenType switch
                        {
                            TokenType.Player => _config != null ? _config.PlayerColor : Color.blue,
                            TokenType.Enemy => _config != null ? _config.EnemyColor : Color.red,
                            TokenType.NPC => _config != null ? _config.NPCColor : Color.yellow,
                            _ => Color.gray
                        };
                    }
                }

                Vector3 targetPos = new Vector3(pos.X + 0.5f, pos.Y + 0.5f, -1f);
                go.transform.position = Vector3.Lerp(
                    go.transform.position, targetPos, Time.deltaTime * 10f);
            }

            var toRemove = new List<Entity>();
            foreach (var kvp in _entityObjects)
            {
                if (!activeEntities.Contains(kvp.Key))
                {
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var e in toRemove) _entityObjects.Remove(e);

            entities.Dispose();
        }
    }
}
