namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum MetamagicType
    {
        None       = 0,
        Twinned    = 1 << 0,
        Quickened  = 1 << 1,
        Subtle     = 1 << 2,
        Empowered  = 1 << 3,
        Heightened = 1 << 4,
        Careful    = 1 << 5,
        Distant    = 1 << 6
    }
}
