using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Marks an entity (chest, corpse, crate) as a lootable container.
    /// LootPickupManager (MonoBehaviour) opens the loot UI when the player interacts.
    /// </summary>
    public struct LootTableComponent : IComponentData
    {
        /// <summary>Unique treasure id — matches the GM module treasure JSON.</summary>
        public FixedString64Bytes TreasureId;

        /// <summary>True if the container requires a Thieves' Tools check to open.</summary>
        public bool Locked;

        /// <summary>DC of the Thieves' Tools check to pick the lock. 0 if not locked.</summary>
        public int LockDC;

        /// <summary>True once the container has been looted (prevents duplicate rewards).</summary>
        public bool Looted;

        /// <summary>Base gold value of this container's contents.</summary>
        public int GoldValue;

        /// <summary>Comma-separated item ids to award on open (e.g. "health_potion,longsword").</summary>
        public FixedString128Bytes ItemIds;
    }
}
