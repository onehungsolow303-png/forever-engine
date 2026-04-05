using Unity.Entities;
using Unity.Mathematics;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Tile-based position on the map grid.
    /// Separate from Unity's Transform — this is the authoritative game position.
    /// The rendering layer interpolates Transform toward this for smooth movement.
    /// </summary>
    public struct PositionComponent : IComponentData
    {
        public int X;
        public int Y;
        public int Z;  // Z-level (floor). 0 = ground, negative = underground

        public int2 Tile => new int2(X, Y);
    }
}
