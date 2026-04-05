using UnityEngine;
using System;

namespace ForeverEngine.AI.SelfHealing
{
    public class FaultBoundary
    {
        private string _systemName;
        private int _failureCount;
        private int _maxRetries;
        private bool _disabled;

        public bool IsDisabled => _disabled;
        public int Failures => _failureCount;

        public FaultBoundary(string systemName, int maxRetries = 3)
        {
            _systemName = systemName; _maxRetries = maxRetries;
        }

        public bool TryExecute(Action action)
        {
            if (_disabled) return false;
            try { action(); _failureCount = 0; return true; }
            catch (Exception e)
            {
                _failureCount++;
                Debug.LogError($"[SelfHeal] {_systemName} failed ({_failureCount}/{_maxRetries}): {e.Message}");
                if (_failureCount >= _maxRetries) { _disabled = true; Debug.LogWarning($"[SelfHeal] {_systemName} disabled"); }
                return false;
            }
        }

        public void AttemptRecovery()
        {
            if (_disabled && _failureCount < _maxRetries * 2) { _disabled = false; _failureCount = 0; Debug.Log($"[SelfHeal] Recovering {_systemName}"); }
        }

        public void Reset() { _failureCount = 0; _disabled = false; }
    }
}
