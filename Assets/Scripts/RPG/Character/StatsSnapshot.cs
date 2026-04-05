namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Lightweight struct for ECS bridge. Maps CharacterSheet data to a format
    /// compatible with the existing StatsComponent.
    /// </summary>
    public struct StatsSnapshot
    {
        public int Strength;
        public int Dexterity;
        public int Constitution;
        public int Intelligence;
        public int Wisdom;
        public int Charisma;
        public int AC;
        public int HP;
        public int MaxHP;
        public int Speed;
        public int AtkDiceCount;
        public int AtkDiceSides;
        public int AtkDiceBonus;

        /// <summary>
        /// Convert to an ECS StatsComponent.
        /// </summary>
        public ForeverEngine.ECS.Components.StatsComponent ToStatsComponent()
        {
            return new ForeverEngine.ECS.Components.StatsComponent
            {
                Strength = Strength,
                Dexterity = Dexterity,
                Constitution = Constitution,
                Intelligence = Intelligence,
                Wisdom = Wisdom,
                Charisma = Charisma,
                AC = AC,
                HP = HP,
                MaxHP = MaxHP,
                Speed = Speed,
                AtkDiceCount = AtkDiceCount,
                AtkDiceSides = AtkDiceSides,
                AtkDiceBonus = AtkDiceBonus
            };
        }
    }
}
