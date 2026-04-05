using System.Collections.Generic;
using ForeverEngine.ECS.Data;
using ForeverEngine.MonoBehaviour.CharacterCreation;

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

        /// <summary>
        /// Creates a PlayerData from a finalized CharacterData produced by CharacterCreator.
        /// Maps RPG stats (HP, AC, ability scores, speed) and assigns a class-appropriate
        /// starting weapon and armor name.
        /// </summary>
        public static PlayerData FromCharacterData(CharacterData cd)
        {
            var pd = new PlayerData();

            // Core stats
            pd.HP    = cd.currentHP;
            pd.MaxHP = cd.maxHP;
            pd.AC    = cd.armorClass;

            // Ability scores
            pd.Strength     = cd.strength;
            pd.Dexterity    = cd.dexterity;
            pd.Constitution = cd.constitution;

            // Speed: CharacterData stores feet; PlayerData uses tiles (1 tile = 5 ft)
            pd.Speed = cd.speed / 5;

            // Starting position
            pd.HexQ = 2;
            pd.HexR = 2;

            // Class-based weapon and attack dice
            (pd.WeaponName, pd.AttackDice, pd.ArmorName) = GetClassGear(cd.className);

            return pd;
        }

        private static (string weapon, string attackDice, string armor) GetClassGear(string className) =>
            className.ToLowerInvariant() switch
            {
                "fighter"   => ("Longsword",   "1d8+3",  "Chain Mail"),
                "warrior"   => ("Longsword",   "1d8+3",  "Chain Mail"),
                "rogue"     => ("Shortsword",  "1d6+3",  "Leather Armor"),
                "ranger"    => ("Longbow",     "1d8+2",  "Leather Armor"),
                "wizard"    => ("Quarterstaff","1d6+2",  "Robes"),
                "cleric"    => ("Mace",        "1d6+2",  "Chain Mail"),
                "paladin"   => ("Longsword",   "1d8+3",  "Plate Armor"),
                "barbarian" => ("Greataxe",    "1d12+3", "Hide Armor"),
                "bard"      => ("Rapier",      "1d8+2",  "Leather Armor"),
                _           => ("Shortsword",  "1d6+1",  "Leather Armor")
            };

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
