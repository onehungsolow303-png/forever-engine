using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Data
{
    [System.Serializable]
    public struct AbilityScores
    {
        public int Strength;
        public int Dexterity;
        public int Constitution;
        public int Intelligence;
        public int Wisdom;
        public int Charisma;

        public AbilityScores(int str, int dex, int con, int intel, int wis, int cha)
        {
            Strength = str;
            Dexterity = dex;
            Constitution = con;
            Intelligence = intel;
            Wisdom = wis;
            Charisma = cha;
        }

        public int GetScore(Ability ability)
        {
            switch (ability)
            {
                case Ability.STR: return Strength;
                case Ability.DEX: return Dexterity;
                case Ability.CON: return Constitution;
                case Ability.INT: return Intelligence;
                case Ability.WIS: return Wisdom;
                case Ability.CHA: return Charisma;
                default: return 10;
            }
        }

        public int GetModifier(Ability ability)
        {
            return DiceRoller.AbilityModifier(GetScore(ability));
        }

        public AbilityScores SetScore(Ability ability, int value)
        {
            var result = this;
            switch (ability)
            {
                case Ability.STR: result.Strength = value; break;
                case Ability.DEX: result.Dexterity = value; break;
                case Ability.CON: result.Constitution = value; break;
                case Ability.INT: result.Intelligence = value; break;
                case Ability.WIS: result.Wisdom = value; break;
                case Ability.CHA: result.Charisma = value; break;
            }
            return result;
        }

        public AbilityScores WithBonus(Ability ability, int bonus)
        {
            return SetScore(ability, GetScore(ability) + bonus);
        }

        public static AbilityScores Default => new AbilityScores(10, 10, 10, 10, 10, 10);
    }
}
