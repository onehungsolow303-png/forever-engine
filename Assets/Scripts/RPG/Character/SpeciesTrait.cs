namespace ForeverEngine.RPG.Character
{
    [System.Flags]
    public enum SpeciesTrait
    {
        None                = 0,
        FeyAncestry         = 1 << 0,   // Advantage on saves vs charmed, immune to magical sleep
        Trance              = 1 << 1,   // 4-hour rest instead of 8
        DarkvisionStandard  = 1 << 2,   // 60ft darkvision
        DarkvisionSuperior  = 1 << 3,   // 120ft darkvision
        DwarvenResilience   = 1 << 4,   // Advantage on poison saves, resistance to poison damage
        Stonecunning        = 1 << 5,   // Double proficiency on stone History checks
        Lucky               = 1 << 6,   // Reroll natural 1s on attacks/ability/saves
        BraveHalfling       = 1 << 7,   // Advantage on saves vs frightened
        NaturallyStealthy   = 1 << 8,   // Hide behind Medium or larger creatures
        StoutResilience     = 1 << 9,   // Advantage on poison saves, resistance to poison damage
        Relentless          = 1 << 10,  // Drop to 1 HP instead of 0 once per rest
        SavageAttacks       = 1 << 11,  // Extra crit die on melee
        BreathWeapon        = 1 << 12,  // Dragonborn breath attack
        DamageResistance    = 1 << 13,  // Resistance to one damage type (by draconic ancestry)
        HellishResistance   = 1 << 14,  // Fire resistance
        InfernalLegacy      = 1 << 15,  // Innate spellcasting (Tiefling)
        GnomeCunning        = 1 << 16,  // Advantage on INT/WIS/CHA saves vs magic
        Tinker              = 1 << 17,  // Create small clockwork devices
        MinorIllusion       = 1 << 18,  // Know Minor Illusion cantrip
        Shapechanger        = 1 << 19,  // Alter appearance at will
        MaskOfTheWild       = 1 << 20,  // Hide in light natural obscuration
        SunlightSensitivity = 1 << 21,  // Disadvantage in direct sunlight
        DrowMagic           = 1 << 22,  // Innate Drow spellcasting
        DwarvenToughness    = 1 << 23,  // +1 HP per level
        ExtraSkill          = 1 << 24,  // Extra skill proficiency (Human/Half-Elf)
        ExtraFeat           = 1 << 25,  // Bonus feat at level 1 (Human variant)
        DwarvenArmorTraining = 1 << 26, // Light and Medium armor proficiency (Mountain Dwarf)
    }
}
