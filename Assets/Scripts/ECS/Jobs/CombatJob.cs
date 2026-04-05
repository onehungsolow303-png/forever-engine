using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.ECS.Jobs
{
    [BurstCompile]
    public struct CombatJob : IJob
    {
        [ReadOnly] public int AttackerStrMod;
        [ReadOnly] public int DefenderAC;
        [ReadOnly] public int AtkDiceCount;
        [ReadOnly] public int AtkDiceSides;
        [ReadOnly] public int AtkDiceBonus;
        public uint Seed;

        [WriteOnly] public NativeArray<CombatResult> Results;

        public void Execute()
        {
            int attackRoll = DiceRoller.Roll(1, 20, 0, ref Seed);
            bool hit;

            if (attackRoll == 1)
                hit = false;
            else if (attackRoll == 20)
                hit = true;
            else
                hit = (attackRoll + AttackerStrMod) >= DefenderAC;

            int damage = 0;
            if (hit)
            {
                damage = DiceRoller.Roll(AtkDiceCount, AtkDiceSides, AtkDiceBonus, ref Seed);
                if (damage < 1) damage = 1;
            }

            Results[0] = new CombatResult
            {
                AttackRoll = attackRoll,
                Hit = hit,
                Damage = damage
            };
        }
    }

    public struct CombatResult
    {
        public int AttackRoll;
        public bool Hit;
        public int Damage;
    }
}
