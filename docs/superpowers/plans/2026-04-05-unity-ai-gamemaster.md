# Unity AI Game Master + Dialogue — Implementation Plan (Plan 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development

**Goal:** Add Claude API integration to the Forever Engine for live NPC dialogue, game state management (dual ledger), narrative arc review, and plot reaction handling.

**Architecture:** AIGameMaster orchestrates all Claude calls. GameLedgerManager maintains structured JSON. NarrativeJournal maintains prose. DialogueManager handles real-time NPC conversations via Haiku. All backed by a C# HTTP client for the Anthropic API.

**Tech Stack:** Unity 6, C#, UnityWebRequest for HTTP, JSON serialization

**Depends on:** Plan 4 (Unity RPG Systems)

**Project:** `C:\Dev\Forever engin\`

---

### Task 1: Claude API Client

Create `Assets/Scripts/AI/GameMaster/ClaudeAPIClient.cs`:
- HTTP client using UnityWebRequest
- `SendMessageAsync(systemPrompt, userMessage, model, maxTokens, temperature)` → returns string
- `SendJsonAsync(...)` → returns parsed JSON dict
- Model selection: "claude-haiku-4-5-20251001" for dialogue, "claude-sonnet-4-6" for planning
- API key loaded from environment or ScriptableObject config
- Rate limiting and retry logic

---

### Task 2: Game Ledger Manager

Create `Assets/Scripts/AI/GameMaster/GameLedgerManager.cs`:
- Maintains the structured JSON game ledger (player, world, npcs, factions, quests, combat_history, player_profile)
- `UpdatePlayer(CharacterSheetComponent)` — sync player data
- `UpdateNPCDisposition(npcId, delta)` — modify NPC relationship
- `UpdateFactionStanding(factionId, delta)` — modify faction rep
- `RecordCombat(encounterResult)` — update combat_history
- `GetLedgerSnapshot()` → compact JSON string for API calls
- Save/load to `game_state/{sessionId}/ledger.json`

---

### Task 3: Narrative Journal

Create `Assets/Scripts/AI/GameMaster/NarrativeJournal.cs`:
- Maintains prose journal entries
- `AddEntry(day, text)` — append new entry
- `GetRecentExcerpt(maxWords)` → condensed recent entries for API calls
- `Summarize()` — compress old entries when total exceeds 2000 words
- Save/load to `game_state/{sessionId}/journal.md`

---

### Task 4: Dialogue System (Rebuilt)

Create/replace `Assets/Scripts/MonoBehaviour/Dialogue/`:
- `DialogueManager.cs` — Manages conversation state, NPC context building
- `DialogueAPIBridge.cs` — Builds Claude payload from NPC personality + ledger + journal + player input, sends to Haiku, parses response (dialogue text, disposition change, plot flags)
- `DialogueUIController.cs` — Text input field + NPC response display with typewriter effect

Free text input — player types whatever they want, Claude responds in character.

---

### Task 5: Plot Reaction Handler

Create `Assets/Scripts/AI/GameMaster/PlotReactionHandler.cs`:
- Listens for major events (quest complete, boss defeat, faction change, new region)
- Sends event + ledger + journal to Claude (Sonnet)
- Processes response: journal entry, world state changes, content generation triggers
- Fires events for other systems to react to

---

### Task 6: Narrative Arc Reviewer

Create `Assets/Scripts/AI/GameMaster/NarrativeArcReviewer.cs`:
- Background coroutine running every ~10 minutes of play time
- Sends full ledger + journal to Claude (Sonnet)
- Receives: pacing assessment, next story beat, NPC adjustments, twist decisions
- Feeds drama guidance to existing AIDirector system

---

### Task 7: AI Game Master Orchestrator

Create `Assets/Scripts/AI/GameMaster/AIGameMaster.cs`:
- Singleton MonoBehaviour coordinating all Claude interactions
- `InitializeWorld(characterSheet)` → calls world seeder (Python) + initial generation
- `HandleDialogue(npcId, playerText)` → routes through DialogueManager
- `HandleMajorEvent(eventType, data)` → routes through PlotReactionHandler
- `TickNarrativeReview()` → periodic arc review
- Manages API call budget and rate limiting
