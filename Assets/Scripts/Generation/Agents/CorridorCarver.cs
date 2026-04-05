using ForeverEngine.Generation.Data;

namespace ForeverEngine.Generation.Agents
{
    public static class CorridorCarver
    {
        public static void Carve(RoomGraph graph, bool[] walkability, int mapWidth, int mapHeight)
        {
            foreach (var edge in graph.Edges)
            {
                var from = graph.GetNode(edge.FromId);
                var to = graph.GetNode(edge.ToId);
                if (from == null || to == null) continue;

                int fromCX = from.X + from.W / 2, fromCY = from.Y + from.H / 2;
                int toCX = to.X + to.W / 2, toCY = to.Y + to.H / 2;

                // L-shaped corridor: horizontal then vertical
                CarveHLine(walkability, fromCX, toCX, fromCY, mapWidth, mapHeight);
                CarveVLine(walkability, fromCY, toCY, toCX, mapWidth, mapHeight);
            }
        }

        private static void CarveHLine(bool[] walk, int x1, int x2, int y, int w, int h)
        {
            int minX = System.Math.Min(x1, x2), maxX = System.Math.Max(x1, x2);
            for (int x = minX; x <= maxX; x++)
            {
                SetWalkable(walk, x, y, w, h);
                SetWalkable(walk, x, y + 1, w, h); // 2-wide corridors
            }
        }

        private static void CarveVLine(bool[] walk, int y1, int y2, int x, int w, int h)
        {
            int minY = System.Math.Min(y1, y2), maxY = System.Math.Max(y1, y2);
            for (int y = minY; y <= maxY; y++)
            {
                SetWalkable(walk, x, y, w, h);
                SetWalkable(walk, x + 1, y, w, h); // 2-wide corridors
            }
        }

        private static void SetWalkable(bool[] walk, int x, int y, int w, int h)
        {
            if (x >= 0 && x < w && y >= 0 && y < h)
                walk[y * w + x] = true;
        }
    }
}
