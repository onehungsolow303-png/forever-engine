using System.Collections.Generic;
using ForeverEngine.Generation.Utility;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    public static class OverworldGenerator
    {
        public static Dictionary<(int,int), HexTile> Generate(int width, int height, int seed)
        {
            PerlinNoise.Seed(seed);
            var tiles = new Dictionary<(int,int), HexTile>();

            for (int q = 0; q < width; q++)
            {
                for (int r = 0; r < height; r++)
                {
                    float elevation = PerlinNoise.Octave(q * 0.15f, r * 0.15f, 4);
                    float moisture = PerlinNoise.Octave(q * 0.15f + 50f, r * 0.15f + 50f, 3);

                    TileType type;
                    if (elevation < 0.25f) type = TileType.Water;
                    else if (elevation < 0.4f) type = TileType.Plains;
                    else if (elevation < 0.6f) type = moisture > 0.5f ? TileType.Forest : TileType.Plains;
                    else if (elevation < 0.75f) type = TileType.Forest;
                    else type = TileType.Mountain;

                    float ruinNoise = PerlinNoise.Sample(q * 0.3f + 200f, r * 0.3f + 200f);
                    if (ruinNoise > 0.75f && type != TileType.Water && type != TileType.Mountain)
                        type = TileType.Road;

                    tiles[(q, r)] = new HexTile
                    {
                        Q = q, R = r, Type = type,
                        Height = (int)(elevation * 3),
                        MovementCost = HexGrid.GetMovementCost(type),
                        DefenseBonus = HexGrid.GetDefenseBonus(type)
                    };
                }
            }

            foreach (var loc in LocationData.GetAll())
            {
                tiles[(loc.HexQ, loc.HexR)] = new HexTile
                {
                    Q = loc.HexQ, R = loc.HexR, Type = TileType.Road,
                    MovementCost = 0.5f, DefenseBonus = 0f
                };
            }

            for (int dq = -1; dq <= 1; dq++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    var key = (2 + dq, 2 + dr);
                    if (tiles.ContainsKey(key) && tiles[key].Type == TileType.Water)
                        tiles[key] = new HexTile { Q = key.Item1, R = key.Item2, Type = TileType.Plains, MovementCost = 1f };
                }

            return tiles;
        }
    }
}
