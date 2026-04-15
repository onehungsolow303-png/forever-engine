using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Manages per-NPC BattleZones and computes a unified bounding-box grid.
    /// A cell is in-combat if it falls within ANY NPC's zone radius.
    /// Zones are dynamic: they follow their NPC and disappear on NPC death.
    /// </summary>
    public class BattleZoneManager : UnityEngine.MonoBehaviour
    {
        public const float CellSize = 1f;

        // ── Public state ──────────────────────────────────────────────────
        public List<BattleZone> Zones { get; private set; } = new();
        public BattleGrid Grid { get; private set; }
        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }
        public Vector3 Origin { get; private set; }

        // ── Boundary visuals ──────────────────────────────────────────────
        private static readonly Color ZoneColor = new(0.5f, 0.3f, 0.8f, 0.6f);
        private const float BoundaryY = 0.05f;
        private const int CircleSegments = 32;
        private readonly List<LineRenderer> _zoneRenderers = new();

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Initialize with a set of enemy combatants. Each gets their own zone.
        /// Call once when the encounter starts.
        /// </summary>
        public void Initialize(List<BattleCombatant> enemies, Vector3 playerWorldPos)
        {
            Zones.Clear();
            foreach (var enemy in enemies)
                Zones.Add(new BattleZone(enemy));

            RebuildUnifiedGrid(playerWorldPos);
            RebuildBoundaryVisuals();
        }

        /// <summary>
        /// Called each frame (or each turn) to update zone centers to match
        /// NPC positions and rebuild the grid if needed.
        /// </summary>
        public void UpdateZones(System.Func<BattleCombatant, Vector3> getWorldPos)
        {
            bool anyMoved = false;
            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                var zone = Zones[i];
                if (!zone.Owner.IsAlive)
                {
                    Zones.RemoveAt(i);
                    anyMoved = true;
                    continue;
                }
                Vector3 newCenter = getWorldPos(zone.Owner);
                if (Vector3.SqrMagnitude(newCenter - zone.Center) > 0.01f)
                {
                    zone.UpdateCenter(newCenter);
                    anyMoved = true;
                }
            }

            if (anyMoved && Zones.Count > 0)
            {
                RebuildUnifiedGrid(Zones[0].Center);
                RebuildBoundaryVisuals();
            }
        }

        /// <summary>
        /// Remove a specific NPC's zone (on death) and rebuild.
        /// </summary>
        public void RemoveZone(BattleCombatant owner, Vector3 playerWorldPos)
        {
            Zones.RemoveAll(z => z.Owner == owner);
            if (Zones.Count > 0)
            {
                RebuildUnifiedGrid(playerWorldPos);
                RebuildBoundaryVisuals();
            }
        }

        /// <summary>
        /// Add a new zone for a dynamically joining enemy.
        /// </summary>
        public void AddZone(BattleCombatant enemy, Vector3 playerWorldPos)
        {
            Zones.Add(new BattleZone(enemy));
            RebuildUnifiedGrid(playerWorldPos);
            RebuildBoundaryVisuals();
        }

        // ── Coordinate helpers ────────────────────────────────────────────

        public Vector3 GridToWorld(int x, int y)
        {
            return Origin + new Vector3(
                x * CellSize + CellSize * 0.5f,
                0f,
                y * CellSize + CellSize * 0.5f);
        }

        public (int x, int y) WorldToGrid(Vector3 pos)
        {
            Vector3 local = pos - Origin;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / CellSize), 0, GridWidth - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(local.z / CellSize), 0, GridHeight - 1);
            return (x, y);
        }

        /// <summary>True if the world position is inside ANY NPC's zone.</summary>
        public bool ContainsWorldPos(Vector3 pos)
        {
            foreach (var zone in Zones)
                if (zone.Contains(pos)) return true;
            return false;
        }

        /// <summary>True if the grid cell is inside ANY NPC's zone.</summary>
        public bool IsCellInZone(int x, int y)
        {
            foreach (var zone in Zones)
                if (zone.ContainsCell(x, y, Origin)) return true;
            return false;
        }

        /// <summary>Total radius of all zones combined (for join-range checks).</summary>
        public float TotalZoneRadius()
        {
            float total = 0f;
            foreach (var z in Zones) total += z.Radius * CellSize;
            return total;
        }

        public void Deactivate()
        {
            ClearBoundaryVisuals();
            Destroy(gameObject);
        }

        // ── Unified grid computation ──────────────────────────────────────

        private void RebuildUnifiedGrid(Vector3 playerWorldPos)
        {
            if (Zones.Count == 0) return;

            // Compute bounding box of all zone circles + player position
            float minX = playerWorldPos.x, maxX = playerWorldPos.x;
            float minZ = playerWorldPos.z, maxZ = playerWorldPos.z;

            foreach (var zone in Zones)
            {
                float r = zone.Radius * CellSize;
                float zMinX = zone.Center.x - r;
                float zMaxX = zone.Center.x + r;
                float zMinZ = zone.Center.z - r;
                float zMaxZ = zone.Center.z + r;

                if (zMinX < minX) minX = zMinX;
                if (zMaxX > maxX) maxX = zMaxX;
                if (zMinZ < minZ) minZ = zMinZ;
                if (zMaxZ > maxZ) maxZ = zMaxZ;
            }

            // Snap to integer grid with 1-cell padding
            Origin = new Vector3(
                Mathf.Floor(minX) - 1f,
                playerWorldPos.y,
                Mathf.Floor(minZ) - 1f);

            GridWidth = Mathf.CeilToInt(maxX - Origin.x) + 2;
            GridHeight = Mathf.CeilToInt(maxZ - Origin.z) + 2;

            // Clamp to reasonable limits
            GridWidth = Mathf.Clamp(GridWidth, 8, 64);
            GridHeight = Mathf.Clamp(GridHeight, 8, 64);

            // Build grid: a cell is walkable only if it's inside at least one zone
            Grid = new BattleGrid(GridWidth, GridHeight, 0); // seed=0, we override walkability

            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    bool inAnyZone = false;
                    foreach (var zone in Zones)
                    {
                        if (zone.ContainsCell(x, y, Origin))
                        {
                            inAnyZone = true;
                            break;
                        }
                    }
                    Grid.Walkable[y * GridWidth + x] = inAnyZone;
                }
            }

            // Physics scan: mark cells with colliders as non-walkable
            var halfExtents = new Vector3(0.45f, 0.5f, 0.45f);
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (!Grid.Walkable[y * GridWidth + x]) continue;

                    Vector3 cellCenter = GridToWorld(x, y) + Vector3.up * 0.5f;
                    var hits = Physics.OverlapBox(cellCenter, halfExtents);
                    foreach (var col in hits)
                    {
                        if (col.CompareTag("Player")) continue;
                        if (col.GetComponentInParent<Dungeon.DungeonNPC>() != null) continue;
                        Grid.Walkable[y * GridWidth + x] = false;
                        break;
                    }
                }
            }
        }

        // ── Boundary visuals (circles per NPC) ───────────────────────────

        private void ClearBoundaryVisuals()
        {
            foreach (var lr in _zoneRenderers)
                if (lr != null) Destroy(lr.gameObject);
            _zoneRenderers.Clear();
        }

        private void RebuildBoundaryVisuals()
        {
            ClearBoundaryVisuals();

            foreach (var zone in Zones)
            {
                var go = new GameObject($"ZoneBoundary_{zone.Owner.Name}");
                go.transform.SetParent(transform, worldPositionStays: false);

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = CircleSegments + 1;
                lr.loop = false;
                lr.useWorldSpace = true;

                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = ZoneColor;
                lr.material = mat;
                lr.startWidth = 0.06f;
                lr.endWidth = 0.06f;
                lr.startColor = ZoneColor;
                lr.endColor = ZoneColor;

                float r = zone.Radius * CellSize;
                for (int i = 0; i <= CircleSegments; i++)
                {
                    float angle = (2f * Mathf.PI * i) / CircleSegments;
                    float px = zone.Center.x + Mathf.Cos(angle) * r;
                    float pz = zone.Center.z + Mathf.Sin(angle) * r;
                    lr.SetPosition(i, new Vector3(px, BoundaryY, pz));
                }

                _zoneRenderers.Add(lr);
            }
        }
    }
}
