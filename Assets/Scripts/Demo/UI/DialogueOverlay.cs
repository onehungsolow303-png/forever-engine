using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ForeverEngine.MonoBehaviour.Dialogue;
using ForeverEngine.ECS.Systems;
using ForeverEngine.AI.GameMaster;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// OnGUI dialogue overlay for NPC conversations and quest acceptance.
    /// Supports free-text player input. Uses Claude API via ClaudeAPIClient when
    /// configured, otherwise falls back to static DialogueTree data (if registered)
    /// or built-in canned responses keyed on NPC role.
    ///
    /// Attach to a persistent GameObject in the Overworld scene.
    /// Called by OverworldManager.OnPlayerMoved when the player presses Enter on
    /// a location hex that has an NPC.
    /// </summary>
    public class DialogueOverlay : UnityEngine.MonoBehaviour
    {
        public static DialogueOverlay Instance { get; private set; }

        // ── State machine ─────────────────────────────────────────────────
        private enum ConvState { Closed, Greeting, Conversation, QuestOffer, Farewell }
        private ConvState _state = ConvState.Closed;

        // ── Current conversation ──────────────────────────────────────────
        private string _npcId;
        private string _npcName;
        private string _npcRole;          // "merchant" | "dwarf_chief" | "guard" | "priest" | "quest_giver"
        private string _treeKey;          // DialogueTree key registered in DialogueManager
        private string _questId;          // quest this NPC can offer
        private string _questObjId;       // objective id to check for completion

        // ── Dialogue content ──────────────────────────────────────────────
        private string _playerInputText = "";
        private Vector2 _scrollPos;
        private List<string> _conversationLog = new List<string>();

        // ── Quest flags ───────────────────────────────────────────────────
        private bool _questOffered;
        private bool _questAccepted;
        private bool _questComplete;
        private bool _awaitingAI;

        // ── Layout ────────────────────────────────────────────────────────
        private const float PanelW = 600f;
        private const float PanelH = 400f;
        private const float Pad    = 12f;

        // ── NPC location registry ─────────────────────────────────────────
        // Key = LocationData.Type; Value = (npcId, displayName, role, treeKey, questId, questObjId)
        private static readonly Dictionary<string, NPCEntry> s_LocationNPCs =
            new Dictionary<string, NPCEntry>
            {
                ["town"]     = new NPCEntry("merchant_vara",  "Merchant Vara",  "merchant",    "merchant",    "merchants_plea",    "clear_dungeon"),
                ["fortress"] = new NPCEntry("chief_borin",    "Chief Borin",    "dwarf_chief", "dwarf_chief", "dwarven_alliance",  "deliver_letter"),
                ["camp"]     = new NPCEntry("elder_mira",     "Elder Mira",     "quest_giver", "",            "signal_fire",       "reach_town"),
            };

        private struct NPCEntry
        {
            public string Id, Name, Role, TreeKey, QuestId, QuestObjId;
            public NPCEntry(string id, string name, string role, string treeKey, string questId, string questObjId)
            { Id = id; Name = name; Role = role; TreeKey = treeKey; QuestId = questId; QuestObjId = questObjId; }
        }

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnGUI()
        {
            if (_state == ConvState.Closed) return;

            // Dim background
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            float px = (Screen.width  - PanelW) * 0.5f;
            float py = (Screen.height - PanelH) * 0.5f;

            GUI.Box(new Rect(px, py, PanelW, PanelH), "");

            // ── NPC name ──────────────────────────────────────────────────
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            GUI.Label(new Rect(px + Pad, py + Pad, PanelW - Pad * 2, 22), _npcName, nameStyle);

            // ── Quest status badge ────────────────────────────────────────
            float badgeY = py + Pad + 22f;
            if (_questOffered && !_questAccepted)
            {
                DrawBadge(new Rect(px + Pad, badgeY, PanelW - Pad * 2, 16),
                    "[Quest Available]", new Color(0.4f, 1f, 0.4f));
            }
            else if (_questAccepted && !_questComplete)
            {
                DrawBadge(new Rect(px + Pad, badgeY, PanelW - Pad * 2, 16),
                    "[Quest Active]", new Color(0.4f, 0.8f, 1f));
            }
            else if (_questComplete)
            {
                DrawBadge(new Rect(px + Pad, badgeY, PanelW - Pad * 2, 16),
                    "[Quest Complete — speak to claim reward]", new Color(1f, 1f, 0.3f));
            }

            // ── Conversation log ──────────────────────────────────────────
            float logTop = py + 46f;
            float logH   = PanelH - 46f - 60f;
            var   logBox = new Rect(px + Pad, logTop, PanelW - Pad * 2, logH);
            GUI.Box(logBox, "");

            var logStyle  = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            string logTxt = string.Join("\n\n", _conversationLog);
            float  txtH   = logStyle.CalcHeight(new GUIContent(logTxt), logBox.width - 22f);
            var    view   = new Rect(0, 0, logBox.width - 20f, Mathf.Max(logH - 4f, txtH + 4f));

            _scrollPos = GUI.BeginScrollView(logBox, _scrollPos, view);
            GUI.Label(new Rect(3, 2, view.width, view.height), logTxt, logStyle);
            GUI.EndScrollView();

            // Thinking dots
            if (_awaitingAI)
            {
                var thinkStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 11,
                    fontStyle = FontStyle.Italic,
                    normal    = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                };
                GUI.Label(new Rect(px + Pad, logTop + logH + 2f, 80f, 18f), "...", thinkStyle);
            }

            // ── Input row ─────────────────────────────────────────────────
            float inputY = py + PanelH - 50f;
            GUI.SetNextControlName("DialogueInput");
            _playerInputText = GUI.TextField(
                new Rect(px + Pad, inputY, PanelW - 110f - Pad * 2f, 24f),
                _playerInputText);

            bool sendClicked = GUI.Button(
                new Rect(px + PanelW - 100f - Pad, inputY, 100f, 24f), "Send");

            if ((sendClicked) && !_awaitingAI)
                SubmitInput();

            // Leave button
            if (GUI.Button(new Rect(px + Pad, inputY + 28f, 80f, 20f), "Leave"))
                Close();

            // Hint
            var hintStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 10, normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };
            string hint = (_questOffered && !_questAccepted)
                ? "Type 'accept' to take the quest, or ask anything"
                : "Type freely and press Enter or Send";
            GUI.Label(new Rect(px + 95f, inputY + 30f, PanelW - 120f, 18f), hint, hintStyle);

            // Enter key
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Return &&
                GUI.GetNameOfFocusedControl() == "DialogueInput" &&
                !_awaitingAI)
            {
                SubmitInput();
                Event.current.Use();
            }

            // Keep focus on text field
            if (_state != ConvState.Closed)
                GUI.FocusControl("DialogueInput");
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Open the dialogue overlay for the NPC at a given location type.
        /// <paramref name="locationType"/> must match a key in LocationData (e.g. "town", "fortress").
        /// </summary>
        public void Show(string locationType)
        {
            if (!s_LocationNPCs.TryGetValue(locationType, out var entry))
            {
                Debug.Log($"[DialogueOverlay] No NPC defined for location type '{locationType}'");
                return;
            }

            _npcId        = entry.Id;
            _npcName      = entry.Name;
            _npcRole      = entry.Role;
            _treeKey      = entry.TreeKey;
            _questId      = entry.QuestId;
            _questObjId   = entry.QuestObjId;

            _playerInputText = "";
            _conversationLog.Clear();
            _scrollPos   = Vector2.zero;
            _awaitingAI  = false;

            _questComplete = IsQuestComplete(_questId);
            _questAccepted = _questComplete || IsQuestActive(_questId);
            _questOffered  = !string.IsNullOrEmpty(_questId) && !_questAccepted;

            // Build opening line: prefer static tree root, fall back to canned
            string greeting = TryGreetingFromTree() ?? BuildFallbackGreeting();
            _conversationLog.Add($"[{_npcName}]: {greeting}");

            _state = ConvState.Greeting;

            // Notify AIGameMaster (registers conversation in ledger, etc.)
            var gm = AIGameMaster.Instance;
            if (gm != null && gm.IsAIEnabled)
                gm.StartNPCDialogue(_npcId, _npcName, GetPersonalitySeed(), 50, GetFaction());

            Debug.Log($"[DialogueOverlay] Opened for {_npcName} ({locationType})");
        }

        /// <summary>True while the overlay is visible.</summary>
        public bool IsOpen => _state != ConvState.Closed;

        // ── Input ─────────────────────────────────────────────────────────

        private void SubmitInput()
        {
            string input = _playerInputText.Trim();
            if (string.IsNullOrEmpty(input)) return;

            _playerInputText = "";
            _conversationLog.Add($"[You]: {input}");
            ScrollToBottom();

            // Quest acceptance shortcut
            if (_questOffered && !_questAccepted && IsAcceptancePhrase(input))
            {
                AcceptQuest();
                return;
            }

            _state = ConvState.Conversation;

            // Try Claude API path
            var claudeClient = FindAnyObjectByType<ClaudeAPIClient>();
            if (claudeClient != null && claudeClient.IsConfigured)
            {
                _awaitingAI = true;
                StartCoroutine(SendToClaudeAsync(claudeClient, input));
            }
            else
            {
                AddNPCLine(BuildFallbackResponse(input));
            }
        }

        private IEnumerator SendToClaudeAsync(ClaudeAPIClient client, string playerInput)
        {
            var task = client.SendMessageAsync(
                BuildSystemPrompt(),
                $"[Player says]: {playerInput}",
                model: "claude-haiku-4-5-20251001",
                maxTokens: 256,
                temperature: 0.8f);

            yield return new WaitUntil(() => task.IsCompleted);
            _awaitingAI = false;

            if (task.Exception != null)
            {
                Debug.LogWarning($"[DialogueOverlay] Claude request failed: {task.Exception.Message}");
                AddNPCLine(BuildFallbackResponse(playerInput));
                yield break;
            }

            string raw = task.Result;
            if (string.IsNullOrEmpty(raw))
            {
                AddNPCLine(BuildFallbackResponse(playerInput));
                yield break;
            }

            // Attempt JSON parse for structured response
            try
            {
                var parsed = JsonUtility.FromJson<AIDialogueResponse>(raw);
                AddNPCLine(!string.IsNullOrEmpty(parsed?.dialogue) ? parsed.dialogue : raw);
            }
            catch
            {
                AddNPCLine(raw);
            }
        }

        private void AddNPCLine(string text)
        {
            _conversationLog.Add($"[{_npcName}]: {text}");
            ScrollToBottom();

            // Re-evaluate quest state
            _questComplete = IsQuestComplete(_questId);
            if (!_questComplete && _questOffered && !_questAccepted)
                _state = ConvState.QuestOffer;
        }

        private void ScrollToBottom() => _scrollPos = new Vector2(0, float.MaxValue);

        // ── Quest helpers ─────────────────────────────────────────────────

        private void AcceptQuest()
        {
            if (string.IsNullOrEmpty(_questId)) return;

            var qs = QuestSystem.Instance;
            if (qs == null) { Debug.LogWarning("[DialogueOverlay] QuestSystem.Instance is null"); return; }

            if (qs.GetQuest(_questId) == null)
                qs.StartQuest(_questId);

            _questAccepted = true;
            _questOffered  = false;
            _state         = ConvState.Conversation;

            AddNPCLine(BuildQuestAcceptMessage());
            Debug.Log($"[DialogueOverlay] Quest '{_questId}' accepted.");
        }

        private bool IsQuestActive(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            return QuestSystem.Instance?.GetQuest(questId) != null;
        }

        private bool IsQuestComplete(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            var qs = QuestSystem.Instance;
            if (qs == null) return false;
            foreach (var q in qs.GetCompletedQuests())
                if (q.QuestId == questId) return true;
            return false;
        }

        private static bool IsAcceptancePhrase(string input)
        {
            string l = input.ToLower();
            return l.Contains("accept") || l == "yes" || l.Contains("i'll do it") ||
                   l.Contains("i'll handle it") || l.Contains("i will") ||
                   l.Contains("sure") || l.Contains("agree") || l.Contains("ok") ||
                   l.Contains("take the quest") || l.Contains("help");
        }

        // ── Close ─────────────────────────────────────────────────────────

        private void Close()
        {
            _state = ConvState.Closed;
            _awaitingAI = false;
            StopAllCoroutines();

            var gm = AIGameMaster.Instance;
            if (gm != null && gm.IsAIEnabled)
                gm.EndNPCDialogue();

            Debug.Log($"[DialogueOverlay] Ended conversation with {_npcName}");
        }

        // ── Static tree greeting ──────────────────────────────────────────

        private string TryGreetingFromTree()
        {
            if (string.IsNullOrEmpty(_treeKey)) return null;
            var dm = DialogueManager.Instance;
            if (dm == null) return null;
            dm.StartDialogue(_treeKey);
            return dm.ActiveState?.CurrentNode?.Text;
        }

        // ── Fallback dialogue ─────────────────────────────────────────────

        private string BuildFallbackGreeting()
        {
            bool done = IsQuestComplete(_questId);
            return (_npcRole, done) switch
            {
                ("merchant",    true)  => "You've cleared The Hollow! As promised — here's your reward. My premium stock is now open to you.",
                ("merchant",    false) => "Welcome to what's left of Ashwick. I trade in supplies... if you have gold.",
                ("dwarf_chief", true)  => "You've done it. The Rot King is gone. Ironhold will remember your name.",
                ("dwarf_chief", false) => "Hmm. A surface wanderer. Ironhold doesn't see many visitors these days.",
                ("guard",       _)     => "The road ahead is dangerous. Watch yourself.",
                ("priest",      _)     => "May the light guide you, traveller.",
                ("quest_giver", true)  => "You've made contact with Ashwick. Well done. The camp breathes easier.",
                ("quest_giver", false) => "Survivor's Camp is holding on — barely. We need someone willing to venture out.",
                _                      => "Greetings, traveller."
            };
        }

        private string BuildFallbackResponse(string input)
        {
            string l    = input.ToLower();
            bool   done = IsQuestComplete(_questId);

            // Quest completion acknowledgement
            if (done && _questAccepted)
            {
                return _npcRole == "merchant"
                    ? "You cleared The Hollow — as promised, here's your gold and my premium stock is open to you."
                    : "You've done your part. The alliance holds. Safe travels.";
            }

            // Quest-related input
            if (l.Contains("quest") || l.Contains("work") || l.Contains("job") || l.Contains("task"))
            {
                if (!_questAccepted && _questOffered) return BuildQuestOfferText();
                if (_questAccepted)                   return BuildQuestReminderText();
            }

            // Lore
            if (l.Contains("rot") || l.Contains("curse") || l.Contains("king"))
                return _npcRole == "dwarf_chief"
                    ? "It started with the old king's obsession with immortality. He found something in the deep places — something that should have stayed buried. Now the whole kingdom rots."
                    : "The Rot came from the castle to the northeast. Undead, mutants... everything twisted. Best steer clear.";

            if (l.Contains("hollow") || l.Contains("dungeon"))
                return _npcRole == "merchant"
                    ? "The Hollow is to the southeast — an old mine. Something stirs down there. Clear it and I'll make it worth your while."
                    : "Dark place, that Hollow. Be well-armed before you go anywhere near it.";

            if (l.Contains("ironhold") || l.Contains("dwarf"))
                return "The dwarves sealed their gates early. Smart — the deep stone keeps the Rot out.";

            if (l.Contains("ashwick") || l.Contains("town") || l.Contains("ruins"))
                return "Most folk fled when the Rot spread. I stayed. Someone has to keep things running.";

            if (l.Contains("buy") || l.Contains("shop") || l.Contains("supplies") || l.Contains("trade"))
                return _npcRole == "merchant"
                    ? "My stock is limited but I have what you need. What are you after?"
                    : "I'm no merchant. Speak to Merchant Vara at Ashwick for supplies.";

            if (l.Contains("heal") || l.Contains("rest") || l.Contains("hurt"))
                return _npcRole == "priest"
                    ? "Come, let me see to those wounds. The light still has power in this dark land."
                    : _npcRole == "dwarf_chief"
                    ? "Aye, rest here. Ironhold is safe — the stone keeps the Rot at bay."
                    : "You look weary. Rest when you can.";

            if (l.Contains("goodbye") || l.Contains("farewell") || l.Contains("bye") || l.Contains("leave"))
            {
                _state = ConvState.Farewell;
                return _npcRole switch
                {
                    "merchant"    => "Safe travels. The roads are treacherous — keep your guard up.",
                    "dwarf_chief" => "The ancestors watch over you, surface walker.",
                    "priest"      => "May the light walk with you.",
                    _             => "Farewell. Watch yourself out there."
                };
            }

            // Role-based default
            return _npcRole switch
            {
                "merchant"    => "Is there something specific you need? Gold, supplies, information — I trade in all of it.",
                "dwarf_chief" => "Speak plainly, wanderer. We dwarves have little patience for riddles.",
                "guard"       => "Move along. The monsters won't wait for you to make up your mind.",
                "priest"      => "The faithful are few in these dark times. What weighs on your heart?",
                "quest_giver" => "Every day we hold this camp is a miracle. What do you need to know?",
                _             => "I'm not sure I follow. Ask me something else."
            };
        }

        private string BuildQuestOfferText()
        {
            return _npcRole switch
            {
                "merchant"
                    => "The Hollow — a dungeon to the southeast. Something stirs down there. Clear it out and I'll make it worth your while. Do you accept?",
                "dwarf_chief"
                    => "If you carry a letter from Vara at Ashwick, I can grant dwarven steel and an alliance. Have you such a letter?",
                "quest_giver"
                    => "Reach the ruins of Ashwick to the east and make contact with any survivors. Will you go?",
                _
                    => "There is something that needs doing. Are you willing?"
            };
        }

        private string BuildQuestReminderText()
        {
            return _npcRole switch
            {
                "merchant"
                    => "Still working on clearing The Hollow? Be careful — the dead don't stay dead in this kingdom.",
                "dwarf_chief"
                    => "Bring the letter when you can. Vara's words carry weight with my council.",
                _   => "You still have unfinished business. Be safe out there."
            };
        }

        private string BuildQuestAcceptMessage()
        {
            return _npcRole switch
            {
                "merchant"
                    => "Be careful down there. The dead don't stay dead in this kingdom. Return when The Hollow is clear.",
                "dwarf_chief"
                    => "Good. Bring the letter and we will speak of alliances. Ironhold's gates are open to you.",
                "quest_giver"
                    => "Thank you. Ashwick is half a day east. May fortune favour you.",
                _   => "I knew I could count on you. Return when it is done."
            };
        }

        // ── AI prompt builder ─────────────────────────────────────────────

        private string BuildSystemPrompt()
        {
            string worldCtx =
                "Setting: A fantasy kingdom corrupted by a supernatural Rot. Undead roam the land. " +
                "The source is the Rot King in the Throne of Rot castle (northeast). " +
                "Key locations: Survivor's Camp, Ashwick Ruins (town), The Hollow (dungeon), " +
                "Ironhold (dwarven fortress), Throne of Rot (final dungeon).";

            string questCtx = "";
            if (_questOffered && !_questAccepted) questCtx = $"You have a quest to offer: {BuildQuestOfferText()}";
            else if (_questAccepted && !_questComplete) questCtx = $"The player is currently working on your quest '{_questId}'.";
            else if (_questComplete) questCtx = $"The player has completed your quest. Acknowledge it warmly.";

            return $"You are {_npcName} in a dark fantasy RPG.\n\n" +
                   $"Personality: {GetPersonalitySeed()}\n\n" +
                   $"World: {worldCtx}\n\n" +
                   $"{questCtx}\n\n" +
                   "Stay in character. Be concise (1-3 sentences). Respond to what the player said.\n\n" +
                   "Return JSON: {\"dialogue\": \"your response\", \"disposition_change\": 0, \"plot_flags\": {}}";
        }

        private string GetPersonalitySeed()
        {
            return _npcRole switch
            {
                "merchant"    => "Pragmatic survivor. Trades in goods and information. Wary but fair. Has seen too much to be surprised.",
                "dwarf_chief" => "Stoic dwarven leader. Proud, direct, values honour and deeds over words. Respects earned strength.",
                "guard"       => "Hardened soldier. Blunt, vigilant, not unfriendly but always on edge.",
                "priest"      => "Compassionate healer. Keeps faith despite the darkness. Warm and thoughtful.",
                "quest_giver" => "Desperate but resourceful camp elder. Grateful for any help.",
                _             => "A survivor in a ruined land, trying to get through each day."
            };
        }

        private string GetFaction()
        {
            return _npcRole switch
            {
                "merchant"    => "Ashwick Survivors",
                "dwarf_chief" => "Ironhold Dwarves",
                "guard"       => "Town Watch",
                "priest"      => "The Faithful",
                _             => "Neutral"
            };
        }

        // ── Drawing helpers ───────────────────────────────────────────────

        private static void DrawBadge(Rect rect, string text, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
                { fontSize = 10, normal = { textColor = color } };
            GUI.Label(rect, text, style);
        }

        // ── AI response struct ────────────────────────────────────────────

        [System.Serializable]
        private class AIDialogueResponse
        {
            public string dialogue;
            public int    disposition_change;
            public string plot_flags;
        }
    }
}
