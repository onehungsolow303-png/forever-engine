using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Bridges;

namespace ForeverEngine.Tests.Bridges
{
    public class ServiceWatchdogTests
    {
        [Test]
        public void StartsInUnknownState()
        {
            var go = new GameObject("watchdog-test");
            var w = go.AddComponent<ServiceWatchdog>();
            Assert.IsFalse(w.AllOk);
            Assert.IsFalse(w.AssetManagerOk);
            Assert.IsFalse(w.DirectorHubOk);
            Object.DestroyImmediate(go);
        }
    }
}
