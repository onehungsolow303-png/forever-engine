using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// All inputs needed to cast a spell.
    /// </summary>
    [System.Serializable]
    public struct CastContext
    {
        public object Caster; // CharacterSheet (typed as object to avoid circular ref; cast at runtime)
        public object[] Targets; // CharacterSheet[] or positions for AoE
        public SpellData Spell;
        public int SlotLevel; // Must be >= spell level
        public MetamagicType Metamagic;
        public bool IsRitual; // Cast as ritual (no slot expended, +10 min casting time)
    }
}
