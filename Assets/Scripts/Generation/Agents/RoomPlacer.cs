using ForeverEngine.Generation.Data;

namespace ForeverEngine.Generation.Agents
{
    public static class RoomPlacer
    {
        public static void Place(RoomGraph graph, int mapWidth, int mapHeight, int seed)
        {
            var rng = new System.Random(seed);
            int roomCount = graph.Nodes.Count;
            if (roomCount == 0) return;

            // BSP-like placement: divide map into sectors, assign rooms
            int cols = (int)System.Math.Ceiling(System.Math.Sqrt(roomCount));
            int rows = (int)System.Math.Ceiling((float)roomCount / cols);
            int sectorW = mapWidth / cols;
            int sectorH = mapHeight / rows;

            int minRoomSize = System.Math.Max(3, sectorW / 4);
            int maxRoomSize = System.Math.Max(minRoomSize + 1, (int)(sectorW * 0.7f));

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                int col = i % cols;
                int row = i / cols;

                int w = rng.Next(minRoomSize, maxRoomSize + 1);
                int h = rng.Next(minRoomSize, maxRoomSize + 1);

                // Center in sector with jitter
                int sectorX = col * sectorW;
                int sectorY = row * sectorH;
                int jitterX = rng.Next(0, System.Math.Max(1, sectorW - w));
                int jitterY = rng.Next(0, System.Math.Max(1, sectorH - h));

                node.X = System.Math.Clamp(sectorX + jitterX, 1, mapWidth - w - 1);
                node.Y = System.Math.Clamp(sectorY + jitterY, 1, mapHeight - h - 1);
                node.W = w;
                node.H = h;
            }
        }

        public static void CarveRooms(RoomGraph graph, bool[] walkability, int mapWidth)
        {
            foreach (var node in graph.Nodes)
            {
                for (int y = node.Y; y < node.Y + node.H; y++)
                    for (int x = node.X; x < node.X + node.W; x++)
                    {
                        int idx = y * mapWidth + x;
                        if (idx >= 0 && idx < walkability.Length)
                            walkability[idx] = true;
                    }
            }
        }
    }
}
