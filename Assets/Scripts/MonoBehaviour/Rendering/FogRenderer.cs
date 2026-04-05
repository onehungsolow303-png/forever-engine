using UnityEngine;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.Rendering
{
    public class FogRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _fogSprite;
        [SerializeField] private GameConfig _config;

        private Texture2D _fogTexture;
        private Color32[] _fogPixels;
        private int _width, _height;
        private bool _enabled = true;

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _fogTexture.filterMode = FilterMode.Point;
            _fogPixels = new Color32[width * height];

            // Start with everything unexplored (black)
            var unexplored = new Color32(0, 0, 0, 255);
            for (int i = 0; i < _fogPixels.Length; i++)
                _fogPixels[i] = unexplored;

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply();

            // Create sprite if we don't have one
            if (_fogSprite == null)
                _fogSprite = GetComponent<SpriteRenderer>();

            if (_fogSprite == null)
            {
                _fogSprite = gameObject.AddComponent<SpriteRenderer>();
                _fogSprite.sortingOrder = 10;
            }

            // Pivot at bottom-left so it aligns with tilemap origin
            _fogSprite.sprite = Sprite.Create(
                _fogTexture,
                new Rect(0, 0, width, height),
                Vector2.zero,
                1f); // 1 pixel per unit = same scale as tilemap

            Debug.Log($"[FogRenderer] Initialized {width}x{height} fog overlay");
        }

        public void Toggle()
        {
            _enabled = !_enabled;
            if (_fogSprite != null)
                _fogSprite.enabled = _enabled;
        }

        private void LateUpdate()
        {
            if (!_enabled) return;

            var store = MapDataStore.Instance;
            if (store == null || !store.FogGrid.IsCreated) return;

            var unexplored = new Color32(0, 0, 0, 255);
            var explored = new Color32(0, 0, 0, 153);
            var visible = new Color32(0, 0, 0, 0);

            for (int i = 0; i < store.FogGrid.Length && i < _fogPixels.Length; i++)
            {
                int x = i % _width;
                int y = i / _width;
                // Texture Y is flipped (bottom-up) relative to map grid (top-down)
                int texIdx = (_height - 1 - y) * _width + x;
                if (texIdx < 0 || texIdx >= _fogPixels.Length) continue;

                _fogPixels[texIdx] = store.FogGrid[i] switch
                {
                    2 => visible,
                    1 => explored,
                    _ => unexplored
                };
            }

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply();
        }
    }
}
