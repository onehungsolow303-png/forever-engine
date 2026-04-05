using ForeverEngine.ECS.Utility;

namespace ForeverEngine.Demo.Battle
{
    public class BattleCombatant
    {
        public string Name;
        public int X, Y;
        public int HP, MaxHP, AC;
        public int Strength, Dexterity, Speed;
        public int AtkCount, AtkSides, AtkBonus;
        public string Behavior;
        public bool IsPlayer;
        public bool IsAlive => HP > 0;
        public int MovementRemaining;
        public bool HasAction;
        public int InitiativeRoll;

        public void RollInitiative(ref uint seed)
        {
            InitiativeRoll = DiceRoller.Roll(1, 20, 0, ref seed) + DiceRoller.AbilityModifier(Dexterity);
        }

        public int RollAttack(ref uint seed) => DiceRoller.Roll(1, 20, 0, ref seed) + DiceRoller.AbilityModifier(Strength);
        public int RollDamage(ref uint seed) => DiceRoller.Roll(AtkCount, AtkSides, AtkBonus, ref seed);

        public void TakeDamage(int amount) { HP = System.Math.Max(0, HP - amount); }
        public void StartTurn() { MovementRemaining = Speed; HasAction = true; }

        public static BattleCombatant FromPlayer(PlayerData data)
        {
            DiceRoller.Parse(data.AttackDice, out int c, out int s, out int b);
            return new BattleCombatant
            {
                Name = "Wanderer", X = 1, Y = 1, IsPlayer = true,
                HP = data.HP, MaxHP = data.MaxHP, AC = data.AC,
                Strength = data.Strength, Dexterity = data.Dexterity, Speed = data.Speed,
                AtkCount = c, AtkSides = s, AtkBonus = b, Behavior = "player"
            };
        }

        public static BattleCombatant FromEnemy(Encounters.EnemyDef def, int x, int y)
        {
            DiceRoller.Parse(def.AtkDice, out int c, out int s, out int b);
            return new BattleCombatant
            {
                Name = def.Name, X = x, Y = y, IsPlayer = false,
                HP = def.HP, MaxHP = def.HP, AC = def.AC,
                Strength = def.Str, Dexterity = def.Dex, Speed = def.Spd,
                AtkCount = c, AtkSides = s, AtkBonus = b, Behavior = def.Behavior
            };
        }
    }
}
