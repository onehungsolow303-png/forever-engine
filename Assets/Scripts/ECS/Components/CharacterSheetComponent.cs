using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Full D&D 5e character sheet for a player entity.
    /// Tracks class, level, XP, spell slots, and hit dice.
    /// Only attached to player-controlled entities (see PlayerTag).
    /// </summary>
    public struct CharacterSheetComponent : IComponentData
    {
        // Identity
        public FixedString64Bytes CharacterName;
        public FixedString32Bytes Species;       // "Human", "Elf", "Dwarf", etc.
        public FixedString32Bytes ClassName;     // "Fighter", "Wizard", "Rogue", etc.
        public FixedString32Bytes Subclass;      // "Champion", "Evocation", etc.
        public FixedString32Bytes Background;    // "Acolyte", "Criminal", etc.

        // Progression
        public int Level;
        public int ExperiencePoints;
        public int ProficiencyBonus;

        // ── Spell slots (1-based; index maps to spell level) ─────────────
        // Stored flat to keep the struct unmanaged and Burst-friendly.
        public int SpellSlot1Max;  public int SpellSlot1Used;
        public int SpellSlot2Max;  public int SpellSlot2Used;
        public int SpellSlot3Max;  public int SpellSlot3Used;
        public int SpellSlot4Max;  public int SpellSlot4Used;
        public int SpellSlot5Max;  public int SpellSlot5Used;
        public int SpellSlot6Max;  public int SpellSlot6Used;
        public int SpellSlot7Max;  public int SpellSlot7Used;
        public int SpellSlot8Max;  public int SpellSlot8Used;
        public int SpellSlot9Max;  public int SpellSlot9Used;

        // ── Hit dice ──────────────────────────────────────────────────────
        public int HitDiceTotal;
        public int HitDiceUsed;
        public FixedString32Bytes HitDieType;   // "d6", "d8", "d10", "d12"

        // ── Concentration ─────────────────────────────────────────────────
        public bool IsConcentrating;
        public FixedString64Bytes ConcentrationSpellId;

        // ── Convenience helpers ───────────────────────────────────────────

        /// <summary>Returns the max slots for a given spell level (1-9). Returns 0 for invalid input.</summary>
        public int GetMaxSlots(int spellLevel) => spellLevel switch
        {
            1 => SpellSlot1Max, 2 => SpellSlot2Max, 3 => SpellSlot3Max,
            4 => SpellSlot4Max, 5 => SpellSlot5Max, 6 => SpellSlot6Max,
            7 => SpellSlot7Max, 8 => SpellSlot8Max, 9 => SpellSlot9Max,
            _ => 0
        };

        /// <summary>Returns the used slots for a given spell level (1-9).</summary>
        public int GetUsedSlots(int spellLevel) => spellLevel switch
        {
            1 => SpellSlot1Used, 2 => SpellSlot2Used, 3 => SpellSlot3Used,
            4 => SpellSlot4Used, 5 => SpellSlot5Used, 6 => SpellSlot6Used,
            7 => SpellSlot7Used, 8 => SpellSlot8Used, 9 => SpellSlot9Used,
            _ => 0
        };

        /// <summary>Returns true if at least one slot is available at this spell level.</summary>
        public bool HasSlotAvailable(int spellLevel) =>
            GetUsedSlots(spellLevel) < GetMaxSlots(spellLevel);

        public static CharacterSheetComponent Default => new CharacterSheetComponent
        {
            Level = 1,
            ExperiencePoints = 0,
            ProficiencyBonus = 2,
            HitDiceTotal = 1,
            HitDiceUsed = 0,
            HitDieType = "d8"
        };
    }
}
