using UnityEngine;
using System.Threading.Tasks;

namespace ForeverEngine.AI.GameMaster
{
    // ── Public data type shared with other systems ─────────────────────────

    [System.Serializable]
    public class NarrativeGuidance
    {
        public int    currentAct;
        public string pacing;
        public string nextBeat;
        public string suggestedLocation;
        public bool   twistReady;
        public string twistDescription;
    }

    // ── Reviewer ──────────────────────────────────────────────────────────

    /// <summary>
    /// Periodically asks Claude to review the narrative arc and return pacing guidance.
    /// Falls back to a static "rising" guidance when Claude is not configured.
    /// </summary>
    public class NarrativeArcReviewer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private ClaudeAPIClient   claudeClient;
        [SerializeField] private GameLedgerManager ledgerManager;
        [SerializeField] private NarrativeJournal  journal;
        [SerializeField] private float reviewIntervalMinutes = 10f;

        private const string ARC_REVIEW_PROMPT =
            "You are the narrative director for a living RPG world. " +
            "Review the current game state and guide the story direction.\n\n" +
            "Return JSON:\n" +
            "{\"current_act\": 1, \"pacing\": \"rising\", \"next_beat\": \"description\", " +
            "\"suggested_location\": \"map_type\", \"twist_ready\": false, " +
            "\"twist_description\": null, \"npc_adjustments\": []}";

        private float            _timeSinceLastReview;
        private NarrativeGuidance _lastGuidance;

        public NarrativeGuidance LastGuidance => _lastGuidance;

        // ── Unity ─────────────────────────────────────────────────────────

        private void Update()
        {
            _timeSinceLastReview += Time.deltaTime;
            if (_timeSinceLastReview >= reviewIntervalMinutes * 60f)
            {
                _timeSinceLastReview = 0f;
                _ = ReviewNarrativeArc();
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        public async Task ReviewNarrativeArc()
        {
            if (claudeClient == null || !claudeClient.IsConfigured)
            {
                _lastGuidance = new NarrativeGuidance
                {
                    currentAct  = 1,
                    pacing      = "rising",
                    nextBeat    = "Continue exploring"
                };
                return;
            }

            string payload =
                $"Game State:\n{ledgerManager.GetLedgerSnapshot()}\n\n" +
                $"Narrative Journal:\n{journal.GetRecentExcerpt(800)}";

            string response = await claudeClient.SendMessageAsync(
                ARC_REVIEW_PROMPT, payload,
                model: "claude-sonnet-4-6", maxTokens: 1024, temperature: 0.5f);

            if (string.IsNullOrEmpty(response)) return;

            try
            {
                var result = JsonUtility.FromJson<ArcReviewResult>(response);
                _lastGuidance = new NarrativeGuidance
                {
                    currentAct       = result.current_act,
                    pacing           = result.pacing,
                    nextBeat         = result.next_beat,
                    suggestedLocation = result.suggested_location,
                    twistReady       = result.twist_ready,
                    twistDescription = result.twist_description,
                };
                Debug.Log($"[Narrative] Act {_lastGuidance.currentAct}, Pacing: {_lastGuidance.pacing}, Next: {_lastGuidance.nextBeat}");
            }
            catch
            {
                Debug.LogWarning("[Narrative] Could not parse arc review response.");
            }
        }

        // ── Response type ─────────────────────────────────────────────────

        [System.Serializable]
        private class ArcReviewResult
        {
            public int    current_act;
            public string pacing;
            public string next_beat;
            public string suggested_location;
            public bool   twist_ready;
            public string twist_description;
        }
    }
}
