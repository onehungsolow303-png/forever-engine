using UnityEngine;
using System.IO;

namespace ForeverEngine.Shared
{
    /// <summary>
    /// Validates data files against shared JSON schemas at C:\Dev\.shared\schemas\.
    /// Ensures Map Generator output and Image Generator assets are compatible
    /// with Forever Engine before import.
    ///
    /// This is the cross-project contract enforcer.
    /// </summary>
    public static class SchemaValidator
    {
        private static string _schemaRoot = "C:\\Dev\\.shared\\schemas";

        /// <summary>
        /// Validates a map_data.json against the shared map schema.
        /// Returns true if valid, logs warnings for issues.
        /// </summary>
        public static bool ValidateMapData(string jsonContent)
        {
            // Basic structural validation (full JSON Schema validation
            // would require a library like Newtonsoft.Json.Schema)
            try
            {
                var data = JsonUtility.FromJson<MapDataCheck>(jsonContent);

                if (data.config == null)
                {
                    Debug.LogError("[SchemaValidator] Map data missing 'config' section");
                    return false;
                }

                if (data.config.width <= 0 || data.config.height <= 0)
                {
                    Debug.LogError("[SchemaValidator] Invalid map dimensions");
                    return false;
                }

                if (data.z_levels == null || data.z_levels.Length == 0)
                {
                    Debug.LogError("[SchemaValidator] Map data has no z_levels");
                    return false;
                }

                // Check schema version compatibility
                string expectedVersion = GetSchemaVersion("map_schema.json");
                if (!string.IsNullOrEmpty(data.config.schema_version) &&
                    data.config.schema_version != expectedVersion)
                {
                    Debug.Log(
                        $"[SchemaValidator] Map schema version mismatch: " +
                        $"expected {expectedVersion}, got {data.config.schema_version}. " +
                        $"Map Generator may need updating.");
                }

                Debug.Log("[SchemaValidator] Map data validated successfully");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SchemaValidator] Failed to parse map data: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates an asset_manifest.json against the shared asset schema.
        /// </summary>
        public static bool ValidateAssetManifest(string jsonContent)
        {
            try
            {
                var data = JsonUtility.FromJson<AssetManifestCheck>(jsonContent);

                if (string.IsNullOrEmpty(data.version))
                {
                    Debug.LogError("[SchemaValidator] Asset manifest missing version");
                    return false;
                }

                if (data.assets == null || data.assets.Length == 0)
                {
                    Debug.LogWarning("[SchemaValidator] Asset manifest has no assets");
                }

                Debug.Log("[SchemaValidator] Asset manifest validated successfully");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SchemaValidator] Failed to parse asset manifest: {e.Message}");
                return false;
            }
        }

        private static string GetSchemaVersion(string schemaFileName)
        {
            string path = Path.Combine(_schemaRoot, schemaFileName);
            if (!File.Exists(path)) return "1.0.0";

            // Quick parse for version field
            string content = File.ReadAllText(path);
            var match = System.Text.RegularExpressions.Regex.Match(
                content, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : "1.0.0";
        }

        // Minimal check structures (just enough to validate structure)
        [System.Serializable]
        private class MapDataCheck
        {
            public ConfigCheck config;
            public ZLevelCheck[] z_levels;
        }

        [System.Serializable]
        private class ConfigCheck
        {
            public int width, height;
            public string schema_version;
        }

        [System.Serializable]
        private class ZLevelCheck
        {
            public int z;
        }

        [System.Serializable]
        private class AssetManifestCheck
        {
            public string version;
            public AssetCheck[] assets;
        }

        [System.Serializable]
        private class AssetCheck
        {
            public string id, category, path;
        }
    }
}
