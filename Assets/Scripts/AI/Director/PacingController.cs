using UnityEngine;

namespace ForeverEngine.AI.Director
{
    public class PacingController
    {
        public float CurrentIntensity { get; private set; }
        public float TimeSincePeak { get; private set; }
        public float TimeSinceCalm { get; private set; }

        private float _decayRate = 0.05f;
        private float _peakThreshold = 0.7f;
        private float _calmThreshold = 0.3f;

        public void AddIntensity(float amount) => CurrentIntensity = Mathf.Clamp01(CurrentIntensity + amount);
        public void SetIntensity(float value) => CurrentIntensity = Mathf.Clamp01(value);

        public void Update(float deltaTime)
        {
            CurrentIntensity = Mathf.Max(0, CurrentIntensity - _decayRate * deltaTime);
            if (CurrentIntensity >= _peakThreshold) { TimeSincePeak = 0; TimeSinceCalm += deltaTime; }
            else if (CurrentIntensity <= _calmThreshold) { TimeSinceCalm = 0; TimeSincePeak += deltaTime; }
            else { TimeSincePeak += deltaTime; TimeSinceCalm += deltaTime; }
        }
    }
}
