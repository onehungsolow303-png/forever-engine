using NUnit.Framework;
using ForeverEngine.ECS.Data;
using Unity.Collections;

namespace ForeverEngine.Tests
{
    public class MapDataStoreTests
    {
        [Test]
        public void CreateStore_SetsWidthHeight()
        {
            var store = new MapDataStore();
            store.Initialize(64, 64);
            Assert.AreEqual(64, store.Width);
            Assert.AreEqual(64, store.Height);
            store.Dispose();
        }

        [Test]
        public void Walkability_CorrectSize()
        {
            var store = new MapDataStore();
            store.Initialize(32, 32);
            Assert.AreEqual(32 * 32, store.Walkability.Length);
            store.Dispose();
        }

        [Test]
        public void FogGrid_InitializedUnexplored()
        {
            var store = new MapDataStore();
            store.Initialize(16, 16);
            for (int i = 0; i < store.FogGrid.Length; i++)
                Assert.AreEqual(0, store.FogGrid[i]);
            store.Dispose();
        }

        [Test]
        public void SetWalkability_RoundTrips()
        {
            var store = new MapDataStore();
            store.Initialize(8, 8);
            for (int x = 0; x < 8; x++)
            {
                store.Walkability[0 * 8 + x] = true;
                store.Walkability[1 * 8 + x] = false;
            }
            Assert.IsTrue(store.Walkability[0]);
            Assert.IsFalse(store.Walkability[8]);
            store.Dispose();
        }

        [Test]
        public void LoadZLevel_ParsesWalkabilityArray()
        {
            var store = new MapDataStore();
            store.Initialize(4, 4);
            int[] flat = { 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            store.LoadWalkability(0, flat);
            Assert.IsTrue(store.Walkability[0]);
            Assert.IsTrue(store.Walkability[1]);
            Assert.IsFalse(store.Walkability[2]);
            Assert.IsTrue(store.Walkability[15]);
            store.Dispose();
        }
    }
}
