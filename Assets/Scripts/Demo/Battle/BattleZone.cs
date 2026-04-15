using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// A single NPC's battle zone. Centered on the NPC, radius = 2 × NPC Speed.
    /// Multiple zones combine additively via BattleZoneManager.
    /// </summary>
    public class BattleZone
    {
        public BattleCombatant Owner { get; }

        /// <summary>Radius in grid cells = 2 × owner's Speed stat.</summary>
        public int Radius { get; }

        /// <summary>World-space center, updated every frame to follow the owner.</summary>
        public Vector3 Center { get; private set; }

        public BattleZone(BattleCombatant owner)
        {
            Owner = owner;
            Radius = 2 * Mathf.Max(1, owner.Speed);
            Center = owner.SpawnWorldPos;
        }

        /// <summary>Update center to match owner's current world position.</summary>
        public void UpdateCenter(Vector3 worldPos)
        {
            Center = worldPos;
        }

        /// <summary>True if worldPos falls within this zone's circle.</summary>
        public bool Contains(Vector3 worldPos)
        {
            float dx = worldPos.x - Center.x;
            float dz = worldPos.z - Center.z;
            float r = Radius * BattleZoneManager.CellSize;
            return dx * dx + dz * dz <= r * r;
        }

        /// <summary>True if grid cell (x, y) relative to unified origin falls in this zone.</summary>
        public bool ContainsCell(int cellX, int cellY, Vector3 unifiedOrigin)
        {
            float worldX = unifiedOrigin.x + cellX * BattleZoneManager.CellSize + BattleZoneManager.CellSize * 0.5f;
            float worldZ = unifiedOrigin.z + cellY * BattleZoneManager.CellSize + BattleZoneManager.CellSize * 0.5f;
            float dx = worldX - Center.x;
            float dz = worldZ - Center.z;
            float r = Radius * BattleZoneManager.CellSize;
            return dx * dx + dz * dz <= r * r;
        }
    }
}
