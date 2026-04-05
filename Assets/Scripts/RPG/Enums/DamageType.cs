namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum DamageType
    {
        None        = 0,
        Slashing    = 1 << 0,
        Piercing    = 1 << 1,
        Bludgeoning = 1 << 2,
        Fire        = 1 << 3,
        Cold        = 1 << 4,
        Lightning   = 1 << 5,
        Thunder     = 1 << 6,
        Poison      = 1 << 7,
        Acid        = 1 << 8,
        Necrotic    = 1 << 9,
        Radiant     = 1 << 10,
        Psychic     = 1 << 11,
        Force       = 1 << 12
    }
}
