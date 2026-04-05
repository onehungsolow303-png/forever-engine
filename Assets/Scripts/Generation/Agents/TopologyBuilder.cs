using System.Collections.Generic;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Generation.Agents
{
    public static class TopologyBuilder
    {
        public static RoomGraph Build(TopologyType type, int roomCount, int seed)
        {
            var rng = new System.Random(seed);
            return type switch
            {
                TopologyType.LinearWithBranches => BuildLinear(roomCount, rng),
                TopologyType.LoopBased => BuildLoop(roomCount, rng),
                TopologyType.HubAndSpoke => BuildHub(roomCount, rng),
                TopologyType.Hybrid => BuildHybrid(roomCount, rng),
                _ => BuildLinear(roomCount, rng)
            };
        }

        private static RoomGraph BuildLinear(int count, System.Random rng)
        {
            var graph = new RoomGraph { Topology = TopologyType.LinearWithBranches };
            for (int i = 0; i < count; i++) graph.AddNode(new RoomNode { Id = i });
            graph.EntranceNodeId = 0;
            for (int i = 0; i < count - 1; i++) graph.Connect(i, i + 1);
            // Add branches: ~30% of nodes get a side branch
            for (int i = 1; i < count - 1; i++)
            {
                if (rng.NextDouble() < 0.3)
                {
                    int branchId = graph.Nodes.Count;
                    graph.AddNode(new RoomNode { Id = branchId });
                    graph.Connect(i, branchId);
                }
            }
            return graph;
        }

        private static RoomGraph BuildLoop(int count, System.Random rng)
        {
            var graph = new RoomGraph { Topology = TopologyType.LoopBased };
            for (int i = 0; i < count; i++) graph.AddNode(new RoomNode { Id = i });
            graph.EntranceNodeId = 0;
            for (int i = 0; i < count - 1; i++) graph.Connect(i, i + 1);
            graph.Connect(count - 1, 0); // Close the loop
            // Add cross-connections for shortcuts
            if (count > 4) graph.Connect(0, count / 2);
            return graph;
        }

        private static RoomGraph BuildHub(int count, System.Random rng)
        {
            var graph = new RoomGraph { Topology = TopologyType.HubAndSpoke };
            graph.AddNode(new RoomNode { Id = 0 }); // Hub
            graph.EntranceNodeId = 1;
            for (int i = 1; i < count; i++)
            {
                graph.AddNode(new RoomNode { Id = i });
                graph.Connect(0, i); // All connect to hub
            }
            return graph;
        }

        private static RoomGraph BuildHybrid(int count, System.Random rng)
        {
            // Start linear, add a hub in the middle, close partial loops
            var graph = BuildLinear(count, rng);
            graph.Topology = TopologyType.Hybrid;
            if (count > 5)
            {
                int mid = count / 2;
                // Connect first and last to mid to create alternative paths
                if (!graph.GetNode(0).Neighbors.Contains(mid)) graph.Connect(0, mid);
                if (!graph.GetNode(count-1).Neighbors.Contains(mid)) graph.Connect(count - 1, mid);
            }
            return graph;
        }
    }
}
