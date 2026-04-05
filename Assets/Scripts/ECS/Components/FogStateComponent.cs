using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Per-tile fog of war state — rewritten from pygame fog_of_war.py.
    /// The FogRaycastJob writes these; the renderer reads them.
    /// Stored as a singleton buffer on the map entity, not per-tile entities.
    /// </summary>
    public enum FogState : byte
    {
        Unexplored = 0,  // Never seen — render black
        Explored = 1,    // Previously seen — render dimmed
        Visible = 2      // Currently in LOS — render full
    }

    /// <summary>
    /// Tag component for entities that have fog-of-war vision (player, allies).
    /// </summary>
    public struct FogVisionComponent : IComponentData
    {
        public int SightRadius;  // Tiles (default 16, from pygame FOW_SIGHT_RADIUS)
    }
}
