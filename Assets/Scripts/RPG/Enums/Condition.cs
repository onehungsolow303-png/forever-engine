namespace ForeverEngine.RPG.Enums
{
    [System.Flags]
    public enum Condition
    {
        None          = 0,
        Blinded       = 1 << 0,
        Charmed       = 1 << 1,
        Deafened      = 1 << 2,
        Frightened    = 1 << 3,
        Grappled      = 1 << 4,
        Incapacitated = 1 << 5,
        Invisible     = 1 << 6,
        Paralyzed     = 1 << 7,
        Petrified     = 1 << 8,
        Poisoned      = 1 << 9,
        Prone         = 1 << 10,
        Restrained    = 1 << 11,
        Stunned       = 1 << 12,
        Unconscious   = 1 << 13,
        Dodging       = 1 << 14,

        // Derived composites (not stored, used for queries)
        CantAct               = Incapacitated | Stunned | Paralyzed | Petrified | Unconscious,
        AttacksHaveAdvantage  = Blinded | Restrained | Stunned | Paralyzed | Unconscious,
        MeleeAutoCrit         = Paralyzed | Unconscious
    }
}
