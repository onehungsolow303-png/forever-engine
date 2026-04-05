using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.AI.SelfHealing
{
    public class SystemMonitor : UnityEngine.MonoBehaviour
    {
        public static SystemMonitor Instance { get; private set; }

        private Dictionary<string, FaultBoundary> _boundaries = new();
        private Dictionary<string, float> _systemTimings = new();

        private void Awake() => Instance = this;

        public FaultBoundary GetOrCreate(string systemName, int maxRetries = 3)
        {
            if (!_boundaries.TryGetValue(systemName, out var fb))
            {
                fb = new FaultBoundary(systemName, maxRetries);
                _boundaries[systemName] = fb;
            }
            return fb;
        }

        public void RecordTiming(string system, float ms) => _systemTimings[system] = ms;

        public float GetTiming(string system) => _systemTimings.TryGetValue(system, out float v) ? v : 0f;

        public List<string> GetDisabledSystems()
        {
            var result = new List<string>();
            foreach (var kv in _boundaries) if (kv.Value.IsDisabled) result.Add(kv.Key);
            return result;
        }

        public void RecoverAll()
        {
            foreach (var fb in _boundaries.Values) fb.AttemptRecovery();
        }

        public int TotalFailures { get { int t = 0; foreach (var fb in _boundaries.Values) t += fb.Failures; return t; } }
    }
}
