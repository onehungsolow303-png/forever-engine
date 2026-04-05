using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.Tests
{
    public class CombatJobTests
    {
        [Test]
        public void Attack_HitAndDamage()
        {
            var results = new NativeArray<CombatResult>(1, Allocator.TempJob);

            var job = new CombatJob
            {
                AttackerStrMod = 3,
                DefenderAC = 10,
                AtkDiceCount = 1,
                AtkDiceSides = 8,
                AtkDiceBonus = 3,
                Seed = 99999,
                Results = results
            };
            job.Schedule().Complete();

            var r = results[0];
            Assert.GreaterOrEqual(r.AttackRoll, 1);
            Assert.LessOrEqual(r.AttackRoll, 20);
            if (r.Hit)
            {
                Assert.GreaterOrEqual(r.Damage, 4);
                Assert.LessOrEqual(r.Damage, 11);
            }
            else
            {
                Assert.AreEqual(0, r.Damage);
            }

            results.Dispose();
        }

        [Test]
        public void Attack_NatOne_AlwaysMisses()
        {
            var results = new NativeArray<CombatResult>(1, Allocator.TempJob);
            uint seed = 1;
            for (uint s = 1; s < 100000; s++)
            {
                uint test = s;
                test ^= test << 13;
                test ^= test >> 17;
                test ^= test << 5;
                if ((int)(test % 20) + 1 == 1) { seed = s; break; }
            }

            var job = new CombatJob
            {
                AttackerStrMod = 10,
                DefenderAC = 1,
                AtkDiceCount = 1, AtkDiceSides = 8, AtkDiceBonus = 0,
                Seed = seed,
                Results = results
            };
            job.Schedule().Complete();

            Assert.AreEqual(1, results[0].AttackRoll);
            Assert.IsFalse(results[0].Hit);
            Assert.AreEqual(0, results[0].Damage);

            results.Dispose();
        }

        [Test]
        public void Attack_NatTwenty_AlwaysHits()
        {
            var results = new NativeArray<CombatResult>(1, Allocator.TempJob);
            uint seed = 1;
            for (uint s = 1; s < 100000; s++)
            {
                uint test = s;
                test ^= test << 13;
                test ^= test >> 17;
                test ^= test << 5;
                if ((int)(test % 20) + 1 == 20) { seed = s; break; }
            }

            var job = new CombatJob
            {
                AttackerStrMod = -5,
                DefenderAC = 30,
                AtkDiceCount = 1, AtkDiceSides = 6, AtkDiceBonus = 0,
                Seed = seed,
                Results = results
            };
            job.Schedule().Complete();

            Assert.AreEqual(20, results[0].AttackRoll);
            Assert.IsTrue(results[0].Hit);
            Assert.Greater(results[0].Damage, 0);

            results.Dispose();
        }
    }
}
