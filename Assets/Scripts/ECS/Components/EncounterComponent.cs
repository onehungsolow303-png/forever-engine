using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Marks an entity as an encounter trigger zone.
    /// When the player enters TriggerRadius and Activated is false,
    /// EncounterTriggerSystem transitions to Combat and spawns monster entities.
    /// </summary>
    public struct EncounterComponent : IComponentData
    {
        /// <summary>Unique encounter id matching the GM module encounter JSON.</summary>
        public FixedString64Bytes EncounterId;

        /// <summary>Radius in tiles at which the encounter activates.</summary>
        public float TriggerRadius;

        /// <summary>True after the encounter has been triggered (prevents re-triggering).</summary>
        public bool Activated;

        /// <summary>Total XP value of all monsters in this encounter.</summary>
        public int TotalXP;

        /// <summary>"Easy", "Medium", "Hard", or "Deadly".</summary>
        public FixedString32Bytes Difficulty;

        /// <summary>Number of monsters to spawn (looked up in MonsterDatabase).</summary>
        public int MonsterCount;

        /// <summary>Id of the monster type to spawn (from MonsterDatabase).</summary>
        public FixedString64Bytes MonsterTemplateId;
    }
}
