using System.Collections.Generic;

namespace ForeverEngine.Generation.Data
{
    public static class GameTables
    {
        public static readonly Dictionary<int, int> PartyXPBudget = new()
        {
            [1] = 75, [2] = 150, [3] = 225, [4] = 375, [5] = 500,
            [6] = 600, [7] = 750, [8] = 900, [9] = 1100, [10] = 1200,
            [11] = 1600, [12] = 2000, [13] = 2200, [14] = 2500, [15] = 2800,
            [16] = 3200, [17] = 3900, [18] = 4200, [19] = 4900, [20] = 5700
        };

        public static readonly Dictionary<int, int> TreasureGoldByLevel = new()
        {
            [1] = 20, [2] = 30, [3] = 50, [4] = 80, [5] = 130,
            [6] = 200, [7] = 300, [8] = 500, [9] = 700, [10] = 1000,
            [11] = 1500, [12] = 2000, [13] = 3000, [14] = 4000, [15] = 6000,
            [16] = 8000, [17] = 12000, [18] = 16000, [19] = 24000, [20] = 35000
        };

        public static int GetXPBudget(int partyLevel, int partySize)
        {
            PartyXPBudget.TryGetValue(System.Math.Clamp(partyLevel, 1, 20), out int perPlayer);
            return perPlayer * partySize;
        }

        public static int GetGoldBudget(int partyLevel, float lootTier)
        {
            TreasureGoldByLevel.TryGetValue(System.Math.Clamp(partyLevel, 1, 20), out int baseGold);
            return (int)(baseGold * lootTier);
        }
    }
}
