using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForeverEngine.MonoBehaviour.ContentLoader
{
    /// <summary>
    /// Priority queue for content generation requests.
    /// Processes one request at a time to avoid overloading the Python bridge.
    /// Higher priority values are processed first.
    ///
    /// Priority guidelines:
    ///   10 = Critical  (next room the player is entering)
    ///    5 = Normal    (two rooms ahead, background generation)
    ///    1 = Low       (pre-generate distant content)
    /// </summary>
    public class ContentRequestQueue : UnityEngine.MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AssetGeneratorBridge bridge;
        [SerializeField] private HotContentLoader loader;

        [Header("Settings")]
        [Tooltip("Max time in seconds between queue polls when idle.")]
        [SerializeField] private float pollInterval = 1f;

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action<GenerationRequest, string> OnRequestCompleted;
        public event System.Action<GenerationRequest, string> OnRequestFailed;

        // ── Internal state ────────────────────────────────────────────────
        private readonly SortedList<int, Queue<GenerationRequest>> _queues = new();
        private bool _processing = false;
        private float _pollTimer = 0f;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Enqueues a generation request with the given priority (higher = sooner).</summary>
        public void Enqueue(GenerationRequest request, int priority = 5)
        {
            if (request == null) return;
            if (string.IsNullOrEmpty(request.id))
                request.id = System.Guid.NewGuid().ToString("N").Substring(0, 8);

            if (!_queues.TryGetValue(priority, out var queue))
            {
                queue = new Queue<GenerationRequest>();
                _queues[priority] = queue;
            }
            queue.Enqueue(request);

            Debug.Log($"[ContentRequestQueue] Enqueued request '{request.id}' (type={request.type}, priority={priority}). Queue depth: {TotalCount}");
        }

        /// <summary>Total number of pending requests across all priority levels.</summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (var q in _queues.Values) total += q.Count;
                return total;
            }
        }

        public bool IsProcessing => _processing;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Update()
        {
            if (_processing || TotalCount == 0) return;

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = pollInterval;

            ProcessNext();
        }

        // ── Internal ──────────────────────────────────────────────────────

        private async void ProcessNext()
        {
            var request = Dequeue();
            if (request == null) return;

            _processing = true;
            Debug.Log($"[ContentRequestQueue] Processing request '{request.id}' (type={request.type})");

            try
            {
                string result = await bridge.GenerateContentAsync(request);

                if (string.IsNullOrEmpty(result))
                {
                    Debug.LogWarning($"[ContentRequestQueue] Request '{request.id}' returned empty response.");
                    OnRequestFailed?.Invoke(request, "empty_response");
                }
                else
                {
                    // Hand off to HotContentLoader for ECS integration
                    loader?.HandleGenerationResponse(request, result);
                    OnRequestCompleted?.Invoke(request, result);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ContentRequestQueue] Request '{request.id}' threw: {ex.Message}");
                OnRequestFailed?.Invoke(request, ex.Message);
            }
            finally
            {
                _processing = false;
            }
        }

        private GenerationRequest Dequeue()
        {
            // Iterate descending (highest priority first)
            for (int i = _queues.Count - 1; i >= 0; i--)
            {
                var queue = _queues.Values[i];
                if (queue.Count > 0) return queue.Dequeue();
            }
            return null;
        }
    }
}
