using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// AI behavior configuration — rewritten from pygame ai.py.
    /// Drives the AIDecisionJob to determine NPC actions each turn.
    /// </summary>
    public enum AIType : byte
    {
        Static = 0,     // Does not move or act
        Chase = 1,      // Greedy pursue player (original pygame behavior)
        Patrol = 2,     // Follow waypoint path, chase if player spotted
        Guard = 3,      // Stay near post, chase if player enters range
        Flee = 4,       // Run from threats
        Wander = 5,     // Random movement
        Scripted = 6    // Controlled by quest/dialogue system
    }

    public struct AIBehaviorComponent : IComponentData
    {
        public AIType Type;
        public float Aggression;         // 0.0 = passive, 1.0 = always attacks
        public int DetectRange;          // Tiles to notice player
        public int LeashRange;           // Max tiles from spawn before returning
        public int SpawnX;               // Home position for guard/patrol
        public int SpawnY;
        public int PatrolIndex;          // Current waypoint index
        public bool Alerted;             // Has spotted player this encounter
    }
}
