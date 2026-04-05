namespace ForeverEngine.RPG.Data
{
    public static class SpellSlotTable
    {
        // FullCasterSlots[level-1][spellLevel-1]
        // Rows = caster level 1-20, Columns = spell level 1-9
        private static readonly int[,] FullCasterSlots = new int[20, 9]
        {
            //  1st 2nd 3rd 4th 5th 6th 7th 8th 9th
            {   2,  0,  0,  0,  0,  0,  0,  0,  0 },  // Level  1
            {   3,  0,  0,  0,  0,  0,  0,  0,  0 },  // Level  2
            {   4,  2,  0,  0,  0,  0,  0,  0,  0 },  // Level  3
            {   4,  3,  0,  0,  0,  0,  0,  0,  0 },  // Level  4
            {   4,  3,  2,  0,  0,  0,  0,  0,  0 },  // Level  5
            {   4,  3,  3,  0,  0,  0,  0,  0,  0 },  // Level  6
            {   4,  3,  3,  1,  0,  0,  0,  0,  0 },  // Level  7
            {   4,  3,  3,  2,  0,  0,  0,  0,  0 },  // Level  8
            {   4,  3,  3,  3,  1,  0,  0,  0,  0 },  // Level  9
            {   4,  3,  3,  3,  2,  0,  0,  0,  0 },  // Level 10
            {   4,  3,  3,  3,  2,  1,  0,  0,  0 },  // Level 11
            {   4,  3,  3,  3,  2,  1,  0,  0,  0 },  // Level 12
            {   4,  3,  3,  3,  2,  1,  1,  0,  0 },  // Level 13
            {   4,  3,  3,  3,  2,  1,  1,  0,  0 },  // Level 14
            {   4,  3,  3,  3,  2,  1,  1,  1,  0 },  // Level 15
            {   4,  3,  3,  3,  2,  1,  1,  1,  0 },  // Level 16
            {   4,  3,  3,  3,  2,  1,  1,  1,  1 },  // Level 17
            {   4,  3,  3,  3,  3,  1,  1,  1,  1 },  // Level 18
            {   4,  3,  3,  3,  3,  2,  1,  1,  1 },  // Level 19
            {   4,  3,  3,  3,  3,  2,  2,  1,  1 },  // Level 20
        };

        // HalfCasterSlots[level-1][spellLevel-1]
        // Rows = class level 1-20, Columns = spell level 1-5
        // Half casters get slots at class level 2+
        private static readonly int[,] HalfCasterSlots = new int[20, 5]
        {
            //  1st 2nd 3rd 4th 5th
            {   0,  0,  0,  0,  0 },  // Level  1 (no casting yet)
            {   2,  0,  0,  0,  0 },  // Level  2
            {   3,  0,  0,  0,  0 },  // Level  3
            {   3,  0,  0,  0,  0 },  // Level  4
            {   4,  2,  0,  0,  0 },  // Level  5
            {   4,  2,  0,  0,  0 },  // Level  6
            {   4,  3,  0,  0,  0 },  // Level  7
            {   4,  3,  0,  0,  0 },  // Level  8
            {   4,  3,  2,  0,  0 },  // Level  9
            {   4,  3,  2,  0,  0 },  // Level 10
            {   4,  3,  3,  0,  0 },  // Level 11
            {   4,  3,  3,  0,  0 },  // Level 12
            {   4,  3,  3,  1,  0 },  // Level 13
            {   4,  3,  3,  1,  0 },  // Level 14
            {   4,  3,  3,  2,  0 },  // Level 15
            {   4,  3,  3,  2,  0 },  // Level 16
            {   4,  3,  3,  3,  1 },  // Level 17
            {   4,  3,  3,  3,  1 },  // Level 18
            {   4,  3,  3,  3,  2 },  // Level 19
            {   4,  3,  3,  3,  2 },  // Level 20
        };

        // ThirdCasterSlots[level-1][spellLevel-1]
        // Rows = class level 1-20, Columns = spell level 1-4
        // Third casters get slots at class level 3+
        private static readonly int[,] ThirdCasterSlots = new int[20, 4]
        {
            //  1st 2nd 3rd 4th
            {   0,  0,  0,  0 },  // Level  1
            {   0,  0,  0,  0 },  // Level  2
            {   2,  0,  0,  0 },  // Level  3
            {   3,  0,  0,  0 },  // Level  4
            {   3,  0,  0,  0 },  // Level  5
            {   3,  0,  0,  0 },  // Level  6
            {   4,  2,  0,  0 },  // Level  7
            {   4,  2,  0,  0 },  // Level  8
            {   4,  2,  0,  0 },  // Level  9
            {   4,  3,  0,  0 },  // Level 10
            {   4,  3,  0,  0 },  // Level 11
            {   4,  3,  0,  0 },  // Level 12
            {   4,  3,  2,  0 },  // Level 13
            {   4,  3,  2,  0 },  // Level 14
            {   4,  3,  2,  0 },  // Level 15
            {   4,  3,  3,  0 },  // Level 16
            {   4,  3,  3,  0 },  // Level 17
            {   4,  3,  3,  0 },  // Level 18
            {   4,  3,  3,  1 },  // Level 19
            {   4,  3,  3,  1 },  // Level 20
        };

        // PactMagicSlots[level-1][0] = slot count, [level-1][1] = slot level
        private static readonly int[,] PactMagicSlots = new int[20, 2]
        {
            //  Count  Level
            {   1,     1 },  // Level  1
            {   2,     1 },  // Level  2
            {   2,     2 },  // Level  3
            {   2,     2 },  // Level  4
            {   2,     3 },  // Level  5
            {   2,     3 },  // Level  6
            {   2,     4 },  // Level  7
            {   2,     4 },  // Level  8
            {   2,     5 },  // Level  9
            {   2,     5 },  // Level 10
            {   3,     5 },  // Level 11
            {   3,     5 },  // Level 12
            {   3,     5 },  // Level 13
            {   3,     5 },  // Level 14
            {   3,     5 },  // Level 15
            {   3,     5 },  // Level 16
            {   4,     5 },  // Level 17
            {   4,     5 },  // Level 18
            {   4,     5 },  // Level 19
            {   4,     5 },  // Level 20
        };

        /// <summary>
        /// Get spell slots for a full caster at a given caster level.
        /// Returns array of 9 ints (spell levels 1-9).
        /// </summary>
        public static int[] GetFullCasterSlots(int casterLevel)
        {
            var slots = new int[9];
            if (casterLevel < 1 || casterLevel > 20) return slots;
            for (int i = 0; i < 9; i++)
                slots[i] = FullCasterSlots[casterLevel - 1, i];
            return slots;
        }

        /// <summary>
        /// Get spell slots for a half caster at a given class level.
        /// Returns array of 5 ints (spell levels 1-5).
        /// </summary>
        public static int[] GetHalfCasterSlots(int classLevel)
        {
            var slots = new int[5];
            if (classLevel < 1 || classLevel > 20) return slots;
            for (int i = 0; i < 5; i++)
                slots[i] = HalfCasterSlots[classLevel - 1, i];
            return slots;
        }

        /// <summary>
        /// Get spell slots for a third caster at a given class level.
        /// Returns array of 4 ints (spell levels 1-4).
        /// </summary>
        public static int[] GetThirdCasterSlots(int classLevel)
        {
            var slots = new int[4];
            if (classLevel < 1 || classLevel > 20) return slots;
            for (int i = 0; i < 4; i++)
                slots[i] = ThirdCasterSlots[classLevel - 1, i];
            return slots;
        }

        /// <summary>
        /// Get Pact Magic slot count and level for a Warlock at a given class level.
        /// Returns (slotCount, slotLevel).
        /// </summary>
        public static (int slotCount, int slotLevel) GetPactMagicSlots(int classLevel)
        {
            if (classLevel < 1 || classLevel > 20) return (0, 0);
            return (PactMagicSlots[classLevel - 1, 0], PactMagicSlots[classLevel - 1, 1]);
        }

        /// <summary>
        /// Get multiclass spell slots based on combined effective caster level.
        /// Uses the full caster table. Returns array of 9 ints (spell levels 1-9).
        /// </summary>
        public static int[] GetMulticlassSlots(int effectiveCasterLevel)
        {
            if (effectiveCasterLevel < 1) return new int[9];
            if (effectiveCasterLevel > 20) effectiveCasterLevel = 20;
            return GetFullCasterSlots(effectiveCasterLevel);
        }
    }
}
