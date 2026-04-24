using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForeverEngine.Procedural;

namespace ForeverEngine.Tests.World
{
    // Verifies PrefabRegistryValidator turns a deliberately-broken registry
    // (one where baked GUIDs don't resolve) into a loud, actionable error
    // instead of an invisible world.
    [TestFixture]
    public class PrefabRegistryValidatorTests
    {
        [SetUp]
        public void ResetBetweenTests()
        {
            PrefabRegistryValidator.Reset_ForTest();
        }

        [Test]
        public void MissRateAboveThreshold_ActivatesBanner()
        {
            int n = PrefabRegistryValidator.SampleSize_ForTest;

            // 6% miss rate (30/500) — just above the 5% threshold.
            int misses = (int)(n * 0.06f);
            int hits = n - misses;

            // The validator logs an error when miss rate breaches threshold.
            // Pattern-match both the summary and the GUID dump so the test
            // runner doesn't fail on an unhandled error.
            LogAssert.Expect(LogType.Error, new Regex("MISS RATE"));
            LogAssert.Expect(LogType.Error, new Regex("Missing \\d+ distinct GUIDs"));

            for (int i = 0; i < hits; i++)
                PrefabRegistryValidator.RecordLookup($"hit-{i}", resolved: true);
            for (int i = 0; i < misses; i++)
                PrefabRegistryValidator.RecordLookup($"miss-{i}", resolved: false);

            Assert.IsTrue(PrefabRegistryValidator.ResultComputed_ForTest);
            Assert.IsTrue(PrefabRegistryValidator.BannerActive);
            Assert.AreEqual(hits, PrefabRegistryValidator.Hits);
            Assert.AreEqual(misses, PrefabRegistryValidator.Misses);
        }

        [Test]
        public void MissRateBelowThreshold_NoBanner()
        {
            int n = PrefabRegistryValidator.SampleSize_ForTest;

            // 2% miss rate (10/500) — well below threshold.
            int misses = (int)(n * 0.02f);
            int hits = n - misses;

            for (int i = 0; i < hits; i++)
                PrefabRegistryValidator.RecordLookup($"hit-{i}", resolved: true);
            for (int i = 0; i < misses; i++)
                PrefabRegistryValidator.RecordLookup($"miss-{i}", resolved: false);

            Assert.IsTrue(PrefabRegistryValidator.ResultComputed_ForTest);
            Assert.IsFalse(PrefabRegistryValidator.BannerActive);
        }

        [Test]
        public void PostVerdictLookups_AreNoOps()
        {
            int n = PrefabRegistryValidator.SampleSize_ForTest;

            // Fill sample with all hits → verdict computed, no banner.
            for (int i = 0; i < n; i++)
                PrefabRegistryValidator.RecordLookup($"hit-{i}", resolved: true);
            Assert.IsTrue(PrefabRegistryValidator.ResultComputed_ForTest);
            Assert.AreEqual(n, PrefabRegistryValidator.Hits);

            // Further records must not bump counters — the validator only
            // watches the first N lookups so steady-state gameplay doesn't
            // recompute after a legitimate transient miss.
            for (int i = 0; i < 50; i++)
                PrefabRegistryValidator.RecordLookup($"late-{i}", resolved: false);
            Assert.AreEqual(n, PrefabRegistryValidator.Hits);
            Assert.AreEqual(0, PrefabRegistryValidator.Misses);
        }

        [Test]
        public void DistinctMissingGuids_AreRecorded()
        {
            int n = PrefabRegistryValidator.SampleSize_ForTest;

            // Trigger a banner so error logs are expected.
            int misses = (int)(n * 0.10f);
            int hits = n - misses;
            LogAssert.Expect(LogType.Error, new Regex("MISS RATE"));
            LogAssert.Expect(LogType.Error, new Regex("Missing \\d+ distinct GUIDs"));

            for (int i = 0; i < hits; i++)
                PrefabRegistryValidator.RecordLookup($"hit-{i}", resolved: true);
            // Feed the same 3 missing GUIDs many times — must dedupe to 3.
            var missGuids = new[] { "guid-A", "guid-B", "guid-C" };
            for (int i = 0; i < misses; i++)
                PrefabRegistryValidator.RecordLookup(missGuids[i % missGuids.Length], resolved: false);

            Assert.AreEqual(3, PrefabRegistryValidator.MissedGuids.Count);
        }
    }
}
