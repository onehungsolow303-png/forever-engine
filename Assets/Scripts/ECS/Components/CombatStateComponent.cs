using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Per-entity combat state — rewritten from pygame combat.py turn tracking.
    /// Tracks token type, turn resources, and faction for the CombatSystem.
    /// </summary>
    public enum TokenType : byte
    {
        Player = 0,
        Enemy = 1,
        NPC = 2,
        Neutral = 3
    }

    public struct CombatStateComponent : IComponentData
    {
        public TokenType TokenType;
        public FixedString32Bytes Faction;

        // Per-turn resources (reset by GameStateSystem at turn start)
        public int MovementRemaining;
        public bool HasAction;
        public bool Alive;

        // Initiative (rolled at combat start by CombatSystem)
        public int InitiativeRoll;
        public int InitiativeBonus;
    }
}
