namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum WeaponProperty
    {
        None       = 0,
        Finesse    = 1 << 0,
        Heavy      = 1 << 1,
        Light      = 1 << 2,
        Thrown     = 1 << 3,
        TwoHanded  = 1 << 4,
        Versatile  = 1 << 5,
        Reach      = 1 << 6,
        Loading    = 1 << 7,
        Ammunition = 1 << 8
    }
}
