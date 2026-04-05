using System.Collections.Generic;

namespace ForeverEngine.ECS.Data
{
    public class Inventory
    {
        private readonly List<ItemInstance> _slots;
        public int MaxSlots { get; }
        public int Count => _slots.Count;
        public int Gold { get; private set; }

        public Inventory(int maxSlots)
        {
            MaxSlots = maxSlots;
            _slots = new List<ItemInstance>(maxSlots);
        }

        public ItemInstance GetSlot(int index)
        {
            return index >= 0 && index < _slots.Count ? _slots[index] : ItemInstance.Empty;
        }

        public bool Add(ItemInstance item)
        {
            if (item.MaxStack > 1)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].ItemId == item.ItemId && _slots[i].StackCount < _slots[i].MaxStack)
                    {
                        var slot = _slots[i];
                        int space = slot.MaxStack - slot.StackCount;
                        int toAdd = item.StackCount <= space ? item.StackCount : space;
                        slot.StackCount += toAdd;
                        _slots[i] = slot;
                        item.StackCount -= toAdd;
                        if (item.StackCount <= 0) return true;
                    }
                }
            }
            if (_slots.Count >= MaxSlots) return false;
            _slots.Add(item);
            return true;
        }

        public bool Remove(int itemId, int count = 1)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemId == itemId)
                {
                    var slot = _slots[i];
                    if (slot.StackCount < count) return false;
                    slot.StackCount -= count;
                    if (slot.StackCount <= 0) _slots.RemoveAt(i);
                    else _slots[i] = slot;
                    return true;
                }
            }
            return false;
        }

        public bool HasItem(int itemId, int count = 1)
        {
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].ItemId == itemId) total += _slots[i].StackCount;
            return total >= count;
        }

        public void Equip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;
            var slot = _slots[slotIndex]; slot.Equipped = true; _slots[slotIndex] = slot;
        }

        public void Unequip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;
            var slot = _slots[slotIndex]; slot.Equipped = false; _slots[slotIndex] = slot;
        }

        public void AddGold(int amount) => Gold += amount;

        public bool SpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount; return true;
        }

        public List<ItemInstance> GetAllSlots() => new List<ItemInstance>(_slots);
    }
}
