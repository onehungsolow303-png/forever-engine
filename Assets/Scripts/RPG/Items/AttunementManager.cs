namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// Per-character attunement manager. D&D 5e limits characters to 3 attuned magic items.
    /// </summary>
    [System.Serializable]
    public class AttunementManager
    {
        public const int MaxSlots = 3;

        private readonly MagicItemData[] _slots = new MagicItemData[MaxSlots];

        /// <summary>
        /// Attune to a magic item. Returns false if all slots are full.
        /// </summary>
        public bool Attune(MagicItemData item)
        {
            if (item == null || !item.RequiresAttunement) return false;
            if (IsAttuned(item)) return true; // Already attuned

            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = item;
                    return true;
                }
            }
            return false; // All slots full
        }

        /// <summary>
        /// Remove attunement from a specific slot.
        /// </summary>
        public void Unattune(int slot)
        {
            if (slot >= 0 && slot < MaxSlots)
            {
                _slots[slot] = null;
            }
        }

        /// <summary>
        /// Remove attunement from a specific item.
        /// </summary>
        public void Unattune(MagicItemData item)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == item)
                {
                    _slots[i] = null;
                    return;
                }
            }
        }

        /// <summary>
        /// Check if a specific item is currently attuned.
        /// </summary>
        public bool IsAttuned(MagicItemData item)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == item) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the item in a specific attunement slot.
        /// </summary>
        public MagicItemData GetSlot(int slot)
        {
            if (slot >= 0 && slot < MaxSlots) return _slots[slot];
            return null;
        }

        /// <summary>
        /// Get the number of occupied attunement slots.
        /// </summary>
        public int OccupiedSlots
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_slots[i] != null) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Whether there's room for another attunement.
        /// </summary>
        public bool HasFreeSlot => OccupiedSlots < MaxSlots;

        /// <summary>
        /// Clear all attunements.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < MaxSlots; i++) _slots[i] = null;
        }
    }
}
