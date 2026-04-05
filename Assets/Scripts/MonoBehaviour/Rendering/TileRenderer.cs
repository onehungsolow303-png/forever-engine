using UnityEngine;
using UnityEngine.Tilemaps;
using ForeverEngine.MonoBehaviour.Bootstrap;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.Rendering
{
    [RequireComponent(typeof(Tilemap))]
    public class TileRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private Tile _baseTile;

        private int _currentZ;

        private Tile GetOrCreateBaseTile()
        {
            if (_baseTile != null) return _baseTile;

            // Create a white 1x1 pixel sprite to use as base tile
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            _baseTile = ScriptableObject.CreateInstance<Tile>();
            _baseTile.sprite = sprite;
            _baseTile.color = Color.white;

            return _baseTile;
        }

        public void RenderLevel(int z)
        {
            if (_tilemap == null)
                _tilemap = GetComponent<Tilemap>();

            var store = MapDataStore.Instance;
            if (store == null) return;

            _tilemap.ClearAllTiles();
            _currentZ = z;

            var tile = GetOrCreateBaseTile();
            int w = store.Width;
            int h = store.Height;

            // Try to use terrain PNG for colors
            var tex = TerrainTextureRegistry.Get(z);

            if (tex != null)
            {
                var pixels = tex.GetPixels32();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // PNG is top-down, tilemap is bottom-up
                        int srcIdx = y * tex.width + x;
                        if (srcIdx >= pixels.Length) continue;

                        var pos = new Vector3Int(x, h - 1 - y, 0);
                        _tilemap.SetTile(pos, tile);
                        _tilemap.SetTileFlags(pos, TileFlags.None);
                        _tilemap.SetColor(pos, pixels[srcIdx]);
                    }
                }
            }
            else
            {
                // No terrain PNG — generate from walkability
                var wallColor = new Color32(40, 40, 50, 255);
                var floorColor = new Color32(120, 100, 80, 255);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        var pos = new Vector3Int(x, h - 1 - y, 0);
                        _tilemap.SetTile(pos, tile);
                        _tilemap.SetTileFlags(pos, TileFlags.None);
                        bool walkable = idx < store.Walkability.Length && store.Walkability[idx];
                        _tilemap.SetColor(pos, walkable ? (Color)floorColor : (Color)wallColor);
                    }
                }
            }

            Debug.Log($"[TileRenderer] Rendered {w}x{h} tiles for z={z}");
        }
    }
}
