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

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _fogTexture.filterMode = FilterMode.Point;
            _fogPixels = new Color32[width * height];

            var unexplored = new Color32(0, 0, 0, 255);
            for (int i = 0; i < _fogPixels.Length; i++)
                _fogPixels[i] = unexplored;

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply();

            _fogSprite.sprite = Sprite.Create(
                _fogTexture,
                new Rect(0, 0, width, height),
                Vector2.zero,
                1f);
        }

        private void LateUpdate()
        {
            var store = MapDataStore.Instance;
            if (store == null || !store.FogGrid.IsCreated) return;

            var unexplored = new Color32(0, 0, 0, 255);
            var explored = new Color32(0, 0, 0, 153);
            var visible = new Color32(0, 0, 0, 0);

            for (int i = 0; i < store.FogGrid.Length && i < _fogPixels.Length; i++)
            {
                int x = i % _width;
                int y = i / _width;
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
