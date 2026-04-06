using UnityEngine;
using ForeverEngine.RPG.Items;
using ForeverEngine.RPG.Enums;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Generates CR-scaled loot drops after battle victory.
    /// Higher CR encounters yield rarer equipment.
    /// </summary>
    public static class LootGenerator
    {
        private static WeaponData[] _allWeapons;
        private static ArmorData[] _allArmor;

        /// <summary>
        /// Generate loot for a battle victory. Returns item names found.
        /// Drop chance: 30% per battle, scaled by encounter difficulty.
        /// </summary>
        public static string[] GenerateLoot(int encounterCR, int goldReward)
        {
            EnsureLoaded();

            var items = new List<string>();
            var rng = new System.Random(System.Environment.TickCount);

            // 30% chance of equipment drop
            if (rng.NextDouble() > 0.30)
                return items.ToArray();

            Rarity maxRarity = encounterCR switch
            {
                <= 25 => Rarity.Common,
                <= 100 => Rarity.Uncommon,
                <= 500 => Rarity.Rare,
                _ => Rarity.VeryRare
            };

            // 50/50 weapon or armor
            if (rng.NextDouble() < 0.5)
            {
                var eligible = System.Array.FindAll(_allWeapons, w => w != null && w.Rarity <= maxRarity);
                if (eligible.Length > 0)
                {
                    var drop = eligible[rng.Next(eligible.Length)];
                    items.Add(drop.Name);
                }
            }
            else
            {
                var eligible = System.Array.FindAll(_allArmor, a => a != null && a.Rarity <= maxRarity);
                if (eligible.Length > 0)
                {
                    var drop = eligible[rng.Next(eligible.Length)];
                    items.Add(drop.Name);
                }
            }

            return items.ToArray();
        }

        private static void EnsureLoaded()
        {
            if (_allWeapons != null) return;
            _allWeapons = Resources.LoadAll<WeaponData>("RPG/Content/Weapons");
            _allArmor = Resources.LoadAll<ArmorData>("RPG/Content/Armor");
            Debug.Log($"[LootGenerator] Loaded {_allWeapons.Length} weapons, {_allArmor.Length} armor for loot tables");
        }
    }
}
