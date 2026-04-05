using System.Collections.Generic;

namespace ForeverEngine.Generation.Data
{
    public enum ConnectionType { Corridor, Door, Secret, Open }

    public class RoomNode
    {
        public int Id;
        public int X, Y, W, H;
        public string Purpose;
        public int Zone;
        public List<int> Neighbors = new();
    }

    public class RoomEdge
    {
        public int FromId, ToId;
        public ConnectionType Type;
    }

    public class RoomGraph
    {
        public List<RoomNode> Nodes = new();
        public List<RoomEdge> Edges = new();
        public TopologyType Topology;
        public int EntranceNodeId;

        public RoomNode GetNode(int id) => Nodes.Find(n => n.Id == id);

        public void AddNode(RoomNode node) => Nodes.Add(node);

        public void Connect(int fromId, int toId, ConnectionType type = ConnectionType.Corridor)
        {
            Edges.Add(new RoomEdge { FromId = fromId, ToId = toId, Type = type });
            GetNode(fromId)?.Neighbors.Add(toId);
            GetNode(toId)?.Neighbors.Add(fromId);
        }

        public bool IsConnected()
        {
            if (Nodes.Count == 0) return true;
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(Nodes[0].Id);
            visited.Add(Nodes[0].Id);
            while (queue.Count > 0)
            {
                var current = GetNode(queue.Dequeue());
                if (current == null) continue;
                foreach (var n in current.Neighbors)
                    if (visited.Add(n)) queue.Enqueue(n);
            }
            return visited.Count == Nodes.Count;
        }
    }
}
