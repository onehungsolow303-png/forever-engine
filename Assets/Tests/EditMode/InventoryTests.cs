using NUnit.Framework;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Tests
{
    public class InventoryTests
    {
        [Test] public void NewInventory_IsEmpty() { var inv = new Inventory(20); Assert.AreEqual(0, inv.Count); Assert.AreEqual(20, inv.MaxSlots); Assert.AreEqual(0, inv.Gold); }
        [Test] public void AddItem_IncreasesCount() { var inv = new Inventory(20); bool added = inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 }); Assert.IsTrue(added); Assert.AreEqual(1, inv.Count); }
        [Test] public void AddItem_FullInventory_Fails() { var inv = new Inventory(2); inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 }); inv.Add(new ItemInstance { ItemId = 2, StackCount = 1 }); Assert.IsFalse(inv.Add(new ItemInstance { ItemId = 3, StackCount = 1 })); Assert.AreEqual(2, inv.Count); }
        [Test] public void AddItem_Stackable_MergesStack() { var inv = new Inventory(20); inv.Add(new ItemInstance { ItemId = 1, StackCount = 3, MaxStack = 10 }); inv.Add(new ItemInstance { ItemId = 1, StackCount = 5, MaxStack = 10 }); Assert.AreEqual(1, inv.Count); Assert.AreEqual(8, inv.GetSlot(0).StackCount); }
        [Test] public void RemoveItem_DecreasesCount() { var inv = new Inventory(20); inv.Add(new ItemInstance { ItemId = 1, StackCount = 5, MaxStack = 10 }); Assert.IsTrue(inv.Remove(1, 3)); Assert.AreEqual(2, inv.GetSlot(0).StackCount); }
        [Test] public void RemoveItem_EntireStack_RemovesSlot() { var inv = new Inventory(20); inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 }); inv.Remove(1, 1); Assert.AreEqual(0, inv.Count); }
        [Test] public void RemoveItem_NotPresent_Fails() { var inv = new Inventory(20); Assert.IsFalse(inv.Remove(999, 1)); }
        [Test] public void Equip_SetsEquipped() { var inv = new Inventory(20); inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 }); inv.Equip(0); Assert.IsTrue(inv.GetSlot(0).Equipped); }
        [Test] public void Unequip_ClearsEquipped() { var inv = new Inventory(20); inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 }); inv.Equip(0); inv.Unequip(0); Assert.IsFalse(inv.GetSlot(0).Equipped); }
        [Test] public void Gold_AddAndRemove() { var inv = new Inventory(20); inv.AddGold(100); Assert.AreEqual(100, inv.Gold); Assert.IsTrue(inv.SpendGold(60)); Assert.AreEqual(40, inv.Gold); }
        [Test] public void Gold_CantOverspend() { var inv = new Inventory(20); inv.AddGold(50); Assert.IsFalse(inv.SpendGold(100)); Assert.AreEqual(50, inv.Gold); }
    }
}
