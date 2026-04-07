using UnityEngine;

namespace ForeverEngine.AI.Inference
{
    /// <summary>
    /// Abstract base for per-frame neural behaviors. Subclasses provide
    /// GetModelInput / ApplyModelOutput / FallbackBehavior.
    ///
    /// Inference goes through the InferenceScheduler when one is available
    /// (gives ms-budget batching across all IntelligentBehavior instances).
    /// Falls back to direct InferenceEngine.Infer if no scheduler exists.
    /// </summary>
    public abstract class IntelligentBehavior : UnityEngine.MonoBehaviour
    {
        protected InferenceEngine inferenceEngine;
        private bool _useInference = true;
        private bool _inferencePending;

        protected virtual void Start()
        {
            inferenceEngine = InferenceEngine.Instance;
        }

        protected void Update()
        {
            if (_useInference && inferenceEngine != null && inferenceEngine.IsAvailable)
            {
                var input = GetModelInput();

                // Prefer the scheduler so multiple agents share the per-frame
                // ms budget; fall back to direct call if no scheduler is wired.
                var scheduler = InferenceScheduler.Instance;
                if (scheduler != null && !_inferencePending)
                {
                    _inferencePending = true;
                    scheduler.Enqueue(new InferenceRequest
                    {
                        Priority = 1f,
                        Input = input,
                        Callback = output =>
                        {
                            _inferencePending = false;
                            if (output != null) ApplyModelOutput(output);
                            else FallbackBehavior();
                        },
                    });
                    return;
                }

                var directOutput = inferenceEngine.Infer(input);
                if (directOutput != null) { ApplyModelOutput(directOutput); return; }
            }
            FallbackBehavior();
        }

        protected abstract float[] GetModelInput();
        protected abstract void ApplyModelOutput(float[] output);
        protected abstract void FallbackBehavior();

        public void SetUseInference(bool use) => _useInference = use;
    }
}
