namespace ForeverEngine.Generation.Agents
{
    public static class CaveCarver
    {
        public static void Carve(bool[] walkability, int width, int height, int seed, int iterations = 4)
        {
            var rng = new System.Random(seed);
            var buffer = new bool[width * height];

            // Cellular automata: B678/S345678 (standard cave generation)
            for (int iter = 0; iter < iterations; iter++)
            {
                System.Array.Copy(walkability, buffer, walkability.Length);
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int neighbors = CountNeighbors(buffer, x, y, width, height);
                        int idx = y * width + x;
                        if (buffer[idx]) // Alive (walkable)
                            walkability[idx] = neighbors >= 3; // Survive with 3+ neighbors
                        else
                            walkability[idx] = neighbors >= 6; // Birth with 6+ neighbors
                    }
                }
            }

            // Ensure border is always walls
            for (int x = 0; x < width; x++) { walkability[x] = false; walkability[(height-1)*width+x] = false; }
            for (int y = 0; y < height; y++) { walkability[y*width] = false; walkability[y*width+width-1] = false; }

            // Flood fill to keep only largest connected region
            FloodFillLargest(walkability, width, height);
        }

        private static int CountNeighbors(bool[] grid, int cx, int cy, int w, int h)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && grid[ny * w + nx]) count++;
                }
            return count;
        }

        private static void FloodFillLargest(bool[] grid, int w, int h)
        {
            var labels = new int[w * h];
            int currentLabel = 0;
            var regionSizes = new System.Collections.Generic.Dictionary<int, int>();

            for (int i = 0; i < grid.Length; i++)
            {
                if (!grid[i] || labels[i] != 0) continue;
                currentLabel++;
                int size = FloodFill(grid, labels, i, currentLabel, w, h);
                regionSizes[currentLabel] = size;
            }

            if (regionSizes.Count == 0) return;
            int largestLabel = 0, largestSize = 0;
            foreach (var kv in regionSizes)
                if (kv.Value > largestSize) { largestLabel = kv.Key; largestSize = kv.Value; }

            for (int i = 0; i < grid.Length; i++)
                if (grid[i] && labels[i] != largestLabel) grid[i] = false;
        }

        private static int FloodFill(bool[] grid, int[] labels, int start, int label, int w, int h)
        {
            var queue = new System.Collections.Generic.Queue<int>();
            queue.Enqueue(start); labels[start] = label; int count = 0;
            while (queue.Count > 0)
            {
                int idx = queue.Dequeue(); count++;
                int x = idx % w, y = idx / w;
                int[] dx = {0,0,1,-1}; int[] dy = {1,-1,0,0};
                for (int d = 0; d < 4; d++)
                {
                    int nx = x+dx[d], ny = y+dy[d];
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                    {
                        int ni = ny*w+nx;
                        if (grid[ni] && labels[ni] == 0) { labels[ni] = label; queue.Enqueue(ni); }
                    }
                }
            }
            return count;
        }
    }
}
