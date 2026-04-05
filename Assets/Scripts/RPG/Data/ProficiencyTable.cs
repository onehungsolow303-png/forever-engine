namespace ForeverEngine.RPG.Data
{
    public static class ProficiencyTable
    {
        // Index 0 = level 1, Index 39 = level 40
        // Levels 1-4: +2, 5-8: +3, 9-12: +4, 13-16: +5, 17-20: +6
        // Levels 21-24: +7, 25-28: +8, 29-32: +9, 33-36: +10, 37-40: +11
        private static readonly int[] _bonuses = new int[40]
        {
            // Level  1- 4
            2, 2, 2, 2,
            // Level  5- 8
            3, 3, 3, 3,
            // Level  9-12
            4, 4, 4, 4,
            // Level 13-16
            5, 5, 5, 5,
            // Level 17-20
            6, 6, 6, 6,
            // Level 21-24
            7, 7, 7, 7,
            // Level 25-28
            8, 8, 8, 8,
            // Level 29-32
            9, 9, 9, 9,
            // Level 33-36
            10, 10, 10, 10,
            // Level 37-40
            11, 11, 11, 11
        };

        /// <summary>
        /// Get proficiency bonus for a given character level (1-40).
        /// </summary>
        public static int GetBonus(int level)
        {
            if (level < 1) return 2;
            if (level > 40) return 11;
            return _bonuses[level - 1];
        }
    }
}
