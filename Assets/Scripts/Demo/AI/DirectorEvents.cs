using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.Bridges;
using ForeverEngine.AI.SelfHealing;

namespace ForeverEngine.Demo.AI
{
    /// <summary>
    /// Fire-and-forget bridge between in-engine gameplay events and the
    /// out-of-process Director Hub. Each call enqueues an /interpret_action
    /// request with a minimal payload describing the event; the response
    /// (a stub DecisionPayload until spec §14 follow-up #2 lands) is logged
    /// and otherwise ignored.
    ///
    /// Wrapped in a SystemMonitor FaultBoundary so repeated Director failures
    /// auto-disable the event stream rather than spamming the log. This
    /// deliberately exercises the previously-orphaned SelfHealing namespace.
    /// </summary>
    public static class DirectorEvents
    {
        private const string SystemName = "DirectorEvents";

        /// <summary>
        /// Send an event to Director Hub. Safe to call from any MonoBehaviour
        /// or static context. Silently no-ops if GameManager / Director are
        /// unavailable, or if the FaultBoundary has disabled the system.
        /// </summary>
        public static void Send(
            string playerInput,
            object actorStats = null,
            string targetId = null,
            object targetStats = null)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Director == null) return;

            var fb = SystemMonitor.Instance != null
                ? SystemMonitor.Instance.GetOrCreate(SystemName, maxRetries: 3)
                : null;
            if (fb != null && fb.IsDisabled) return;

            var req = new DirectorClient.ActionRequestDto
            {
                SessionId = gm.SessionId ?? "no-session",
                ActorId = "player",
                TargetId = targetId,
                PlayerInput = playerInput,
                ActorStats = actorStats ?? BuildActorStatsFromPlayer(gm),
                TargetStats = targetStats,
            };

            // Coroutines need a MonoBehaviour host. GameManager survives
            // scene loads via DontDestroyOnLoad and is the natural host.
            gm.StartCoroutine(gm.Director.InterpretAction(
                req,
                _ =>
                {
                    // Stub Director returns a deterministic-fallback DecisionPayload
                    // we don't currently apply. Just count it as a success so the
                    // FaultBoundary's failure counter resets.
                    fb?.TryExecute(() => { });
                },
                err =>
                {
                    Debug.LogWarning($"[DirectorEvents] '{playerInput}': {err}");
                    // Force a failure on the boundary so repeated errors trip the breaker.
                    fb?.TryExecute(() => throw new System.Exception(err));
                }
            ));
        }

        /// <summary>
        /// Like Send() but invokes onResponse with the narrative text from the
        /// Director's reply. Used by DialoguePanel for player-facing dialogue
        /// where the response needs to be displayed. Empty string passed on any
        /// failure so the caller can render a fallback.
        /// </summary>
        public static void SendDialogue(
            string text,
            string npcId,
            System.Action<string> onResponse)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Director == null) { onResponse?.Invoke(""); return; }

            var fb = SystemMonitor.Instance != null
                ? SystemMonitor.Instance.GetOrCreate(SystemName, maxRetries: 3)
                : null;
            if (fb != null && fb.IsDisabled) { onResponse?.Invoke(""); return; }

            var req = new DirectorClient.ActionRequestDto
            {
                SessionId = gm.SessionId ?? "no-session",
                ActorId = "player",
                TargetId = npcId,
                PlayerInput = text,
                ActorStats = BuildActorStatsFromPlayer(gm),
            };

            gm.StartCoroutine(gm.Director.InterpretAction(
                req,
                decision =>
                {
                    fb?.TryExecute(() => { });
                    onResponse?.Invoke(decision?.NarrativeText ?? "");
                },
                err =>
                {
                    Debug.LogWarning($"[DirectorEvents] dialogue '{text}': {err}");
                    fb?.TryExecute(() => throw new System.Exception(err));
                    onResponse?.Invoke("");
                }));
        }

        /// <summary>
        /// Builds the actor_stats payload that the JSON schema (action.schema.json)
        /// requires. The schema mandates {hp, max_hp} as integers; without them
        /// pydantic returns 422 Unprocessable Entity. Caller can override by passing
        /// their own stats object to Send().
        ///
        /// Returns a minimal-but-valid stats object even if the player isn't loaded
        /// yet (e.g. on the title screen) so the contract still holds.
        /// </summary>
        private static Dictionary<string, object> BuildActorStatsFromPlayer(GameManager gm)
        {
            var p = gm?.Player;
            if (p == null)
            {
                return new Dictionary<string, object>
                {
                    ["hp"] = 0,
                    ["max_hp"] = 1,
                };
            }
            return new Dictionary<string, object>
            {
                ["hp"] = p.HP,
                ["max_hp"] = p.MaxHP,
                ["attack"] = p.Strength,
                ["defense"] = p.AC,
                ["level"] = p.Level,
                ["gold"] = p.Gold,
            };
        }
    }
}
