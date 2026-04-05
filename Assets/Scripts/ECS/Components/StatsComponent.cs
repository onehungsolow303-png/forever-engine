using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// D&D 5e stat block — rewritten from pygame entities.py Creature dataclass.
    /// Stores all combat-relevant stats as pure ECS data for Burst-compiled jobs.
    /// </summary>
    public struct StatsComponent : IComponentData
    {
        // Ability scores (D&D standard: 3-20 range, 10 = average)
        public int Strength;
        public int Dexterity;
        public int Constitution;
        public int Intelligence;
        public int Wisdom;
        public int Charisma;

        // Combat stats
        public int AC;
        public int HP;
        public int MaxHP;
        public int Speed;          // Tiles per turn

        // Attack
        public int AtkDiceCount;   // e.g., 2 for "2d8+3"
        public int AtkDiceSides;   // e.g., 8 for "2d8+3"
        public int AtkDiceBonus;   // e.g., 3 for "2d8+3"

        // Derived (cached, recalculated on stat change)
        public int StrMod => Utility.DiceRoller.AbilityModifier(Strength);
        public int DexMod => Utility.DiceRoller.AbilityModifier(Dexterity);
        public int ConMod => Utility.DiceRoller.AbilityModifier(Constitution);
        public int IntMod => Utility.DiceRoller.AbilityModifier(Intelligence);
        public int WisMod => Utility.DiceRoller.AbilityModifier(Wisdom);
        public int ChaMod => Utility.DiceRoller.AbilityModifier(Charisma);

        public float HPPercent => MaxHP > 0 ? (float)HP / MaxHP : 0f;

        public static StatsComponent Default => new StatsComponent
        {
            Strength = 10, Dexterity = 10, Constitution = 10,
            Intelligence = 10, Wisdom = 10, Charisma = 10,
            AC = 10, HP = 10, MaxHP = 10, Speed = 6,
            AtkDiceCount = 1, AtkDiceSides = 4, AtkDiceBonus = 0
        };
    }
}
