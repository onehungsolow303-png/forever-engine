using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;
using ECSWorld = Unity.Entities.World;
using Unity.Jobs;
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
            // Ensure ECS world exists
            if (ECSWorld.DefaultGameObjectInjectionWorld == null)
                DefaultWorldInitialization.Initialize("TestWorld", false);

            yield return null;

            var em = ECSWorld.DefaultGameObjectInjectionWorld.EntityManager;

            // Import map directly
            var go = new GameObject("Importer");
            var importer = go.AddComponent<MapImporter>();

            string testMapPath = Application.dataPath + "/Resources/Maps/test_dungeon/map_data.json";
            importer.Import(testMapPath, em);

            yield return null;
            yield return null; // Extra frame for systems to run

            // Verify player spawned
            var playerQuery = em.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent));
            Assert.AreEqual(1, playerQuery.CalculateEntityCount(), "Should have exactly 1 player");

            var playerPos = playerQuery.GetSingleton<PositionComponent>();
            Assert.AreEqual(2, playerPos.X);
            Assert.AreEqual(2, playerPos.Y);

            // Verify enemy spawned
            var enemyQuery = em.CreateEntityQuery(typeof(AIBehaviorComponent), typeof(StatsComponent));
            Assert.GreaterOrEqual(enemyQuery.CalculateEntityCount(), 1, "Should have at least 1 enemy");

            // Verify map data store
            var store = MapDataStore.Instance;
            Assert.IsNotNull(store);
            Assert.AreEqual(8, store.Width);
            Assert.AreEqual(8, store.Height);

            // Complete all pending jobs before reading NativeArrays
            ECSWorld.DefaultGameObjectInjectionWorld.Unmanaged.ResolveSystemStateRef(
                ECSWorld.DefaultGameObjectInjectionWorld.GetExistingSystem(
                    typeof(ForeverEngine.ECS.Systems.FogOfWarSystem))).Dependency.Complete();

            Assert.IsTrue(store.Walkability[1 * 8 + 1]);
            Assert.IsFalse(store.Walkability[0 * 8 + 0]);

            // Cleanup — complete all jobs before dispose
            Object.Destroy(go);
            JobHandle.ScheduleBatchedJobs();
            ECSWorld.DefaultGameObjectInjectionWorld.EntityManager.CompleteAllTrackedJobs();
            store.Dispose();
        }
    }
}
