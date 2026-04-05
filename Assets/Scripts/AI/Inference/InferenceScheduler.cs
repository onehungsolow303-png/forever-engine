using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.AI.Inference
{
    public class InferenceScheduler : MonoBehaviour
    {
        public static InferenceScheduler Instance { get; private set; }
        [SerializeField] private float _maxInferenceTimeMs = 2.0f;

        private SortedList<float, InferenceRequest> _queue = new(new DuplicateKeyComparer());

        private void Awake() => Instance = this;

        public void Enqueue(InferenceRequest request)
        {
            _queue.Add(-request.Priority, request); // Negative for descending
        }

        private void Update()
        {
            float elapsed = 0f;
            while (_queue.Count > 0 && elapsed < _maxInferenceTimeMs)
            {
                var request = _queue.Values[0];
                _queue.RemoveAt(0);
                float start = Time.realtimeSinceStartup * 1000f;
                request.Execute();
                elapsed += (Time.realtimeSinceStartup * 1000f) - start;
            }
        }

        private class DuplicateKeyComparer : IComparer<float>
        {
            public int Compare(float x, float y)
            {
                int result = x.CompareTo(y);
                return result == 0 ? 1 : result; // Allow duplicate keys
            }
        }
    }

    public class InferenceRequest
    {
        public float Priority;
        public float[] Input;
        public System.Action<float[]> Callback;

        public void Execute()
        {
            var engine = InferenceEngine.Instance;
            var output = engine != null && engine.IsAvailable ? engine.Infer(Input) : null;
            Callback?.Invoke(output);
        }
    }
}
