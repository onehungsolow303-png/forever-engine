using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace ForeverEngine.Bridges
{
    /// <summary>
    /// HTTP client for Director Hub (http://127.0.0.1:7802).
    /// On any failure the caller can fall back to deterministic mode using
    /// only Forever engine's RPG/ rules layer.
    /// </summary>
    public class DirectorClient
    {
        public string BaseUrl { get; }
        public int RetryCount { get; set; } = 3;

        public DirectorClient(string baseUrl = "http://127.0.0.1:7802")
        {
            BaseUrl = baseUrl.TrimEnd('/');
        }

        public IEnumerator StartSession(SessionStartRequestDto request, Action<SessionStartResponseDto> onSuccess, Action<string> onError)
        {
            yield return Post<SessionStartRequestDto, SessionStartResponseDto>("/session/start", request, onSuccess, onError);
        }

        public IEnumerator InterpretAction(ActionRequestDto request, Action<DecisionPayloadDto> onSuccess, Action<string> onError)
        {
            yield return Post<ActionRequestDto, DecisionPayloadDto>("/interpret_action", request, onSuccess, onError);
        }

        private IEnumerator Post<TReq, TResp>(string path, TReq request, Action<TResp> onSuccess, Action<string> onError)
        {
            var json = JsonConvert.SerializeObject(request);
            int attempt = 0;
            string lastError = null;

            while (attempt < RetryCount)
            {
                attempt++;
                using var req = new UnityWebRequest($"{BaseUrl}{path}", "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<TResp>(req.downloadHandler.text);
                        onSuccess?.Invoke(resp);
                        yield break;
                    }
                    catch (Exception e)
                    {
                        lastError = $"parse error on attempt {attempt}: {e.Message}";
                    }
                }
                else
                {
                    lastError = $"http error on attempt {attempt}: {req.error}";
                }

                yield return new WaitForSeconds(Mathf.Pow(2, attempt - 1) * 0.5f);
            }

            onError?.Invoke(lastError ?? "unknown error after all retries");
        }

        // DTOs that mirror director_hub/bridge/schemas.py.
        // Named with Dto suffix to avoid colliding with the auto-generated
        // POCOs in SharedSchemaTypes.cs (which carry the JSON schema titles
        // ActionRequest and DecisionPayload).

        [Serializable]
        public class SessionStartRequestDto
        {
            [JsonProperty("schema_version")] public string SchemaVersion = "1.0.0";
            [JsonProperty("player_profile")] public object PlayerProfile;
            [JsonProperty("map_meta")] public object MapMeta;
        }

        [Serializable]
        public class SessionStartResponseDto
        {
            [JsonProperty("schema_version")] public string SchemaVersion;
            [JsonProperty("session_id")] public string SessionId;
            [JsonProperty("opening")] public DecisionPayloadDto Opening;
        }

        [Serializable]
        public class ActionRequestDto
        {
            [JsonProperty("schema_version")] public string SchemaVersion = "1.0.0";
            [JsonProperty("session_id")] public string SessionId;
            [JsonProperty("actor_id")] public string ActorId;
            [JsonProperty("target_id")] public string TargetId;
            [JsonProperty("player_input")] public string PlayerInput;
            [JsonProperty("actor_stats")] public object ActorStats;
            [JsonProperty("target_stats")] public object TargetStats;
            [JsonProperty("scene_context")] public object SceneContext;
            [JsonProperty("recent_history")] public string[] RecentHistory;
        }

        [Serializable]
        public class DecisionPayloadDto
        {
            [JsonProperty("schema_version")] public string SchemaVersion;
            [JsonProperty("session_id")] public string SessionId;
            [JsonProperty("success")] public bool Success;
            // Numeric fields are nullable to defend against any optional
            // schema field arriving as null from an upstream serializer.
            // The session start fix on the server side (Director Hub
            // bridge/server.py session_start) populates these explicitly,
            // but a defensive client tolerates legacy or stub responses
            // that don't.
            [JsonProperty("scale")] public int? Scale;
            [JsonProperty("narrative_text")] public string NarrativeText;
            [JsonProperty("stat_effects")] public object[] StatEffects;
            [JsonProperty("fx_requests")] public object[] FxRequests;
            [JsonProperty("repetition_penalty")] public int? RepetitionPenalty;
            [JsonProperty("deterministic_fallback")] public bool? DeterministicFallback;
        }
    }
}
