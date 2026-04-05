using UnityEngine;
using System.Threading.Tasks;
using ForeverEngine.AI.GameMaster;

namespace ForeverEngine.MonoBehaviour.Dialogue
{
    /// <summary>
    /// AI-powered dialogue system that routes player speech through Claude.
    /// Works alongside the existing static-tree DialogueManager.
    /// Attach to the same GameObject as ClaudeAPIClient, GameLedgerManager,
    /// NarrativeJournal, and DialogueUIController.
    /// </summary>
    public class AIDialogueManager : UnityEngine.MonoBehaviour
    {
        [Header("AI Subsystems")]
        [SerializeField] private ClaudeAPIClient   claudeClient;
        [SerializeField] private GameLedgerManager ledgerManager;
        [SerializeField] private NarrativeJournal  journal;

        [Header("UI")]
        [SerializeField] private DialogueUIController uiController;

        // Current conversation state
        private string _currentNPCId;
        private string _currentNPCName;
        private string _currentNPCPersonality;

        public bool InConversation { get; private set; }

        // ── Public API ────────────────────────────────────────────────────

        public void StartConversation(
            string npcId, string npcName, string personalitySeed,
            int disposition, string faction)
        {
            _currentNPCId          = npcId;
            _currentNPCName        = npcName;
            _currentNPCPersonality = personalitySeed;
            InConversation         = true;

            ledgerManager.RecordNPCConversation(npcId);

            uiController.Show(npcName);
            uiController.OnPlayerSubmit += HandlePlayerInput;
        }

        public void EndConversation()
        {
            InConversation = false;
            _currentNPCId  = null;
            uiController.Hide();
            uiController.OnPlayerSubmit -= HandlePlayerInput;
        }

        // ── Input handling ────────────────────────────────────────────────

        private async void HandlePlayerInput(string playerText)
        {
            if (claudeClient == null || !claudeClient.IsConfigured)
            {
                uiController.ShowNPCResponse("I don't have much to say right now.");
                return;
            }

            uiController.ShowThinking();

            string systemPrompt = BuildDialoguePrompt();
            string userPayload  = BuildUserPayload(playerText);

            string response = await claudeClient.SendMessageAsync(
                systemPrompt, userPayload,
                model: "claude-haiku-4-5-20251001", maxTokens: 512, temperature: 0.8f);

            if (string.IsNullOrEmpty(response))
            {
                uiController.ShowNPCResponse("...");
                return;
            }

            // Try to parse structured JSON response first
            try
            {
                var dialogueResponse = JsonUtility.FromJson<DialogueResponse>(response);
                uiController.ShowNPCResponse(dialogueResponse.dialogue);

                if (dialogueResponse.disposition_change != 0)
                    ledgerManager.UpdateNPCDisposition(_currentNPCId, dialogueResponse.disposition_change);

                if (!string.IsNullOrEmpty(dialogueResponse.plot_flags) && dialogueResponse.plot_flags != "{}")
                    Debug.Log($"[AIDialogue] Plot flags: {dialogueResponse.plot_flags}");
            }
            catch
            {
                // Model returned plain prose — display it directly
                uiController.ShowNPCResponse(response);
            }
        }

        // ── Prompt builders ───────────────────────────────────────────────

        private string BuildDialoguePrompt()
        {
            return
                $"You are roleplaying as {_currentNPCName} in a fantasy RPG.\n\n" +
                $"Personality: {_currentNPCPersonality}\n\n" +
                "Stay in character. Respond naturally to the player.\n\n" +
                "Return JSON: {\"dialogue\": \"what you say\", " +
                "\"disposition_change\": 0, \"plot_flags\": {}, \"quest_update\": null}";
        }

        private string BuildUserPayload(string playerText)
        {
            string ledgerSnap    = ledgerManager.GetLedgerSnapshot();
            string journalExcerpt = journal.GetRecentExcerpt(300);
            return $"[Game State]\n{ledgerSnap}\n\n[Recent Events]\n{journalExcerpt}\n\n[Player says]: {playerText}";
        }

        // ── Response type ─────────────────────────────────────────────────

        [System.Serializable]
        private class DialogueResponse
        {
            public string dialogue;
            public int    disposition_change;
            public string plot_flags;
            public string quest_update;
        }
    }
}
