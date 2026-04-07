using UnityEngine;

namespace ForeverEngine.AI.SelfHealing
{
    public static class AssetFaultHandler
    {
        private static Texture2D _placeholderTexture;

        /// <summary>
        /// Generic Resources.Load wrapper that logs missing assets and
        /// surfaces them through SystemMonitor for the runtime fault graph.
        /// Returns null if the asset is missing — caller decides the fallback.
        /// </summary>
        public static T SafeLoad<T>(string path) where T : Object
        {
            var asset = Resources.Load<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[SelfHeal] Missing {typeof(T).Name}: {path}");
                SystemMonitor.Instance?.GetOrCreate("Resources.Load")?.TryExecute(() =>
                    throw new System.IO.FileNotFoundException(path));
            }
            return asset;
        }

        public static Texture2D SafeLoadTexture(string path)
        {
            var tex = SafeLoad<Texture2D>(path);
            return tex != null ? tex : GetPlaceholderTexture();
        }

        public static GameObject SafeLoadPrefab(string path)
        {
            var prefab = SafeLoad<GameObject>(path);
            return prefab != null ? prefab : GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        public static AudioClip SafeLoadAudio(string path)
        {
            // null is safe — AudioSource handles null clips
            return SafeLoad<AudioClip>(path);
        }

        private static Texture2D GetPlaceholderTexture()
        {
            if (_placeholderTexture != null) return _placeholderTexture;
            _placeholderTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _placeholderTexture.SetPixels(new[] { Color.magenta, Color.black, Color.black, Color.magenta });
            _placeholderTexture.filterMode = FilterMode.Point;
            _placeholderTexture.Apply();
            return _placeholderTexture;
        }
    }
}
