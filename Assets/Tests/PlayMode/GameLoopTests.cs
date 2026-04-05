using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.MonoBehaviour.Bootstrap;

namespace ForeverEngine.Tests
{
    public class GameLoopTests
    {
        [UnityTest]
        public IEnumerator LoadMap_SpawnsPlayerAndEnemy()
        {
            var go = new GameObject("Bootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();

            string testMapPath = Application.dataPath + "/Resources/Maps/test_dungeon/map_data.json";
            bootstrap.LoadMap(testMapPath);

            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var playerQuery = em.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent));
            Assert.AreEqual(1, playerQuery.CalculateEntityCount(), "Should have exactly 1 player");

            var playerPos = playerQuery.GetSingleton<PositionComponent>();
            Assert.AreEqual(2, playerPos.X);
            Assert.AreEqual(2, playerPos.Y);

            var enemyQuery = em.CreateEntityQuery(typeof(AIBehaviorComponent), typeof(StatsComponent));
            Assert.GreaterOrEqual(enemyQuery.CalculateEntityCount(), 1, "Should have at least 1 enemy");

            var store = MapDataStore.Instance;
            Assert.IsNotNull(store);
            Assert.AreEqual(8, store.Width);
            Assert.AreEqual(8, store.Height);
            Assert.IsTrue(store.Walkability[1 * 8 + 1]);
            Assert.IsFalse(store.Walkability[0 * 8 + 0]);

            Object.Destroy(go);
            store.Dispose();
        }
    }
}
