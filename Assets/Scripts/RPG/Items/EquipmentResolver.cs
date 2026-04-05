using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Items
{
    /// <summary>
    /// Static equipment resolver. Calculates AC from equipped armor, shield, ability modifiers,
    /// and class features (Monk/Barbarian Unarmored Defense).
    ///
    /// AC Rules:
    /// - No armor: 10 + DEX mod
    /// - Light armor: base AC + DEX mod
    /// - Medium armor: base AC + DEX mod (max +2)
    /// - Heavy armor: base AC (flat, no DEX)
    /// - Shield: +2
    /// - Magic bonuses added on top
    /// - Monk Unarmored Defense: 10 + DEX + WIS (no armor, no shield)
    /// - Barbarian Unarmored Defense: 10 + DEX + CON (no armor, shield OK)
    /// </summary>
    public static class EquipmentResolver
    {
        /// <summary>
        /// Calculate AC for a character given their equipment and abilities.
        /// </summary>
        /// <param name="abilities">Character's effective ability scores.</param>
        /// <param name="equippedArmor">Equipped body armor (null if none).</param>
        /// <param name="equippedShield">Equipped shield (null if none).</param>
        /// <param name="hasMonkUnarmored">Whether character has Monk Unarmored Defense.</param>
        /// <param name="hasBarbarianUnarmored">Whether character has Barbarian Unarmored Defense.</param>
        /// <param name="magicACBonus">Bonus AC from magic items (non-armor/shield).</param>
        /// <returns>Calculated AC value.</returns>
        public static int CalculateAC(
            AbilityScores abilities,
            ArmorData equippedArmor,
            ArmorData equippedShield,
            bool hasMonkUnarmored,
            bool hasBarbarianUnarmored,
            int magicACBonus = 0)
        {
            int dexMod = abilities.GetModifier(Ability.DEX);
            int ac;

            if (equippedArmor == null)
            {
                // No armor — check for Unarmored Defense
                if (hasMonkUnarmored)
                {
                    // Monk: 10 + DEX + WIS (no shield allowed for Monk unarmored)
                    int wisMod = abilities.GetModifier(Ability.WIS);
                    ac = 10 + dexMod + wisMod;
                    // Monk unarmored defense doesn't benefit from shields
                    ac += magicACBonus;
                    return ac;
                }
                else if (hasBarbarianUnarmored)
                {
                    // Barbarian: 10 + DEX + CON (shield OK)
                    int conMod = abilities.GetModifier(Ability.CON);
                    ac = 10 + dexMod + conMod;
                }
                else
                {
                    // Standard: 10 + DEX
                    ac = 10 + dexMod;
                }
            }
            else
            {
                switch (equippedArmor.Type)
                {
                    case ArmorType.Light:
                        // Light armor: base + full DEX
                        ac = equippedArmor.BaseAC + dexMod;
                        break;

                    case ArmorType.Medium:
                        // Medium armor: base + DEX (max +2)
                        int cappedDex = dexMod > 2 ? 2 : dexMod;
                        ac = equippedArmor.BaseAC + cappedDex;
                        break;

                    case ArmorType.Heavy:
                        // Heavy armor: flat base AC
                        ac = equippedArmor.BaseAC;
                        break;

                    default:
                        ac = 10 + dexMod;
                        break;
                }

                // Add armor magic bonus
                ac += equippedArmor.MagicBonus;
            }

            // Add shield bonus
            if (equippedShield != null && equippedShield.Type == ArmorType.Shield)
            {
                ac += equippedShield.BaseAC; // Typically 2 for a standard shield
                ac += equippedShield.MagicBonus;
            }

            // Add magic item AC bonus (from rings, cloaks, etc.)
            ac += magicACBonus;

            return ac;
        }
    }
}
