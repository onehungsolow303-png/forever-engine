using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ForeverEngine.MonoBehaviour.ContentLoader
{
    /// <summary>
    /// Bridge between Unity and the Python GM module (Map Generator project).
    /// Launches the python generator with a JSON request and reads back the response.
    ///
    /// Usage:
    ///   var bridge = GetComponent&lt;AssetGeneratorBridge&gt;();
    ///   string responseJson = await bridge.GenerateContentAsync(request);
    ///
    /// The Python script is expected to:
    ///   1. Read a request JSON from the path passed via --request argument.
    ///   2. Write a response JSON to stdout (or to a sibling file).
    ///
    /// Set generatorPath to the absolute path of gm_module/run_request.py.
    /// </summary>
    public class AssetGeneratorBridge : UnityEngine.MonoBehaviour
    {
        [Header("Python Bridge Settings")]
        [Tooltip("Python executable name or full path. E.g. 'python', 'python3', or absolute path.")]
        [SerializeField] private string pythonPath = "python";

        [Tooltip("Absolute path to the GM module entry script (run_request.py).")]
        [SerializeField] private string generatorPath = "";

        [Tooltip("Maximum seconds to wait for the Python process before timing out.")]
        [SerializeField] private int timeoutSeconds = 30;

        public bool IsConfigured => !string.IsNullOrEmpty(generatorPath) && File.Exists(generatorPath);

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Serializes <paramref name="request"/> to a temporary JSON file,
        /// launches the Python generator, and returns the stdout response.
        /// Returns null on error or timeout.
        /// </summary>
        public async Task<string> GenerateContentAsync(GenerationRequest request)
        {
            if (!IsConfigured)
            {
                UnityEngine.Debug.LogWarning("[AssetGeneratorBridge] generatorPath is not set or file not found.");
                return null;
            }

            string requestPath = Path.Combine(Application.temporaryCachePath, $"req_{request.id}.json");

            try
            {
                string requestJson = JsonUtility.ToJson(request, true);
                File.WriteAllText(requestPath, requestJson);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[AssetGeneratorBridge] Failed to write request JSON: {ex.Message}");
                return null;
            }

            var psi = new ProcessStartInfo
            {
                FileName  = pythonPath,
                Arguments = $"\"{generatorPath}\" --request \"{requestPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute  = false,
                CreateNoWindow   = true,
            };

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    UnityEngine.Debug.LogError("[AssetGeneratorBridge] Failed to start Python process.");
                    return null;
                }

                // Wait async with timeout
                var completionTask = Task.Run(() => process.WaitForExit());
                var timeoutTask    = Task.Delay(timeoutSeconds * 1000);

                if (await Task.WhenAny(completionTask, timeoutTask) == timeoutTask)
                {
                    process.Kill();
                    UnityEngine.Debug.LogWarning($"[AssetGeneratorBridge] Process timed out after {timeoutSeconds}s.");
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(errors))
                    UnityEngine.Debug.LogWarning($"[AssetGeneratorBridge] stderr: {errors}");

                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[AssetGeneratorBridge] Process exited with code {process.ExitCode}.");
                    return null;
                }

                return string.IsNullOrEmpty(output) ? null : output;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[AssetGeneratorBridge] Exception: {ex.Message}");
                return null;
            }
            finally
            {
                // Clean up temp request file
                if (File.Exists(requestPath))
                {
                    try { File.Delete(requestPath); } catch { /* ignore */ }
                }
            }
        }
    }

    // ── Request / Response types ──────────────────────────────────────────

    [System.Serializable]
    public class GenerationRequest
    {
        public string id              = "";
        public string type            = "dungeon";    // "dungeon", "encounter", "npc", "treasure"
        public string biome           = "generic";
        public string size            = "medium";     // "small", "medium", "large"
        public int    partyLevel      = 1;
        public int    partySize       = 4;
        public string difficulty      = "medium";     // "easy", "medium", "hard", "deadly"
        public string narrativeContext = "";           // free-text hint for the GM module
    }

    [System.Serializable]
    public class GenerationResponse
    {
        public bool   success      = false;
        public string requestId    = "";
        public string type         = "";
        public string dataPath     = "";  // path to generated map_data.json (for dungeon type)
        public string encounterJson = ""; // inline JSON for encounter type
        public string npcJson       = ""; // inline JSON for NPC type
        public string treasureJson  = ""; // inline JSON for treasure type
        public string errorMessage  = "";
    }
}
