using NUnit.Framework;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.Tests
{
    public class StatsComponentTests
    {
        [Test]
        public void Default_AllScores10()
        {
            var stats = StatsComponent.Default;
            Assert.AreEqual(10, stats.Strength);
            Assert.AreEqual(10, stats.Dexterity);
            Assert.AreEqual(10, stats.AC);
            Assert.AreEqual(10, stats.HP);
            Assert.AreEqual(6, stats.Speed);
        }

        [Test]
        public void Modifiers_Correct()
        {
            var stats = StatsComponent.Default;
            stats.Strength = 16;
            stats.Dexterity = 8;
            Assert.AreEqual(3, stats.StrMod);
            Assert.AreEqual(-1, stats.DexMod);
        }

        [Test]
        public void HPPercent_HalfHealth()
        {
            var stats = StatsComponent.Default;
            stats.HP = 5;
            stats.MaxHP = 10;
            Assert.AreEqual(0.5f, stats.HPPercent, 0.001f);
        }

        [Test]
        public void HPPercent_Dead()
        {
            var stats = StatsComponent.Default;
            stats.HP = 0;
            stats.MaxHP = 10;
            Assert.AreEqual(0f, stats.HPPercent, 0.001f);
        }

        [Test]
        public void HPPercent_ZeroMaxHP_ReturnsZero()
        {
            var stats = StatsComponent.Default;
            stats.HP = 0;
            stats.MaxHP = 0;
            Assert.AreEqual(0f, stats.HPPercent, 0.001f);
        }
    }
}
