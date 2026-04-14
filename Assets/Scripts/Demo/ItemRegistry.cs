using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo
{
    public static class ItemRegistry
    {
        private static readonly Dictionary<int, string> _names = new();

        static ItemRegistry()
        {
            Register("Food");
            Register("Water");
            Register("Health Potion");
        }

        public static void Register(string name)
        {
            _names[name.GetHashCode()] = name;
        }

        public static string GetName(int itemId)
        {
            if (itemId == ItemIds.Food) return "Food";
            if (itemId == ItemIds.Water) return "Water";
            if (itemId == ItemIds.HealthPotion) return "Health Potion";
            return _names.TryGetValue(itemId, out var n) ? n : $"Item #{itemId}";
        }

        public static bool IsConsumable(int itemId) =>
            itemId == ItemIds.Food || itemId == ItemIds.Water || itemId == ItemIds.HealthPotion;

        public static RPG.Items.WeaponData TryGetWeapon(int itemId)
        {
            string name = GetName(itemId);
            var weapons = Resources.LoadAll<RPG.Items.WeaponData>("RPG/Content/Weapons");
            foreach (var w in weapons)
                if (w.Name == name) return w;
            return null;
        }

        public static RPG.Items.ArmorData TryGetArmor(int itemId)
        {
            string name = GetName(itemId);
            var armor = Resources.LoadAll<RPG.Items.ArmorData>("RPG/Content/Armor");
            foreach (var a in armor)
                if (a.Name == name) return a;
            return null;
        }
    }
}
