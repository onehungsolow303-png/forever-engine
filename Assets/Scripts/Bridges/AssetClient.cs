using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace ForeverEngine.Bridges
{
    /// <summary>
    /// HTTP client for Asset Manager (http://127.0.0.1:7801).
    /// All calls are coroutine-based and tolerant of service unavailability -
    /// the engine continues with placeholder assets when Asset Manager is down.
    ///
    /// Retry logic matches DirectorClient: 3 attempts with exponential backoff
    /// (0.5s, 1s, 2s). Previously AssetClient was single-shot (no retries),
    /// which meant transient network hiccups caused permanent asset selection
    /// failures. Fixed in the audit remediation pass (2026-04-09).
    /// </summary>
    public class AssetClient
    {
        public string BaseUrl { get; }
        public int RetryCount { get; set; } = 3;

        public AssetClient(string baseUrl = "http://127.0.0.1:7801")
        {
            BaseUrl = baseUrl.TrimEnd('/');
        }

        public IEnumerator GetCatalog(Action<CatalogResponse> onSuccess, Action<string> onError)
        {
            yield return Get<CatalogResponse>("/catalog", onSuccess, onError);
        }

        public IEnumerator Select(AssetSelectionRequestDto request, Action<AssetSelectionResponse> onSuccess, Action<string> onError)
        {
            var json = JsonConvert.SerializeObject(request);
            yield return Post<AssetSelectionResponse>("/select", json, onSuccess, onError);
        }

        private IEnumerator Get<TResp>(string path, Action<TResp> onSuccess, Action<string> onError)
        {
            int attempt = 0;
            string lastError = null;

            while (attempt < RetryCount)
            {
                attempt++;
                using var req = UnityWebRequest.Get($"{BaseUrl}{path}");
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

        private IEnumerator Post<TResp>(string path, string json, Action<TResp> onSuccess, Action<string> onError)
        {
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

        // Plain DTOs for the asset bridge - mirror asset_manager/bridge/schemas.py.

        [Serializable]
        public class AssetSelectionRequestDto
        {
            [JsonProperty("schema_version")] public string SchemaVersion = "1.0.0";
            [JsonProperty("kind")] public string Kind;
            [JsonProperty("biome")] public string Biome;
            [JsonProperty("theme")] public string Theme;
            [JsonProperty("tags")] public string[] Tags;
            [JsonProperty("allow_ai_generation")] public bool AllowAiGeneration;
        }

        [Serializable]
        public class CatalogResponse
        {
            [JsonProperty("schema_version")] public string SchemaVersion;
            [JsonProperty("count")] public int Count;
            [JsonProperty("assets")] public object[] Assets;
        }

        [Serializable]
        public class AssetSelectionResponse
        {
            [JsonProperty("schema_version")] public string SchemaVersion;
            [JsonProperty("found")] public bool Found;
            [JsonProperty("asset_id")] public string AssetId;
            [JsonProperty("path")] public string Path;
            [JsonProperty("notes")] public string[] Notes;
        }
    }
}
