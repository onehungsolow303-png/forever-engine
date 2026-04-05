namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum ClassFlag
    {
        None      = 0,
        Warrior   = 1 << 0,
        Wizard    = 1 << 1,
        Rogue     = 1 << 2,
        Cleric    = 1 << 3,
        Druid     = 1 << 4,
        Bard      = 1 << 5,
        Ranger    = 1 << 6,
        Paladin   = 1 << 7,
        Sorcerer  = 1 << 8,
        Warlock   = 1 << 9,
        Monk      = 1 << 10,
        Barbarian = 1 << 11
    }
}
