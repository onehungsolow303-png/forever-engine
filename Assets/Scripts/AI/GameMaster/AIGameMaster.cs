using UnityEngine;
using System.Threading.Tasks;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.MonoBehaviour.ContentLoader;
using ForeverEngine.MonoBehaviour.Dialogue;

namespace ForeverEngine.AI.GameMaster
{
    /// <summary>
    /// Top-level singleton that wires together all AI subsystems:
    /// Claude API, game ledger, narrative journal, dialogue, plot reactions, and arc review.
    ///
    /// Place on a persistent scene object alongside ClaudeAPIClient, GameLedgerManager,
    /// NarrativeJournal, AIDialogueManager, PlotReactionHandler, and NarrativeArcReviewer.
    /// </summary>
    public class AIGameMaster : UnityEngine.MonoBehaviour
    {
        public static AIGameMaster Instance { get; private set; }

        [Header("Core Subsystems")]
        [SerializeField] private ClaudeAPIClient      claudeClient;
        [SerializeField] private GameLedgerManager    ledgerManager;
        [SerializeField] private NarrativeJournal     journal;

        [Header("Narrative Subsystems")]
        [SerializeField] private AIDialogueManager    dialogueManager;
        [SerializeField] private PlotReactionHandler  plotHandler;
        [SerializeField] private NarrativeArcReviewer arcReviewer;

        [Header("Content Generation")]
        [SerializeField] private AssetGeneratorBridge assetBridge;

        [Header("Config")]
        [SerializeField] private bool enableClaudeAI = true;

        // ── Accessors ─────────────────────────────────────────────────────

        public GameLedgerManager Ledger  => ledgerManager;
        public NarrativeJournal  Journal => journal;

        public bool IsAIEnabled =>
            enableClaudeAI && claudeClient != null && claudeClient.IsConfigured;

        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            ledgerManager?.Save();
            journal?.Save();
        }

        // ── Session management ────────────────────────────────────────────

        public void InitializeSession(string sessionId)
        {
            ledgerManager.Initialize(sessionId);
            journal.Initialize(sessionId);
            Debug.Log($"[GameMaster] Session initialised: {sessionId}");
        }

        // ── World bootstrap ───────────────────────────────────────────────

        public async Task InitializeWorld(CharacterData characterData)
        {
            // Mirror character info into the ledger
            ledgerManager.Ledger.player.name      = characterData.characterName;
            ledgerManager.Ledger.player.species   = characterData.species;
            ledgerManager.Ledger.player.className = characterData.className;
            ledgerManager.Ledger.player.level     = characterData.level;
            ledgerManager.Ledger.player.maxHP     = characterData.maxHP;
            ledgerManager.Ledger.player.currentHP = characterData.currentHP;

            Debug.Log("[GameMaster] Generating starting world...");

            // Kick off procedural world generation via the Python bridge
            if (assetBridge != null && assetBridge.IsConfigured)
            {
                var request = new GenerationRequest
                {
                    id               = "world_start",
                    type             = "village",
                    biome            = "forest",
                    size             = "standard",
                    partyLevel       = characterData.level,
                    partySize        = 1,
                    difficulty       = "medium",
                    narrativeContext = $"{characterData.characterName} begins their journey.",
                };
                await assetBridge.GenerateContentAsync(request);
            }

            journal.AddEntry(1,
                $"{characterData.characterName}, a {characterData.species} {characterData.className}, begins their journey.");

            ledgerManager.Save();
            journal.Save();
        }

        // ── NPC dialogue ──────────────────────────────────────────────────

        public void StartNPCDialogue(
            string npcId, string npcName, string personalitySeed,
            int disposition, string faction)
        {
            dialogueManager.StartConversation(npcId, npcName, personalitySeed, disposition, faction);
        }

        public void EndNPCDialogue() => dialogueManager.EndConversation();

        // ── World events ──────────────────────────────────────────────────

        public async Task HandleMajorEvent(string eventType, string description)
        {
            await plotHandler.HandleMajorEvent(eventType, description);
            ledgerManager.Save();
            journal.Save();
        }

        public void RecordEncounterVictory(int cr, int xpAwarded)
        {
            ledgerManager.RecordEncounterWon(cr);
            int day = ledgerManager.Ledger.world.day;
            journal.AddEntry(day, $"Defeated a CR {cr} encounter. Earned {xpAwarded} XP.");
        }

        public void AdvanceDay()
        {
            ledgerManager.Ledger.world.day++;
            ledgerManager.Save();
        }

        // ── Narrative review (manual trigger) ────────────────────────────

        public async Task<NarrativeGuidance> RequestNarrativeReview()
        {
            await arcReviewer.ReviewNarrativeArc();
            return arcReviewer.LastGuidance;
        }
    }
}
