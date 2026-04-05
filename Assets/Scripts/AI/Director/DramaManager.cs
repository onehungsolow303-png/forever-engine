using UnityEngine;

namespace ForeverEngine.AI.Director
{
    public class DramaManager
    {
        public float DramaNeed { get; private set; }
        public float TimeSinceLastDrama { get; private set; }

        private float _dramaDecay = 0.02f;
        private float _boredomBuildRate = 0.01f;

        public void TriggerDrama(float amount = 1f) { DramaNeed = Mathf.Max(0, DramaNeed - amount); TimeSinceLastDrama = 0; }

        public void Update(float deltaTime)
        {
            TimeSinceLastDrama += deltaTime;
            DramaNeed = Mathf.Clamp01(DramaNeed + _boredomBuildRate * deltaTime);
        }
    }
}
