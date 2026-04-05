using UnityEngine;

namespace ForeverEngine.AI.SelfHealing
{
    public static class AssetFaultHandler
    {
        private static Texture2D _placeholderTexture;
        private static Material _placeholderMaterial;

        public static Texture2D SafeLoadTexture(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return tex;
            Debug.LogWarning($"[SelfHeal] Missing texture: {path}");
            return GetPlaceholderTexture();
        }

        public static GameObject SafeLoadPrefab(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab != null) return prefab;
            Debug.LogWarning($"[SelfHeal] Missing prefab: {path}");
            return GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        public static AudioClip SafeLoadAudio(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"[SelfHeal] Missing audio: {path}");
            return clip; // null is safe — AudioSource handles null clips
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
