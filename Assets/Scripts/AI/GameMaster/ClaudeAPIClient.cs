using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Text;

namespace ForeverEngine.AI.GameMaster
{
    /// <summary>
    /// HTTP client for Anthropic Claude API.
    /// API key is read from the ANTHROPIC_API_KEY environment variable or
    /// set directly on the component via the Inspector.
    /// </summary>
    public class ClaudeAPIClient : UnityEngine.MonoBehaviour
    {
        [SerializeField] private string apiKey = "";
        [SerializeField] private string defaultModel = "claude-haiku-4-5-20251001";

        private const string API_URL     = "https://api.anthropic.com/v1/messages";
        private const string API_VERSION = "2023-06-01";

        public bool IsConfigured => !string.IsNullOrEmpty(apiKey);

        private void Awake()
        {
            if (string.IsNullOrEmpty(apiKey))
                apiKey = System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
        }

        // ── Core send ─────────────────────────────────────────────────────

        public async Task<string> SendMessageAsync(
            string systemPrompt, string userMessage,
            string model = null, int maxTokens = 1024, float temperature = 0.7f)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning("[ClaudeAPI] No API key configured. Returning empty.");
                return "";
            }

            model ??= defaultModel;

            var requestBody = new ClaudeRequest
            {
                model       = model,
                max_tokens  = maxTokens,
                temperature = temperature,
                system      = systemPrompt,
                messages    = new[] { new ClaudeMessage { role = "user", content = userMessage } }
            };

            string json    = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(API_URL, "POST");
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type",        "application/json");
            request.SetRequestHeader("x-api-key",           apiKey);
            request.SetRequestHeader("anthropic-version",   API_VERSION);

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ClaudeAPI] Error: {request.error} — {request.downloadHandler.text}");
                return "";
            }

            var response = JsonUtility.FromJson<ClaudeResponse>(request.downloadHandler.text);
            return response?.content?[0]?.text ?? "";
        }

        /// <summary>
        /// Sends a message and deserializes the JSON response into T.
        /// Strips markdown code fences if present.
        /// </summary>
        public async Task<T> SendJsonAsync<T>(
            string systemPrompt, string userMessage,
            string model = null, int maxTokens = 2048, float temperature = 0.3f) where T : class
        {
            string text = await SendMessageAsync(systemPrompt, userMessage, model, maxTokens, temperature);
            if (string.IsNullOrEmpty(text)) return null;

            // Strip markdown code fences
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                text = firstNewline >= 0 ? text.Substring(firstNewline + 1) : text.Substring(3);
            }
            if (text.EndsWith("```"))
                text = text.Substring(0, text.Length - 3);
            text = text.Trim();

            try
            {
                return JsonUtility.FromJson<T>(text);
            }
            catch
            {
                Debug.LogError($"[ClaudeAPI] JSON parse failed: {text.Substring(0, Mathf.Min(200, text.Length))}");
                return null;
            }
        }

        // ── Serialisation helper types ────────────────────────────────────

        [System.Serializable] private class ClaudeRequest
        {
            public string         model;
            public int            max_tokens;
            public float          temperature;
            public string         system;
            public ClaudeMessage[] messages;
        }

        [System.Serializable] private class ClaudeMessage  { public string role; public string content; }
        [System.Serializable] private class ClaudeResponse { public ContentBlock[] content; }
        [System.Serializable] private class ContentBlock   { public string text; }
    }
}
