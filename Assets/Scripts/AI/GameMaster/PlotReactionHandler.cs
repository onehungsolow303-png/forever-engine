using UnityEngine;
using System.Threading.Tasks;

namespace ForeverEngine.AI.GameMaster
{
    /// <summary>
    /// Receives major game-world events, logs them, and (when Claude is available)
    /// generates narrative reactions and world-state adjustments.
    /// </summary>
    public class PlotReactionHandler : UnityEngine.MonoBehaviour
    {
        [SerializeField] private ClaudeAPIClient   claudeClient;
        [SerializeField] private GameLedgerManager ledgerManager;
        [SerializeField] private NarrativeJournal  journal;

        private const string PLOT_REACTION_PROMPT =
            "You are the narrative director for a living RPG world. A major event just occurred. " +
            "Analyse the impact on the world state and suggest consequences.\n\n" +
            "Return JSON:\n" +
            "{\"journal_entry\": \"narrative description\", " +
            "\"world_changes\": [{\"type\": \"faction_standing|npc_disposition|new_location|quest_update\", " +
            "\"target\": \"id\", \"value\": \"description\"}], " +
            "\"generate_content\": false, \"generation_hint\": \"\"}";

        // ── Public API ────────────────────────────────────────────────────

        public async Task HandleMajorEvent(string eventType, string eventDescription)
        {
            Debug.Log($"[Plot] Major event: {eventType} — {eventDescription}");

            int currentDay = ledgerManager.Ledger.world.day;
            journal.AddEntry(currentDay, $"[{eventType}] {eventDescription}");

            if (claudeClient == null || !claudeClient.IsConfigured) return;

            string payload =
                $"Event: {eventType}\nDescription: {eventDescription}\n\n" +
                $"Game State:\n{ledgerManager.GetLedgerSnapshot()}\n\n" +
                $"Recent Events:\n{journal.GetRecentExcerpt(500)}";

            string response = await claudeClient.SendMessageAsync(
                PLOT_REACTION_PROMPT, payload,
                model: "claude-sonnet-4-6", maxTokens: 1024, temperature: 0.5f);

            if (string.IsNullOrEmpty(response)) return;

            try
            {
                var result = JsonUtility.FromJson<PlotReactionResult>(response);
                if (!string.IsNullOrEmpty(result?.journal_entry))
                {
                    journal.AddEntry(currentDay, result.journal_entry);
                    Debug.Log($"[Plot] Reaction: {result.journal_entry.Substring(0, Mathf.Min(100, result.journal_entry.Length))}");
                }
            }
            catch
            {
                Debug.LogWarning($"[Plot] Could not parse reaction: {response.Substring(0, Mathf.Min(200, response.Length))}");
            }
        }

        // ── Response type ─────────────────────────────────────────────────

        [System.Serializable]
        private class PlotReactionResult
        {
            public string journal_entry;
            public bool   generate_content;
            public string generation_hint;
        }
    }
}
