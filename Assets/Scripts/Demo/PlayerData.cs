using System.Collections.Generic;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Demo
{
    [System.Serializable]
    public class PlayerData
    {
        public int HexQ, HexR;
        public int HP = 20, MaxHP = 20, AC = 12;
        public int Strength = 14, Dexterity = 12, Constitution = 12;
        public int Speed = 6;
        public string AttackDice = "1d8+2";
        public int Level = 1;
        public float Hunger = 100, Thirst = 100;
        public float MaxHunger = 100, MaxThirst = 100;
        public int Gold = 10;
        public Inventory Inventory;
        public int DayCount = 1;
        public HashSet<string> ExploredHexes = new();
        public HashSet<string> DiscoveredLocations = new();
        public string LastSafeLocation = "camp";
        public string WeaponName = "Rusty Sword";
        public string ArmorName = "Leather Armor";

        public PlayerData()
        {
            Inventory = new Inventory(20);
            Inventory.Add(new ItemInstance { ItemId = 100, StackCount = 3, MaxStack = 10 }); // Food
            Inventory.Add(new ItemInstance { ItemId = 101, StackCount = 3, MaxStack = 10 }); // Water
        }

        public bool IsAlive => HP > 0;
        public bool IsStarving => Hunger <= 0;
        public bool IsDehydrated => Thirst <= 0;
        public float HungerPercent => Hunger / MaxHunger;
        public float ThirstPercent => Thirst / MaxThirst;
        public float HPPercent => MaxHP > 0 ? (float)HP / MaxHP : 0;
        public string HexKey => $"{HexQ},{HexR}";

        public void DrainHunger(float amount) { Hunger = System.Math.Max(0, Hunger - amount); }
        public void DrainThirst(float amount) { Thirst = System.Math.Max(0, Thirst - amount); }
        public void Eat(float amount) { Hunger = System.Math.Min(MaxHunger, Hunger + amount); }
        public void Drink(float amount) { Thirst = System.Math.Min(MaxThirst, Thirst + amount); }
        public void Heal(int amount) { HP = System.Math.Min(MaxHP, HP + amount); }
        public void TakeDamage(int amount) { HP = System.Math.Max(0, HP - amount); }
        public void LevelUp() { Level++; MaxHP += 5; HP = MaxHP; }
        public void FullRest() { HP = MaxHP; Hunger = MaxHunger; Thirst = MaxThirst; }
    }
}
