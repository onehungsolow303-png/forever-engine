using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.AI.SelfHealing
{
    public class PerformanceRegulator : MonoBehaviour
    {
        public static PerformanceRegulator Instance { get; private set; }
        [SerializeField] private float _targetFrameTimeMs = 16.6f;
        [SerializeField] private int _consecutiveThreshold = 10;

        private int _overBudgetFrames;
        private float _qualityLevel = 1f;

        public float QualityLevel => _qualityLevel;

        private void Awake() => Instance = this;

        private void LateUpdate()
        {
            float frameTime = Time.unscaledDeltaTime * 1000f;
            if (frameTime > _targetFrameTimeMs)
            {
                _overBudgetFrames++;
                if (_overBudgetFrames >= _consecutiveThreshold) ReduceQuality();
            }
            else
            {
                _overBudgetFrames = 0;
                if (frameTime < _targetFrameTimeMs * 0.8f) IncreaseQuality();
            }
        }

        private void ReduceQuality()
        {
            _qualityLevel = Mathf.Max(0.1f, _qualityLevel - 0.1f);
            Debug.Log($"[PerfReg] Reducing quality to {_qualityLevel:F1}");
        }

        private void IncreaseQuality()
        {
            _qualityLevel = Mathf.Min(1f, _qualityLevel + 0.01f);
        }
    }
}
