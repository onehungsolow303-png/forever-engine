using System.Collections.Generic;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Demo.NPCs
{
    public struct ShopItem { public string Name; public int ItemId; public int Price; public int MaxStack; public string Description; }

    public class ShopSystem
    {
        private List<ShopItem> _stock = new();

        public static ShopSystem CreateMerchantShop()
        {
            var shop = new ShopSystem();
            shop._stock.Add(new ShopItem { Name = "Bread", ItemId = 100, Price = 5, MaxStack = 10, Description = "Restores 25 hunger" });
            shop._stock.Add(new ShopItem { Name = "Water Flask", ItemId = 101, Price = 5, MaxStack = 10, Description = "Restores 25 thirst" });
            shop._stock.Add(new ShopItem { Name = "Health Potion", ItemId = 102, Price = 15, MaxStack = 5, Description = "Restores 10 HP" });
            shop._stock.Add(new ShopItem { Name = "Rations", ItemId = 103, Price = 10, MaxStack = 10, Description = "Restores 50 hunger" });
            return shop;
        }

        public List<ShopItem> GetStock() => new(_stock);

        public bool Buy(ShopItem item, PlayerData player)
        {
            if (player.Gold < item.Price) return false;
            bool added = player.Inventory.Add(new ItemInstance { ItemId = item.ItemId, StackCount = 1, MaxStack = item.MaxStack });
            if (!added) return false;
            player.Gold -= item.Price;
            return true;
        }

        public void Sell(int slotIndex, PlayerData player, int sellRatio = 2)
        {
            var slot = player.Inventory.GetSlot(slotIndex);
            if (slot.IsEmpty) return;
            int sellPrice = GetSellPrice(slot.ItemId) / sellRatio;
            if (sellPrice < 1) sellPrice = 1;
            player.Gold += sellPrice;
            player.Inventory.Remove(slot.ItemId, 1);
        }

        private int GetSellPrice(int itemId) => itemId switch
        {
            100 => 5, 101 => 5, 102 => 15, 103 => 10,
            _ => 3
        };
    }
}
