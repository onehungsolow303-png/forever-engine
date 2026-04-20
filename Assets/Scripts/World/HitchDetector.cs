using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Logs frames whose deltaTime exceeds a threshold, to correlate user-observed
    /// flickers with chunk churn events (GRASS-DIAG, DECOR-DIAG, CHUNKMGR-DIAG).
    /// Warm-up skips the first few seconds so scene load doesn't spam.
    /// </summary>
    public class HitchDetector : UnityEngine.MonoBehaviour
    {
        public float WarmupSeconds = 3f;
        public float HitchThresholdMs = 50f; // ~20 FPS frame

        private float _startTime;

        private void Start()
        {
            _startTime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup - _startTime < WarmupSeconds) return;
            float dtMs = Time.unscaledDeltaTime * 1000f;
            if (dtMs >= HitchThresholdMs)
            {
                Debug.Log($"[HITCH-DIAG] dt={dtMs:F1}ms t={Time.realtimeSinceStartup:F2}");
            }
        }
    }
}
