using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// One-frame request component — added to a player entity to trigger a rest.
    /// RestSystem reads this, applies healing / resource recovery, then removes it.
    /// </summary>
    public struct RestRequestComponent : IComponentData
    {
        public RestType Type;

        /// <summary>For short rest: number of hit dice the player wants to spend.</summary>
        public int HitDiceToSpend;
    }

    public enum RestType : byte
    {
        Short = 0,
        Long  = 1
    }
}
