using Unity.Burst;
using Unity.Collections;
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

        /// <summary>
        /// Burst-compatible overload that parses FixedString32Bytes directly.
        /// Supports formats like "2d8", "1d6+2", "3d4-1".
        /// </summary>
        [BurstCompile]
        public static void Parse(in FixedString32Bytes dice, out int count, out int sides, out int bonus)
        {
            count = 1; sides = 4; bonus = 0;
            int len = dice.Length;
            if (len == 0) return;

            // Find 'd' or 'D'
            int dIdx = -1;
            for (int i = 0; i < len; i++)
            {
                byte c = dice[i];
                if (c == (byte)'d' || c == (byte)'D') { dIdx = i; break; }
            }
            if (dIdx < 0) return;

            // Parse count before 'd'
            count = ParseInt(in dice, 0, dIdx);
            if (count <= 0) { count = 1; return; }

            // Find '+' or '-' after 'd'
            int modIdx = -1;
            int sign = 1;
            for (int i = dIdx + 1; i < len; i++)
            {
                byte c = dice[i];
                if (c == (byte)'+') { modIdx = i; sign = 1; break; }
                if (c == (byte)'-') { modIdx = i; sign = -1; break; }
            }

            if (modIdx >= 0)
            {
                sides = ParseInt(in dice, dIdx + 1, modIdx);
                bonus = ParseInt(in dice, modIdx + 1, len) * sign;
            }
            else
            {
                sides = ParseInt(in dice, dIdx + 1, len);
            }

            if (sides <= 0) sides = 4;
        }

        [BurstCompile]
        private static int ParseInt(in FixedString32Bytes s, int start, int end)
        {
            int result = 0;
            for (int i = start; i < end; i++)
            {
                byte c = s[i];
                if (c < (byte)'0' || c > (byte)'9') continue;
                result = result * 10 + (c - (byte)'0');
            }
            return result;
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
