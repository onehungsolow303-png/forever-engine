using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ForeverEngine.Bridges
{
    /// <summary>
    /// On boot, pings Asset Manager and Director Hub /health.
    /// Both must be reachable or the game halts with an overlay.
    /// </summary>
    public class ServiceWatchdog : UnityEngine.MonoBehaviour
    {
        public string AssetManagerUrl = "http://127.0.0.1:7801";
        public string DirectorHubUrl = "http://127.0.0.1:7802";
        public float TimeoutSeconds = 3f;

        public bool AssetManagerOk { get; private set; }
        public bool DirectorHubOk { get; private set; }
        public bool AllOk => AssetManagerOk && DirectorHubOk;
        public event Action<bool, bool> OnHealthChecked;

        public IEnumerator CheckAll()
        {
            yield return CheckOne(AssetManagerUrl, ok => AssetManagerOk = ok);
            yield return CheckOne(DirectorHubUrl, ok => DirectorHubOk = ok);
            OnHealthChecked?.Invoke(AssetManagerOk, DirectorHubOk);
        }

        private IEnumerator CheckOne(string baseUrl, Action<bool> setResult)
        {
            // Note: explicit DownloadHandlerBuffer + manual Dispose is required
            // because Unity batchmode can hit "Curl error 23: Failure writing
            // output to destination" when the download handler is left to its
            // default and the request is wrapped in `using var`. Constructing
            // the handler explicitly and disposing in finally fixes it.
            var req = new UnityWebRequest($"{baseUrl}/health", "GET");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.RoundToInt(TimeoutSeconds);
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode == 200;
            if (ok)
                Debug.Log($"[ServiceWatchdog] OK: {baseUrl}/health -> {req.downloadHandler.text}");
            else
                Debug.LogError($"[ServiceWatchdog] DOWN: {baseUrl}/health - {req.error}");
            setResult(ok);
            req.Dispose();
        }
    }
}
