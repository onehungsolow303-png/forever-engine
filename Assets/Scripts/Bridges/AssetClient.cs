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
    /// </summary>
    public class AssetClient
    {
        public string BaseUrl { get; }

        public AssetClient(string baseUrl = "http://127.0.0.1:7801")
        {
            BaseUrl = baseUrl.TrimEnd('/');
        }

        public IEnumerator GetCatalog(Action<CatalogResponse> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get($"{BaseUrl}/catalog");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"GetCatalog failed: {req.error}");
                yield break;
            }
            try
            {
                var resp = JsonConvert.DeserializeObject<CatalogResponse>(req.downloadHandler.text);
                onSuccess?.Invoke(resp);
            }
            catch (Exception e)
            {
                onError?.Invoke($"GetCatalog parse error: {e.Message}");
            }
        }

        public IEnumerator Select(AssetSelectionRequestDto request, Action<AssetSelectionResponse> onSuccess, Action<string> onError)
        {
            var json = JsonConvert.SerializeObject(request);
            using var req = new UnityWebRequest($"{BaseUrl}/select", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Select failed: {req.error}");
                yield break;
            }
            try
            {
                var resp = JsonConvert.DeserializeObject<AssetSelectionResponse>(req.downloadHandler.text);
                onSuccess?.Invoke(resp);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Select parse error: {e.Message}");
            }
        }

        // Plain DTOs for the asset bridge - mirror asset_manager/bridge/schemas.py.
        // Named with Dto suffix to avoid colliding with the auto-generated
        // AssetSelectionRequest in SharedSchemaTypes.cs (which has the same name
        // because the JSON schema title is AssetSelectionRequest).

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
