using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Tracks an NPC's faction membership and disposition toward the player.
    /// Disposition range: -100 (hostile) to +100 (devoted ally).
    /// The AI system uses Disposition to choose between combat and dialogue stances.
    /// </summary>
    public struct NPCPersonalityComponent : IComponentData
    {
        /// <summary>Unique NPC id matching the map/content JSON.</summary>
        public FixedString64Bytes NPCId;

        public FixedString64Bytes NPCName;

        /// <summary>Faction name for alignment with other NPCs: "town_guard", "thieves_guild", etc.</summary>
        public FixedString32Bytes Faction;

        /// <summary>Current disposition toward the player (-100 hostile … +100 ally).</summary>
        public int Disposition;

        /// <summary>Starting disposition used when resetting after a long rest or scene reload.</summary>
        public int DispositionDefault;

        /// <summary>
        /// True when personality traits, ideals, bonds and flaws have been seeded
        /// from the GM module response. The dialogue system gates advanced conversation
        /// behind this flag.
        /// </summary>
        public bool HasDialogueSeed;

        // ── Convenience thresholds ────────────────────────────────────────
        public bool IsHostile     => Disposition <= -50;
        public bool IsUnfriendly  => Disposition is > -50 and < -10;
        public bool IsNeutral     => Disposition is >= -10 and <= 10;
        public bool IsFriendly    => Disposition is > 10 and < 50;
        public bool IsAlly        => Disposition >= 50;
    }
}
