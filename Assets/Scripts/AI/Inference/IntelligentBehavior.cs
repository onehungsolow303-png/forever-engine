using UnityEngine;

namespace ForeverEngine.AI.Inference
{
    public abstract class IntelligentBehavior : UnityEngine.MonoBehaviour
    {
        protected InferenceEngine inferenceEngine;
        private bool _useInference = true;

        protected virtual void Start()
        {
            inferenceEngine = InferenceEngine.Instance;
        }

        protected void Update()
        {
            if (_useInference && inferenceEngine != null && inferenceEngine.IsAvailable)
            {
                var input = GetModelInput();
                var output = inferenceEngine.Infer(input);
                if (output != null) { ApplyModelOutput(output); return; }
            }
            FallbackBehavior();
        }

        protected abstract float[] GetModelInput();
        protected abstract void ApplyModelOutput(float[] output);
        protected abstract void FallbackBehavior();

        public void SetUseInference(bool use) => _useInference = use;
    }
}
