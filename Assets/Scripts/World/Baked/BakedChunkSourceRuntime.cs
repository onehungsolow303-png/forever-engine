using System.IO;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Unity-side wrapper around ForeverEngine.Core.World.Baked.BakedChunkSource.
    /// Looks for a baked world under StreamingAssets or a dev path. Returns null
    /// if no bake found; caller falls back to runtime noise.
    /// </summary>
    public static class BakedChunkSourceRuntime
    {
        private static BakedChunkSource _cached;
        private static bool _loadAttempted;

        public static BakedChunkSource Get()
        {
            if (_loadAttempted) return _cached;
            _loadAttempted = true;

            string[] candidates = {
                Path.Combine(Application.streamingAssetsPath, "baked", "planet", "layer_0"),
                "C:/Dev/.shared/baked/planet/layer_0",
            };

            foreach (var dir in candidates)
            {
                var indexPath = Path.Combine(dir, "index.json");
                if (!File.Exists(indexPath)) continue;
                try
                {
                    _cached = BakedChunkSource.Load(dir, layerId: 0);
                    Debug.Log($"[BakedChunkSourceRuntime] loaded layer 0 from {dir}");
                    return _cached;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BakedChunkSourceRuntime] load failed at {dir}: {ex.Message}");
                }
            }

            Debug.Log("[BakedChunkSourceRuntime] no bake found; runtime noise path will be used.");
            return null;
        }

        public static void _SetForTest(BakedChunkSource source)
        {
            _cached = source;
            _loadAttempted = true;
        }

        public static void _Reset()
        {
            _cached = null;
            _loadAttempted = false;
        }
    }
}
