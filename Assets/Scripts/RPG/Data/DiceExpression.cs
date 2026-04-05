using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Data
{
    [System.Serializable]
    public struct DiceExpression
    {
        public int Count;
        public DieType Die;
        public int Bonus;

        public DiceExpression(int count, DieType die, int bonus = 0)
        {
            Count = count;
            Die = die;
            Bonus = bonus;
        }

        public static DiceExpression Parse(string expression)
        {
            DiceRoller.Parse(expression, out int count, out int sides, out int bonus);
            return new DiceExpression(count, (DieType)sides, bonus);
        }

        public int Roll(ref uint seed)
        {
            return DiceRoller.Roll(Count, (int)Die, Bonus, ref seed);
        }

        public int RollWithAdvantage(ref uint seed)
        {
            int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);
            int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
            return (roll1 > roll2 ? roll1 : roll2) + Bonus;
        }

        public int RollWithDisadvantage(ref uint seed)
        {
            int roll1 = DiceRoller.Roll(1, 20, 0, ref seed);
            int roll2 = DiceRoller.Roll(1, 20, 0, ref seed);
            return (roll1 < roll2 ? roll1 : roll2) + Bonus;
        }

        public int CriticalDamage(ref uint seed)
        {
            // Double the dice count, keep bonus once
            return DiceRoller.Roll(Count * 2, (int)Die, Bonus, ref seed);
        }

        public override string ToString()
        {
            if (Bonus > 0) return $"{Count}d{(int)Die}+{Bonus}";
            if (Bonus < 0) return $"{Count}d{(int)Die}{Bonus}";
            return $"{Count}d{(int)Die}";
        }

        public static DiceExpression None => new DiceExpression(0, DieType.D4, 0);
    }
}
