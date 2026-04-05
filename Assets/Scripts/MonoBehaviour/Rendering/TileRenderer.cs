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

        public void RenderLevel(int z)
        {
            var store = MapDataStore.Instance;
            if (store == null) return;

            var tex = TerrainTextureRegistry.Get(z);
            if (tex == null) return;

            _tilemap.ClearAllTiles();
            _currentZ = z;

            var pixels = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var color = pixels[(h - 1 - y) * w + x];
                    var pos = new Vector3Int(x, y, 0);

                    _tilemap.SetTile(pos, _baseTile);
                    _tilemap.SetTileFlags(pos, TileFlags.None);
                    _tilemap.SetColor(pos, color);
                }
            }
        }
    }
}
