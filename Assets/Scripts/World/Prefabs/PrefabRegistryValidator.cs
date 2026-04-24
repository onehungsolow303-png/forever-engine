using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Monitors PrefabRegistry.Resolve hit/miss rate during the first N baked
    /// prop lookups after client startup. If the miss rate exceeds the
    /// threshold the validator triggers an in-game banner and dumps every
    /// missing GUID to the log so the next time registry/props.bin drift
    /// recurs the user sees a loud, actionable error instead of an invisible
    /// world.
    ///
    /// Intentionally not a ScriptableObject / Singleton — the counters are
    /// pure static state so editor tests can exercise them without Unity Play
    /// mode. The banner side-effect (RegistryMissBanner) only activates when
    /// Application.isPlaying.
    /// </summary>
    public static class PrefabRegistryValidator
    {
        private const int SampleSize = 500;
        private const float MissThresholdFraction = 0.05f;
        private const int LoggedMissLimit = 25;

        private static int _hits;
        private static int _misses;
        private static readonly HashSet<string> _recordedMisses = new();
        private static bool _resultComputed;
        private static bool _bannerActive;

        public static int Hits => _hits;
        public static int Misses => _misses;
        public static bool BannerActive => _bannerActive;
        public static IReadOnlyCollection<string> MissedGuids => _recordedMisses;

        /// <summary>
        /// Called from BakedPropRenderer once per placement lookup. After the
        /// first SampleSize calls a verdict is computed and further calls are
        /// no-ops.
        /// </summary>
        public static void RecordLookup(string guid, bool resolved)
        {
            if (_resultComputed) return;
            if (resolved) _hits++;
            else
            {
                _misses++;
                if (!string.IsNullOrEmpty(guid)) _recordedMisses.Add(guid);
            }
            if (_hits + _misses >= SampleSize) FinalizeSample();
        }

        private static void FinalizeSample()
        {
            _resultComputed = true;
            int total = _hits + _misses;
            float missRate = total == 0 ? 0f : (float)_misses / total;
            if (missRate > MissThresholdFraction)
            {
                _bannerActive = true;
                Debug.LogError(
                    $"[PrefabRegistryValidator] MISS RATE {missRate:P1} over {total} lookups " +
                    $"EXCEEDS {MissThresholdFraction:P0} threshold. Rebuild client + redeploy props.bin.");
                Debug.LogError(
                    $"[PrefabRegistryValidator] Missing {_recordedMisses.Count} distinct GUIDs: " +
                    $"{string.Join(", ", _recordedMisses.Take(LoggedMissLimit))}" +
                    (_recordedMisses.Count > LoggedMissLimit ? " ..." : ""));
                if (Application.isPlaying)
                    RegistryMissBanner.EnsureInstalled();
            }
            else
            {
                Debug.Log(
                    $"[PrefabRegistryValidator] Miss rate {missRate:P1} OK " +
                    $"({_hits}/{total} resolve), threshold {MissThresholdFraction:P0}.");
            }
        }

        // Test-only helpers. Public (not internal) so the EditMode tests
        // assembly can call them without needing InternalsVisibleTo wiring.
        // The `_ForTest` suffix flags the contract.
        public static void Reset_ForTest()
        {
            _hits = 0;
            _misses = 0;
            _recordedMisses.Clear();
            _resultComputed = false;
            _bannerActive = false;
        }

        public static int SampleSize_ForTest => SampleSize;
        public static float MissThreshold_ForTest => MissThresholdFraction;
        public static bool ResultComputed_ForTest => _resultComputed;
    }
}
