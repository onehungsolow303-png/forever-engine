using System.Collections.Generic;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Per-character spell slot manager. Handles full/half/third caster slot calculation,
    /// multiclass stacking, Pact Magic (Warlock), slot expenditure, and rest recovery.
    ///
    /// Multiclass caster level calculation:
    /// - Full caster class levels count at 1x
    /// - Half caster class levels count at 0.5x (round down)
    /// - Third caster class levels count at 0.33x (round down)
    /// - Sum = effective caster level -> look up shared multiclass table
    /// - Pact Magic (Warlock) slots are tracked separately and don't stack
    /// </summary>
    [System.Serializable]
    public class SpellSlotManager
    {
        /// <summary>
        /// Available spell slots per level (index 0 = 1st level, index 8 = 9th level).
        /// </summary>
        public int[] AvailableSlots = new int[9];

        /// <summary>
        /// Maximum spell slots per level (calculated from class levels).
        /// </summary>
        public int[] MaxSlots = new int[9];

        /// <summary>
        /// Warlock Pact Magic slot count.
        /// </summary>
        public int PactSlotCount;

        /// <summary>
        /// Warlock Pact Magic slot level.
        /// </summary>
        public int PactSlotLevel;

        /// <summary>
        /// Available Pact Magic slots (separate from regular slots).
        /// </summary>
        public int AvailablePactSlots;

        /// <summary>
        /// Recalculate all spell slots from class levels.
        /// Handles single class, multiclass stacking, and Pact Magic.
        /// </summary>
        public void RecalculateSlots(List<ClassLevel> classLevels)
        {
            // Reset
            for (int i = 0; i < 9; i++) MaxSlots[i] = 0;
            PactSlotCount = 0;
            PactSlotLevel = 0;

            if (classLevels == null || classLevels.Count == 0) return;

            // Check for single class vs multiclass
            bool isMulticlass = classLevels.Count > 1;
            int effectiveCasterLevel = 0;
            foreach (var cl in classLevels)
            {
                if (cl.ClassRef == null) continue;

                switch (cl.ClassRef.CastingType)
                {
                    case SpellcastingType.Full:
                        effectiveCasterLevel += cl.Level;
                        break;
                    case SpellcastingType.Half:
                        effectiveCasterLevel += cl.Level / 2;
                        break;
                    case SpellcastingType.Third:
                        effectiveCasterLevel += cl.Level / 3;
                        break;
                    case SpellcastingType.Pact:
                        var pact = SpellSlotTable.GetPactMagicSlots(cl.Level);
                        PactSlotCount = pact.slotCount;
                        PactSlotLevel = pact.slotLevel;
                        AvailablePactSlots = PactSlotCount;
                        break;
                }
            }

            if (isMulticlass || effectiveCasterLevel > 0)
            {
                // For multiclass (or any class combination), use the shared multiclass table
                if (isMulticlass)
                {
                    var slots = SpellSlotTable.GetMulticlassSlots(effectiveCasterLevel);
                    for (int i = 0; i < 9 && i < slots.Length; i++)
                        MaxSlots[i] = slots[i];
                }
                else
                {
                    // Single class — use class-specific table
                    var cl = classLevels[0];
                    if (cl.ClassRef != null)
                    {
                        switch (cl.ClassRef.CastingType)
                        {
                            case SpellcastingType.Full:
                            {
                                var slots = SpellSlotTable.GetFullCasterSlots(cl.Level);
                                for (int i = 0; i < 9 && i < slots.Length; i++)
                                    MaxSlots[i] = slots[i];
                                break;
                            }
                            case SpellcastingType.Half:
                            {
                                var slots = SpellSlotTable.GetHalfCasterSlots(cl.Level);
                                for (int i = 0; i < 5 && i < slots.Length; i++)
                                    MaxSlots[i] = slots[i];
                                break;
                            }
                            case SpellcastingType.Third:
                            {
                                var slots = SpellSlotTable.GetThirdCasterSlots(cl.Level);
                                for (int i = 0; i < 4 && i < slots.Length; i++)
                                    MaxSlots[i] = slots[i];
                                break;
                            }
                        }
                    }
                }
            }

            // Initialize available slots to max
            for (int i = 0; i < 9; i++)
                AvailableSlots[i] = MaxSlots[i];
        }

        /// <summary>
        /// Check if a spell can be cast at the given slot level.
        /// </summary>
        public bool CanCast(SpellData spell, int slotLevel)
        {
            // Cantrips don't need slots
            if (spell.IsCantrip) return true;

            // Slot level must be >= spell level
            if (slotLevel < spell.Level) return false;

            // Check regular slots
            if (slotLevel >= 1 && slotLevel <= 9)
            {
                if (AvailableSlots[slotLevel - 1] > 0) return true;
            }

            // Check Pact Magic slots
            if (PactSlotCount > 0 && AvailablePactSlots > 0 && slotLevel == PactSlotLevel)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expend a spell slot at the given level. Tries regular slots first, then Pact slots.
        /// Returns false if no slot available.
        /// </summary>
        public bool ExpendSlot(int level)
        {
            if (level < 1 || level > 9) return false;

            // Try regular slots first
            if (AvailableSlots[level - 1] > 0)
            {
                AvailableSlots[level - 1]--;
                return true;
            }

            // Try Pact Magic slots
            if (PactSlotCount > 0 && AvailablePactSlots > 0 && level == PactSlotLevel)
            {
                AvailablePactSlots--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Restore all spell slots (long rest).
        /// </summary>
        public void RestoreAll()
        {
            for (int i = 0; i < 9; i++)
                AvailableSlots[i] = MaxSlots[i];
            AvailablePactSlots = PactSlotCount;
        }

        /// <summary>
        /// Restore Pact Magic slots only (short rest).
        /// </summary>
        public void RestorePactSlots()
        {
            AvailablePactSlots = PactSlotCount;
        }

        /// <summary>
        /// Get the highest available slot level (for AI/UI display).
        /// </summary>
        public int HighestAvailableSlot
        {
            get
            {
                for (int i = 8; i >= 0; i--)
                {
                    if (AvailableSlots[i] > 0) return i + 1;
                }
                if (AvailablePactSlots > 0) return PactSlotLevel;
                return 0;
            }
        }
    }
}
