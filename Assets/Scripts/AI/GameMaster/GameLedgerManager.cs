using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace ForeverEngine.AI.GameMaster
{
    // ── Ledger data classes ───────────────────────────────────────────────

    [System.Serializable]
    public class GameLedger
    {
        public string sessionId;
        public string worldSeed;
        public float  playTimeMinutes;

        public PlayerLedger       player        = new();
        public WorldLedger        world         = new();
        public QuestLedger        quests        = new();
        public CombatHistoryLedger combatHistory = new();
        public PlayerProfileLedger playerProfile = new();

        // Unity JsonUtility doesn't support Dictionary<,> serialisation —
        // we store them as parallel lists and rebuild at runtime.
        public List<string>           npcIds   = new();
        public List<NPCLedgerEntry>   npcs     = new();
        public List<string>           factionIds  = new();
        public List<FactionLedgerEntry> factions = new();
    }

    [System.Serializable] public class PlayerLedger
    {
        public string       name;
        public string       species;
        public string       className;
        public int          level;
        public int          xp;
        public int          currentHP;
        public int          maxHP;
        public List<string> inventorySummary = new();
    }

    [System.Serializable] public class WorldLedger
    {
        public string       currentLocation;
        public List<string> discoveredLocations = new();
        public string       currentRegion;
        public string       timeOfDay = "morning";
        public int          day       = 1;
    }

    [System.Serializable] public class NPCLedgerEntry
    {
        public int          disposition;
        public bool         met;
        public int          conversations;
        public List<string> knowsAbout = new();
        public bool         alive      = true;
        public string       location;
    }

    [System.Serializable] public class FactionLedgerEntry
    {
        public int    standing;
        public string rank = "neutral";
    }

    [System.Serializable] public class QuestLedger
    {
        public List<string> active    = new();
        public List<string> completed = new();
        public List<string> failed    = new();
    }

    [System.Serializable] public class CombatHistoryLedger
    {
        public int    encountersWon;
        public int    encountersFled;
        public int    deaths;
        public int    hardestCRDefeated;
        public string preferredTactics = "mixed";
    }

    [System.Serializable] public class PlayerProfileLedger
    {
        public string archetype         = "explorer";
        public string dialoguePreference = "neutral";
        public string moralTendency      = "neutral";
        public string riskTolerance      = "medium";
    }

    // ── Manager ───────────────────────────────────────────────────────────

    /// <summary>
    /// Maintains the authoritative game-state ledger used by all AI subsystems.
    /// Persists to a per-session JSON file in Application.persistentDataPath.
    /// </summary>
    public class GameLedgerManager : UnityEngine.MonoBehaviour
    {
        private GameLedger _ledger   = new();
        private string     _savePath;

        // Runtime lookup maps rebuilt from the parallel lists after load
        private Dictionary<string, NPCLedgerEntry>     _npcMap     = new();
        private Dictionary<string, FactionLedgerEntry> _factionMap = new();

        public GameLedger Ledger => _ledger;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public void Initialize(string sessionId)
        {
            _ledger.sessionId = sessionId;
            _savePath = Path.Combine(
                Application.persistentDataPath, "game_state", sessionId, "ledger.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_savePath));
        }

        // ── NPC helpers ───────────────────────────────────────────────────

        public void UpdateNPCDisposition(string npcId, int delta)
        {
            var entry = GetOrCreateNPC(npcId);
            entry.disposition = Mathf.Clamp(entry.disposition + delta, -100, 100);
        }

        public void RecordNPCConversation(string npcId)
        {
            var entry = GetOrCreateNPC(npcId);
            entry.met = true;
            entry.conversations++;
        }

        public void AddNPCKnowledge(string npcId, string fact)
        {
            var entry = GetOrCreateNPC(npcId);
            if (!entry.knowsAbout.Contains(fact))
                entry.knowsAbout.Add(fact);
        }

        public NPCLedgerEntry GetNPC(string npcId)
        {
            _npcMap.TryGetValue(npcId, out var e);
            return e;
        }

        // ── Faction helpers ───────────────────────────────────────────────

        public void UpdateFactionStanding(string factionId, int delta)
        {
            var entry = GetOrCreateFaction(factionId);
            entry.standing = Mathf.Clamp(entry.standing + delta, -100, 100);
        }

        // ── Combat helpers ────────────────────────────────────────────────

        public void RecordEncounterWon(int cr)
        {
            _ledger.combatHistory.encountersWon++;
            if (cr > _ledger.combatHistory.hardestCRDefeated)
                _ledger.combatHistory.hardestCRDefeated = cr;
        }

        // ── Serialisation ─────────────────────────────────────────────────

        /// <summary>Returns a compact JSON snapshot suitable for LLM context injection.</summary>
        public string GetLedgerSnapshot() => JsonUtility.ToJson(_ledger, true);

        public void Save()
        {
            SyncMapsToLists();
            File.WriteAllText(_savePath, JsonUtility.ToJson(_ledger, true));
        }

        public void Load()
        {
            if (!File.Exists(_savePath)) return;
            _ledger = JsonUtility.FromJson<GameLedger>(File.ReadAllText(_savePath));
            RebuildMapsFromLists();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private NPCLedgerEntry GetOrCreateNPC(string id)
        {
            if (!_npcMap.ContainsKey(id)) _npcMap[id] = new NPCLedgerEntry();
            return _npcMap[id];
        }

        private FactionLedgerEntry GetOrCreateFaction(string id)
        {
            if (!_factionMap.ContainsKey(id)) _factionMap[id] = new FactionLedgerEntry();
            return _factionMap[id];
        }

        private void SyncMapsToLists()
        {
            _ledger.npcIds.Clear(); _ledger.npcs.Clear();
            foreach (var kv in _npcMap) { _ledger.npcIds.Add(kv.Key); _ledger.npcs.Add(kv.Value); }

            _ledger.factionIds.Clear(); _ledger.factions.Clear();
            foreach (var kv in _factionMap) { _ledger.factionIds.Add(kv.Key); _ledger.factions.Add(kv.Value); }
        }

        private void RebuildMapsFromLists()
        {
            _npcMap.Clear();
            for (int i = 0; i < Mathf.Min(_ledger.npcIds.Count, _ledger.npcs.Count); i++)
                _npcMap[_ledger.npcIds[i]] = _ledger.npcs[i];

            _factionMap.Clear();
            for (int i = 0; i < Mathf.Min(_ledger.factionIds.Count, _ledger.factions.Count); i++)
                _factionMap[_ledger.factionIds[i]] = _ledger.factions[i];
        }
    }
}
