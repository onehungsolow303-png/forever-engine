using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// One-frame request component — added to a caster entity when they want to cast a spell.
    /// SpellSystem reads this, validates slot availability, resolves effects, then removes it.
    /// Following the ForeverEngine one-frame command pattern.
    /// </summary>
    public struct SpellCastRequestComponent : IComponentData
    {
        public FixedString64Bytes SpellId;

        /// <summary>Spell slot level used (may be higher than spell's base level for upcast).</summary>
        public int SlotLevel;

        /// <summary>Target entity. Entity.Null for self-targeting or area spells.</summary>
        public Entity Target;

        /// <summary>Target world tile position (for AoE spells).</summary>
        public int TargetX;
        public int TargetY;
    }

    /// <summary>
    /// One-frame result component added to the caster after SpellSystem resolves the cast.
    /// The UI layer reads this to display feedback, then removes it.
    /// </summary>
    public struct SpellCastResultComponent : IComponentData
    {
        public FixedString64Bytes SpellId;
        public bool Success;
        public FixedString64Bytes FailReason; // "no_slot", "no_concentration", "incapacitated", etc.
        public int DamageDealt;
        public int HealingDone;
    }
}
