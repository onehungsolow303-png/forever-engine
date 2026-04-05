using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.CharacterCreation
{
    /// <summary>
    /// Serializable data bag produced by CharacterCreator and consumed by MapImporter/Bootstrap
    /// to spawn the player's ECS entity with a fully populated CharacterSheetComponent.
    /// </summary>
    [System.Serializable]
    public class CharacterData
    {
        public string characterName = "Adventurer";
        public string species       = "Human";
        public string className     = "Fighter";
        public string subclass      = "";
        public string background    = "Soldier";
        public int    level         = 1;

        // Ability scores
        public int strength     = 10;
        public int dexterity    = 10;
        public int constitution = 10;
        public int intelligence = 10;
        public int wisdom       = 10;
        public int charisma     = 10;

        // Derived stats (calculated by CharacterCreator.FinalizeCharacter)
        public int maxHP          = 10;
        public int currentHP      = 10;
        public int armorClass     = 10;
        public int proficiencyBonus = 2;
        public int speed          = 30; // feet

        // Proficiencies
        public string[] skillProficiencies        = System.Array.Empty<string>();
        public string[] savingThrowProficiencies  = System.Array.Empty<string>();
        public string[] weaponProficiencies       = System.Array.Empty<string>();
        public string[] armorProficiencies        = System.Array.Empty<string>();

        // Starting gear (item ids from ItemDatabase)
        public string[] startingItems = System.Array.Empty<string>();

        // Spellcasting
        public bool    isSpellcaster    = false;
        public string  spellcastingAbility = ""; // "INT", "WIS", "CHA"
        public int     spellSaveDC      = 8;
        public int     spellAttackBonus = 0;
        public string[] knownSpells     = System.Array.Empty<string>();

        // Starting spell slots (1-9)
        public int[] spellSlotsMax = new int[10]; // index 0 unused

        // Hit die
        public string hitDieType = "d8";
    }
}
