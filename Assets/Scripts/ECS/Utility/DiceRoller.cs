using Unity.Burst;
using Unity.Mathematics;

namespace ForeverEngine.ECS.Utility
{
    [BurstCompile]
    public static class DiceRoller
    {
        public static void Parse(string dice, out int count, out int sides, out int bonus)
        {
            count = 1; sides = 4; bonus = 0;
            if (string.IsNullOrEmpty(dice)) return;

            string s = dice.ToLowerInvariant().Trim();
            int dIdx = s.IndexOf('d');
            if (dIdx < 0) return;

            if (!int.TryParse(s.Substring(0, dIdx), out count)) { count = 1; return; }

            string rest = s.Substring(dIdx + 1);
            int plusIdx = rest.IndexOf('+');
            int minusIdx = rest.IndexOf('-');
            int modIdx = plusIdx >= 0 ? plusIdx : minusIdx;

            if (modIdx >= 0)
            {
                if (!int.TryParse(rest.Substring(0, modIdx), out sides)) sides = 4;
                if (!int.TryParse(rest.Substring(modIdx), out bonus)) bonus = 0;
            }
            else
            {
                if (!int.TryParse(rest, out sides)) sides = 4;
            }
        }

        [BurstCompile]
        public static int Roll(int count, int sides, int bonus, ref uint seed)
        {
            int total = bonus;
            for (int i = 0; i < count; i++)
            {
                seed = Xorshift32(seed);
                total += (int)(seed % (uint)sides) + 1;
            }
            return total;
        }

        [BurstCompile]
        public static int AbilityModifier(int score)
        {
            // D&D floor division: (1-10)/2 = -5, not -4
            int diff = score - 10;
            return diff >= 0 ? diff / 2 : (diff - 1) / 2;
        }

        [BurstCompile]
        public static uint Xorshift32(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
