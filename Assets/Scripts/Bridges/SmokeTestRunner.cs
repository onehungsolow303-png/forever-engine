using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ForeverEngine.Tests
{
    /// <summary>
    /// Cross-module smoke test. Hits Asset Manager and Director Hub over plain
    /// System.Net.Http.HttpClient (NOT UnityWebRequest), because Unity batchmode
    /// has known reliability issues with UnityWebRequest coroutines outside of
    /// the player loop. The HttpClient path is fully synchronous from Unity's
    /// perspective and runs in the batchmode -executeMethod context cleanly.
    ///
    /// Run via:
    ///   Unity -batchmode -nographics -projectPath . \
    ///         -executeMethod ForeverEngine.Tests.SmokeTestRunner.Run -quit
    ///
    /// Exit code 0 = pass, 1 = fail.
    /// </summary>
    public static class SmokeTestRunner
    {
        public static void Run()
        {
            int exitCode = 1;
            try
            {
                // Task.Run offloads to the thread pool, escaping Unity's main-thread
                // SynchronizationContext. Without this, Execute().GetAwaiter().GetResult()
                // deadlocks because the awaited HttpClient call tries to resume on the
                // captured context which is busy waiting for the Result.
                exitCode = Task.Run(() => Execute()).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SmokeTest] FAIL: unhandled exception: {e.Message}\n{e.StackTrace}");
                exitCode = 1;
            }
            Debug.Log($"[SmokeTest] exit code: {exitCode}");
            EditorOrAppQuit(exitCode);
        }

        private static void EditorOrAppQuit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }

        private static async Task<int> Execute()
        {
            Debug.Log("[SmokeTest] starting");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            // 1. Health probes
            if (!await GetOk(http, "http://127.0.0.1:7801/health", "asset")) return 1;
            if (!await GetOk(http, "http://127.0.0.1:7802/health", "director")) return 1;
            Debug.Log("[SmokeTest] services up");

            // 2. Asset Manager catalog
            if (!await GetOk(http, "http://127.0.0.1:7801/catalog", "catalog")) return 1;

            // 3. Asset Manager select (POST)
            var selJson = "{\"schema_version\":\"1.0.0\",\"kind\":\"sprite\",\"biome\":\"forest\",\"theme\":\"stone\",\"tags\":[\"wall\"],\"allow_ai_generation\":false}";
            if (!await PostOk(http, "http://127.0.0.1:7801/select", selJson, "select")) return 1;

            // 4. Director Hub session start
            var sessJson = "{\"schema_version\":\"1.0.0\",\"player_profile\":{\"name\":\"SmokeHero\"},\"map_meta\":{\"seed\":1}}";
            string sessionBody = await PostBody(http, "http://127.0.0.1:7802/session/start", sessJson, "session");
            if (sessionBody == null) return 1;
            // Crude session_id extraction without dragging Newtonsoft into a static path
            int sidStart = sessionBody.IndexOf("\"session_id\":\"") + "\"session_id\":\"".Length;
            int sidEnd = sessionBody.IndexOf('"', sidStart);
            string sessionId = (sidStart > 14 && sidEnd > sidStart) ? sessionBody.Substring(sidStart, sidEnd - sidStart) : "stub";
            Debug.Log($"[SmokeTest] session={sessionId}");

            // 5. Director Hub interpret_action
            var actJson = "{\"schema_version\":\"1.0.0\",\"session_id\":\"" + sessionId + "\",\"actor_id\":\"player\",\"player_input\":\"I attack the goblin\",\"actor_stats\":{\"hp\":20,\"max_hp\":20}}";
            if (!await PostOk(http, "http://127.0.0.1:7802/interpret_action", actJson, "interpret_action")) return 1;

            Debug.Log("[SmokeTest] PASS");
            return 0;
        }

        private static async Task<bool> GetOk(HttpClient http, string url, string label)
        {
            try
            {
                var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SmokeTest] {label} GET {url} -> {(int)resp.StatusCode}");
                    return false;
                }
                Debug.Log($"[SmokeTest] {label} GET ok");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SmokeTest] {label} GET error: {e.Message}");
                return false;
            }
        }

        private static async Task<bool> PostOk(HttpClient http, string url, string body, string label)
        {
            return await PostBody(http, url, body, label) != null;
        }

        private static async Task<string> PostBody(HttpClient http, string url, string body, string label)
        {
            try
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await http.PostAsync(url, content);
                string respBody = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SmokeTest] {label} POST {url} -> {(int)resp.StatusCode}: {respBody}");
                    return null;
                }
                Debug.Log($"[SmokeTest] {label} POST ok");
                return respBody;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SmokeTest] {label} POST error: {e.Message}");
                return null;
            }
        }
    }
}
