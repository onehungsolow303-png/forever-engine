using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Data
{
    public static class ExperienceTable
    {
        // XP required to reach each level (index 0 = level 1 = 0 XP)
        // Levels 1-20: Standard D&D 5e XP thresholds
        // Levels 21-40: Extended progression (each level requires progressively more)
        private static readonly int[] _thresholds = new int[40]
        {
            // Level  1-5 (standard D&D 5e)
            0,          // Level  1
            300,        // Level  2
            900,        // Level  3
            2700,       // Level  4
            6500,       // Level  5
            // Level  6-10
            14000,      // Level  6
            23000,      // Level  7
            34000,      // Level  8
            48000,      // Level  9
            64000,      // Level 10
            // Level 11-15
            85000,      // Level 11
            100000,     // Level 12
            120000,     // Level 13
            140000,     // Level 14
            165000,     // Level 15
            // Level 16-20
            195000,     // Level 16
            225000,     // Level 17
            265000,     // Level 18
            305000,     // Level 19
            355000,     // Level 20
            // Level 21-25 (extended — Paragon tier)
            405000,     // Level 21
            465000,     // Level 22
            535000,     // Level 23
            615000,     // Level 24
            705000,     // Level 25
            // Level 26-30 (Epic tier)
            805000,     // Level 26
            915000,     // Level 27
            1035000,    // Level 28
            1165000,    // Level 29
            1305000,    // Level 30
            // Level 31-35 (Mythic tier)
            1455000,    // Level 31
            1615000,    // Level 32
            1785000,    // Level 33
            1965000,    // Level 34
            2155000,    // Level 35
            // Level 36-40 (Divine tier)
            2355000,    // Level 36
            2565000,    // Level 37
            2785000,    // Level 38
            3015000,    // Level 39
            3255000     // Level 40
        };

        /// <summary>
        /// Get XP threshold to reach a given level (1-40).
        /// </summary>
        public static int GetThreshold(int level)
        {
            if (level < 1) return 0;
            if (level > 40) return _thresholds[39];
            return _thresholds[level - 1];
        }

        /// <summary>
        /// Determine what level a character should be based on their total XP.
        /// </summary>
        public static int GetLevelForXP(int xp)
        {
            for (int i = 39; i >= 0; i--)
            {
                if (xp >= _thresholds[i]) return i + 1;
            }
            return 1;
        }

        /// <summary>
        /// Get the tier for a given character level.
        /// </summary>
        public static Tier GetTierForLevel(int level)
        {
            if (level <= 4)  return Tier.Adventurer;
            if (level <= 10) return Tier.Journeyman;
            if (level <= 16) return Tier.Hero;
            if (level <= 20) return Tier.Legend;
            if (level <= 24) return Tier.Paragon;
            if (level <= 28) return Tier.Epic;
            if (level <= 32) return Tier.Mythic;
            if (level <= 36) return Tier.Demigod;
            return Tier.Divine;
        }
    }
}
