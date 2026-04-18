using NUnit.Framework;
using UnityEngine;

namespace ForeverEngine.Tests.World
{
    /// <summary>
    /// Documents the Chebyshev-distance rule used by
    /// ChunkManager.ChunkNeedsCollider. The method is private; this test
    /// asserts the math the method wraps.
    /// </summary>
    public class ChunkColliderRadiusTests
    {
        [Test]
        public void ChebyshevDistance_MaxOfAxisDeltas()
        {
            // Player at chunk (5, 5). Chunk at (7, 5) is dx=2 → inside radius 2.
            Assert.AreEqual(2, Mathf.Max(Mathf.Abs(7 - 5), Mathf.Abs(5 - 5)));
            // Chunk at (8, 5) is dx=3 → outside.
            Assert.AreEqual(3, Mathf.Max(Mathf.Abs(8 - 5), Mathf.Abs(5 - 5)));
            // Diagonal: chunk at (7, 7) is dx=dz=2 → still inside radius 2.
            Assert.AreEqual(2, Mathf.Max(Mathf.Abs(7 - 5), Mathf.Abs(7 - 5)));
            // Negative coords: chunk at (-1, -1) with player at (0, 0) is dx=dz=1 → inside.
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(-1 - 0), Mathf.Abs(-1 - 0)));
        }
    }
}
