using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    public class OverworldPlayer : UnityEngine.MonoBehaviour
    {
        private PlayerData _data;
        private OverworldFog _fog;
        private Dictionary<(int,int), HexTile> _tiles;
        private System.Action<int,int> _onMoved;

        public int Q => _data.HexQ;
        public int R => _data.HexR;
        public int MovesTaken { get; private set; }

        public void Initialize(PlayerData data, OverworldFog fog, Dictionary<(int,int), HexTile> tiles, System.Action<int,int> onMoved)
        {
            _data = data; _fog = fog; _tiles = tiles; _onMoved = onMoved;
            _fog.Reveal(data.HexQ, data.HexR);
        }

        public bool TryMove(int dq, int dr)
        {
            int newQ = _data.HexQ + dq, newR = _data.HexR + dr;
            if (!_tiles.TryGetValue((newQ, newR), out var tile)) return false;
            if (tile.Type == TileType.Water) return false;

            _data.HexQ = newQ; _data.HexR = newR;
            _data.ExploredHexes.Add(_data.HexKey);
            _fog.Reveal(newQ, newR);

            // Survival drain per move
            _data.DrainHunger(1f);
            _data.DrainThirst(1.5f);

            // Starvation/dehydration damage
            if (_data.IsStarving) _data.TakeDamage(1);
            if (_data.IsDehydrated) _data.TakeDamage(2);

            MovesTaken++;
            _onMoved?.Invoke(newQ, newR);
            return true;
        }

        /// <summary>
        /// Set hex position from world coordinates (free movement mode).
        /// Triggers fog reveal and survival drains when hex changes.
        /// </summary>
        public void SetHex(int q, int r, OverworldFog fog)
        {
            _data.HexQ = q; _data.HexR = r;
            _data.ExploredHexes.Add(_data.HexKey);
            fog.Reveal(q, r);

            _data.DrainHunger(1f);
            _data.DrainThirst(1.5f);
            if (_data.IsStarving) _data.TakeDamage(1);
            if (_data.IsDehydrated) _data.TakeDamage(2);

            MovesTaken++;
        }

        public void Forage()
        {
            if (!_tiles.TryGetValue((_data.HexQ, _data.HexR), out var tile)) return;
            if (tile.Type == TileType.Forest && Random.Range(0f, 1f) < 0.3f)
            {
                _data.Eat(20f);
                Debug.Log("[Overworld] Foraged food!");
            }
        }
    }
}
