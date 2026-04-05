using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// A single status condition on an entity.
    /// Stored as a dynamic buffer so an entity can have multiple simultaneous conditions.
    /// ConditionSystem ticks RemainingRounds down and removes expired entries.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ConditionBufferElement : IBufferElementData
    {
        /// <summary>
        /// Condition name matching D&D 5e condition list:
        /// "blinded", "charmed", "deafened", "exhaustion", "frightened",
        /// "grappled", "incapacitated", "invisible", "paralyzed", "petrified",
        /// "poisoned", "prone", "restrained", "stunned", "unconscious"
        /// </summary>
        public FixedString32Bytes ConditionName;

        /// <summary>Rounds remaining. -1 = permanent (until removed by dispel/cure).</summary>
        public int RemainingRounds;

        /// <summary>Entity that applied this condition (for concentration tracking).</summary>
        public Entity Source;
    }

    /// <summary>
    /// Exhaustion is a special stacking condition (1-6 levels).
    /// Stored separately from the buffer because it accumulates rather than stacks as duplicates.
    /// </summary>
    public struct ExhaustionComponent : IComponentData
    {
        /// <summary>Exhaustion level (0 = none, 1–5 = progressive penalties, 6 = death).</summary>
        public int Level;
    }
}
