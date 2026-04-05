namespace ForeverEngine.Demo.Battle
{
    public class BattleGrid
    {
        public int Width { get; }
        public int Height { get; }
        public bool[] Walkable { get; }

        public BattleGrid(int width, int height, int seed)
        {
            Width = width; Height = height;
            Walkable = new bool[width * height];
            var rng = new System.Random(seed);

            // All walkable except border and scattered obstacles
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool border = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                    bool obstacle = !border && rng.NextDouble() < 0.1;
                    Walkable[y * width + x] = !border && !obstacle;
                }

            // Ensure player start (1,1) and a clear path
            Walkable[1 * width + 1] = true;
            Walkable[1 * width + 2] = true;
            Walkable[2 * width + 1] = true;
        }

        public bool IsWalkable(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height && Walkable[y * Width + x];
    }
}
