using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Marks an entity as a trap that can be detected, disarmed, or triggered.
    /// TrapDetectionSystem compares player passive perception vs PerceptionDC each turn.
    /// If triggered: player must make SaveType save vs SaveDC or take Damage.
    /// </summary>
    public struct TrapComponent : IComponentData
    {
        /// <summary>Unique trap id matching the map data JSON.</summary>
        public FixedString64Bytes TrapId;

        /// <summary>"spike pit", "poison dart", "alarm", "magical glyph", etc.</summary>
        public FixedString32Bytes TrapType;

        /// <summary>DC to notice the trap via passive perception or active Investigation check.</summary>
        public int PerceptionDC;

        /// <summary>Saving throw ability: "DEX", "CON", "WIS", etc.</summary>
        public FixedString32Bytes SaveType;

        /// <summary>DC of the saving throw when the trap is triggered.</summary>
        public int SaveDC;

        /// <summary>Damage dice string, e.g. "2d6" or "4d4+2".</summary>
        public FixedString32Bytes Damage;

        /// <summary>"piercing", "poison", "fire", etc.</summary>
        public FixedString32Bytes DamageType;

        /// <summary>Thieves' Tools DC to disarm the trap.</summary>
        public int DisarmDC;

        /// <summary>True once the trap has fired (prevents multiple triggers).</summary>
        public bool Triggered;

        /// <summary>True once the trap has been spotted by the player.</summary>
        public bool Detected;

        /// <summary>True once the trap has been disarmed.</summary>
        public bool Disarmed;
    }
}
