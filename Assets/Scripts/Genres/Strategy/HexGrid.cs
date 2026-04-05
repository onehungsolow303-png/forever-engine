using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.Strategy
{
    public enum TileType { Plains, Forest, Mountain, Water, Road }

    [System.Serializable]
    public struct HexTile { public int Q, R; public TileType Type; public int Height; public float MovementCost; public float DefenseBonus; }

    public class HexGrid : UnityEngine.MonoBehaviour
    {
        public static HexGrid Instance { get; private set; }
        private Dictionary<(int, int), HexTile> _tiles = new();
        [SerializeField] private float _hexSize = 1f;

        private void Awake() => Instance = this;

        public void SetTile(int q, int r, HexTile tile) { tile.Q = q; tile.R = r; _tiles[(q, r)] = tile; }
        public HexTile? GetTile(int q, int r) => _tiles.TryGetValue((q, r), out var t) ? t : null;
        public bool HasTile(int q, int r) => _tiles.ContainsKey((q, r));

        public Vector3 HexToWorld(int q, int r)
        {
            float x = _hexSize * (3f / 2f * q);
            float z = _hexSize * (Mathf.Sqrt(3f) / 2f * q + Mathf.Sqrt(3f) * r);
            return new Vector3(x, 0, z);
        }

        public (int q, int r) WorldToHex(Vector3 pos)
        {
            float q = (2f / 3f * pos.x) / _hexSize;
            float r = (-1f / 3f * pos.x + Mathf.Sqrt(3f) / 3f * pos.z) / _hexSize;
            return (Mathf.RoundToInt(q), Mathf.RoundToInt(r));
        }

        public List<(int, int)> GetNeighbors(int q, int r) => new()
        { (q+1,r), (q-1,r), (q,r+1), (q,r-1), (q+1,r-1), (q-1,r+1) };

        public static int Distance(int q1, int r1, int q2, int r2) =>
            (Mathf.Abs(q1-q2) + Mathf.Abs(q1+r1-q2-r2) + Mathf.Abs(r1-r2)) / 2;

        public int TileCount => _tiles.Count;

        public static float GetMovementCost(TileType type) => type switch
        { TileType.Plains => 1f, TileType.Forest => 2f, TileType.Mountain => 3f, TileType.Water => 99f, TileType.Road => 0.5f, _ => 1f };

        public static float GetDefenseBonus(TileType type) => type switch
        { TileType.Plains => 0f, TileType.Forest => 0.25f, TileType.Mountain => 0.5f, _ => 0f };
    }
}
