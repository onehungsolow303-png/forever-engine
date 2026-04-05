using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace ForeverEngine.AssetGeneration
{
    [System.Serializable]
    public class AssetEntry { public string id; public string category; public string path; public string format; public int width; public int height; public string[] tags; }

    [System.Serializable]
    public class AssetManifest { public string version = "1.0.0"; public string generator = "ForeverEngine"; public string created_at; public List<AssetEntry> assets = new(); }

    public static class AssetManifestBuilder
    {
        public static AssetManifest Build(string assetRoot)
        {
            var manifest = new AssetManifest { created_at = System.DateTime.UtcNow.ToString("o") };
            if (!Directory.Exists(assetRoot)) return manifest;

            foreach (var file in Directory.GetFiles(assetRoot, "*.png", SearchOption.AllDirectories))
            {
                string relative = file.Replace(assetRoot, "").TrimStart('/', '\\');
                string id = Path.GetFileNameWithoutExtension(file);
                string category = relative.Contains("Sprite") ? "sprite" : relative.Contains("Tile") ? "tileset" : relative.Contains("UI") ? "ui" : "texture";
                manifest.assets.Add(new AssetEntry { id = id, category = category, path = relative, format = "png", tags = new[] { category } });
            }
            return manifest;
        }

        public static string ToJson(AssetManifest manifest) => JsonUtility.ToJson(manifest, true);

        public static void SaveManifest(AssetManifest manifest, string path)
        {
            File.WriteAllText(path, ToJson(manifest));
            Debug.Log($"[AssetManifest] Saved {manifest.assets.Count} assets to {path}");
        }
    }
}
