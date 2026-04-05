using NUnit.Framework;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.Tests
{
    public class DiceRollerTests
    {
        [Test]
        public void ParseDice_Simple_1d6()
        {
            DiceRoller.Parse("1d6", out int count, out int sides, out int bonus);
            Assert.AreEqual(1, count);
            Assert.AreEqual(6, sides);
            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void ParseDice_WithBonus_2d8Plus3()
        {
            DiceRoller.Parse("2d8+3", out int count, out int sides, out int bonus);
            Assert.AreEqual(2, count);
            Assert.AreEqual(8, sides);
            Assert.AreEqual(3, bonus);
        }

        [Test]
        public void ParseDice_WithPenalty_1d4Minus1()
        {
            DiceRoller.Parse("1d4-1", out int count, out int sides, out int bonus);
            Assert.AreEqual(1, count);
            Assert.AreEqual(4, sides);
            Assert.AreEqual(-1, bonus);
        }

        [Test]
        public void ParseDice_Uppercase_3D10Plus5()
        {
            DiceRoller.Parse("3D10+5", out int count, out int sides, out int bonus);
            Assert.AreEqual(3, count);
            Assert.AreEqual(10, sides);
            Assert.AreEqual(5, bonus);
        }

        [Test]
        public void ParseDice_Invalid_ReturnsDefaults()
        {
            DiceRoller.Parse("garbage", out int count, out int sides, out int bonus);
            Assert.AreEqual(1, count);
            Assert.AreEqual(4, sides);
            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void Roll_1d6_InRange()
        {
            uint seed = 12345;
            for (int i = 0; i < 100; i++)
            {
                int result = DiceRoller.Roll(1, 6, 0, ref seed);
                Assert.GreaterOrEqual(result, 1);
                Assert.LessOrEqual(result, 6);
            }
        }

        [Test]
        public void Roll_2d6Plus3_InRange()
        {
            uint seed = 54321;
            for (int i = 0; i < 100; i++)
            {
                int result = DiceRoller.Roll(2, 6, 3, ref seed);
                Assert.GreaterOrEqual(result, 5);
                Assert.LessOrEqual(result, 15);
            }
        }

        [Test]
        public void AbilityModifier_Standard()
        {
            Assert.AreEqual(-5, DiceRoller.AbilityModifier(1));
            Assert.AreEqual(-1, DiceRoller.AbilityModifier(8));
            Assert.AreEqual(0, DiceRoller.AbilityModifier(10));
            Assert.AreEqual(0, DiceRoller.AbilityModifier(11));
            Assert.AreEqual(2, DiceRoller.AbilityModifier(14));
            Assert.AreEqual(5, DiceRoller.AbilityModifier(20));
        }
    }
}
