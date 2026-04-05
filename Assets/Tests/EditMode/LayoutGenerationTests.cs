using NUnit.Framework;
using ForeverEngine.Generation.Data;
using ForeverEngine.Generation.Agents;

namespace ForeverEngine.Tests
{
    public class LayoutGenerationTests
    {
        [Test] public void TopologyBuilder_Linear_Connected() { var g = TopologyBuilder.Build(TopologyType.LinearWithBranches, 8, 42); Assert.IsTrue(g.IsConnected()); Assert.GreaterOrEqual(g.Nodes.Count, 8); }
        [Test] public void TopologyBuilder_Loop_Connected() { var g = TopologyBuilder.Build(TopologyType.LoopBased, 6, 42); Assert.IsTrue(g.IsConnected()); }
        [Test] public void TopologyBuilder_Hub_Connected() { var g = TopologyBuilder.Build(TopologyType.HubAndSpoke, 7, 42); Assert.IsTrue(g.IsConnected()); }
        [Test] public void RoomPlacer_NoOverlapWithBorder()
        {
            var g = TopologyBuilder.Build(TopologyType.LinearWithBranches, 5, 42);
            RoomPlacer.Place(g, 64, 64, 42);
            foreach (var n in g.Nodes) { Assert.GreaterOrEqual(n.X, 0); Assert.GreaterOrEqual(n.Y, 0); Assert.Less(n.X + n.W, 64); Assert.Less(n.Y + n.H, 64); }
        }
    }
}
