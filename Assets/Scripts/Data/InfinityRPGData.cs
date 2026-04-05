namespace ForeverEngine.Data
{
    /// <summary>
    /// Static reference tables for D&D 5e-style RPG rules.
    /// All magic numbers live here — do not hardcode in systems.
    /// </summary>
    public static class InfinityRPGData
    {
        // ── Proficiency bonus ────────────────────────────────────────────────
        /// <summary>Returns proficiency bonus for a given character level (1–40).</summary>
        public static int GetProficiencyBonus(int level) => 2 + (level - 1) / 4;

        // ── Ability modifier ────────────────────────────────────────────────
        /// <summary>Standard D&D ability modifier: floor((score - 10) / 2).</summary>
        public static int AbilityModifier(int score)
        {
            int diff = score - 10;
            return diff >= 0 ? diff / 2 : (diff - 1) / 2;
        }

        // ── Hit die ─────────────────────────────────────────────────────────
        public static int HitDieValue(string hitDie) => hitDie switch
        {
            "d6"  => 6,
            "d8"  => 8,
            "d10" => 10,
            "d12" => 12,
            _     => 8
        };

        // ── XP per level (index = level, so XPByLevel[1] = 0, XPByLevel[2] = 300) ──
        public static readonly int[] XPByLevel =
        {
            0,        // [0] unused
            0,        // [1]  Level 1
            300,      // [2]  Level 2
            900,      // [3]  Level 3
            2700,     // [4]  Level 4
            6500,     // [5]  Level 5
            14000,    // [6]  Level 6
            23000,    // [7]  Level 7
            34000,    // [8]  Level 8
            48000,    // [9]  Level 9
            64000,    // [10] Level 10
            85000,    // [11] Level 11
            100000,   // [12] Level 12
            120000,   // [13] Level 13
            140000,   // [14] Level 14
            165000,   // [15] Level 15
            195000,   // [16] Level 16
            225000,   // [17] Level 17
            265000,   // [18] Level 18
            305000,   // [19] Level 19
            355000    // [20] Level 20
        };

        // ── Encounter difficulty thresholds (XP per character, by level) ──
        public static readonly int[] EasyXP =
        {
            0, 25, 50, 75, 125, 250, 300, 350, 450, 550,
            600, 800, 1000, 1100, 1250, 1400, 1600, 2000, 2100, 2400, 2800
        };

        public static readonly int[] MediumXP =
        {
            0, 50, 100, 150, 250, 500, 600, 750, 900, 1100,
            1200, 1600, 2000, 2200, 2500, 2800, 3200, 3900, 4200, 4900, 5700
        };

        public static readonly int[] HardXP =
        {
            0, 75, 150, 225, 375, 750, 900, 1100, 1400, 1600,
            1900, 2400, 3000, 3400, 3800, 4300, 4800, 5900, 6300, 7300, 8500
        };

        public static readonly int[] DeadlyXP =
        {
            0, 100, 200, 400, 500, 1100, 1400, 1700, 2100, 2400,
            2800, 3600, 4500, 5100, 5700, 6400, 7200, 8800, 9500, 10900, 12700
        };

        // ── CR to XP lookup ────────────────────────────────────────────────
        public static int CRToXP(float cr)
        {
            if (cr <= 0f)   return 10;
            if (cr <= 0.12f) return 25;
            if (cr <= 0.25f) return 50;
            if (cr <= 0.5f)  return 100;
            if (cr <= 1f)    return 200;
            if (cr <= 2f)    return 450;
            if (cr <= 3f)    return 700;
            if (cr <= 4f)    return 1100;
            if (cr <= 5f)    return 1800;
            if (cr <= 6f)    return 2300;
            if (cr <= 7f)    return 2900;
            if (cr <= 8f)    return 3900;
            if (cr <= 9f)    return 5000;
            if (cr <= 10f)   return 5900;
            if (cr <= 11f)   return 7200;
            if (cr <= 12f)   return 8400;
            if (cr <= 13f)   return 10000;
            if (cr <= 14f)   return 11500;
            if (cr <= 15f)   return 13000;
            if (cr <= 16f)   return 15000;
            if (cr <= 17f)   return 18000;
            if (cr <= 18f)   return 20000;
            if (cr <= 19f)   return 22000;
            if (cr <= 20f)   return 25000;
            if (cr <= 21f)   return 33000;
            if (cr <= 22f)   return 41000;
            if (cr <= 23f)   return 50000;
            if (cr <= 24f)   return 62000;
            return 155000; // CR 30
        }

        // ── Spell slots by class level ─────────────────────────────────────
        // [classLevel][spellLevel] — 0-indexed for class level in array, 1-indexed column
        // Full caster (Wizard/Cleric/Druid/Bard/Sorcerer) slots
        public static readonly int[,] FullCasterSlots = new int[20, 9]
        {
            // L1   L2  L3  L4  L5  L6  L7  L8  L9
            { 2,  0,  0,  0,  0,  0,  0,  0,  0 }, // class lvl 1
            { 3,  0,  0,  0,  0,  0,  0,  0,  0 }, // class lvl 2
            { 4,  2,  0,  0,  0,  0,  0,  0,  0 }, // class lvl 3
            { 4,  3,  0,  0,  0,  0,  0,  0,  0 }, // class lvl 4
            { 4,  3,  2,  0,  0,  0,  0,  0,  0 }, // class lvl 5
            { 4,  3,  3,  0,  0,  0,  0,  0,  0 }, // class lvl 6
            { 4,  3,  3,  1,  0,  0,  0,  0,  0 }, // class lvl 7
            { 4,  3,  3,  2,  0,  0,  0,  0,  0 }, // class lvl 8
            { 4,  3,  3,  3,  1,  0,  0,  0,  0 }, // class lvl 9
            { 4,  3,  3,  3,  2,  0,  0,  0,  0 }, // class lvl 10
            { 4,  3,  3,  3,  2,  1,  0,  0,  0 }, // class lvl 11
            { 4,  3,  3,  3,  2,  1,  0,  0,  0 }, // class lvl 12
            { 4,  3,  3,  3,  2,  1,  1,  0,  0 }, // class lvl 13
            { 4,  3,  3,  3,  2,  1,  1,  0,  0 }, // class lvl 14
            { 4,  3,  3,  3,  2,  1,  1,  1,  0 }, // class lvl 15
            { 4,  3,  3,  3,  2,  1,  1,  1,  0 }, // class lvl 16
            { 4,  3,  3,  3,  2,  1,  1,  1,  1 }, // class lvl 17
            { 4,  3,  3,  3,  3,  1,  1,  1,  1 }, // class lvl 18
            { 4,  3,  3,  3,  3,  2,  1,  1,  1 }, // class lvl 19
            { 4,  3,  3,  3,  3,  2,  2,  1,  1 }, // class lvl 20
        };

        /// <summary>Returns max spell slots of <paramref name="spellLevel"/> for a full caster at <paramref name="classLevel"/>.</summary>
        public static int GetFullCasterSlots(int classLevel, int spellLevel)
        {
            if (classLevel < 1 || classLevel > 20) return 0;
            if (spellLevel < 1 || spellLevel > 9) return 0;
            return FullCasterSlots[classLevel - 1, spellLevel - 1];
        }

        // ── Passive perception helper ──────────────────────────────────────
        /// <summary>Returns passive perception score (10 + WIS mod + proficiency if proficient).</summary>
        public static int PassivePerception(int wisdomScore, int proficiencyBonus, bool proficient)
            => 10 + AbilityModifier(wisdomScore) + (proficient ? proficiencyBonus : 0);

        // ── Spell save DC helper ────────────────────────────────────────────
        /// <summary>8 + proficiency bonus + spellcasting ability modifier.</summary>
        public static int SpellSaveDC(int proficiencyBonus, int castingAbilityScore)
            => 8 + proficiencyBonus + AbilityModifier(castingAbilityScore);
    }
}
