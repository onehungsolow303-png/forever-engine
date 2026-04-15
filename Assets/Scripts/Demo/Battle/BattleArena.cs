using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.Demo.Dungeon;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// ONE shared battle zone for an entire encounter, replacing the broken per-enemy
    /// BattleZone system that produced mismatched coordinate systems.
    ///
    /// Grid size = 2 × playerSpeed × max(1, enemyCount), clamped [12, 32].
    /// Origin = centroid of all combatants minus half grid size (integer-snapped).
    ///
    /// Lifecycle: Initialize → (optional Recalculate when enemies die) → Deactivate.
    /// Deactivate destroys the GameObject; callers must null their reference.
    /// </summary>
    public class BattleArena : UnityEngine.MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────
        public const float CellSize = 1f;
        private const int MinGridSize = 12;
        private const int MaxGridSize = 32;

        // ── Boundary visual constants ─────────────────────────────────────────
        private static readonly Color BoundaryColor = new Color(0.8f, 0.6f, 1.0f, 0.85f);
        private const float BoundaryWidth = 0.08f;
        private const float BoundaryY = 0.05f;

        // ── Public state ──────────────────────────────────────────────────────
        /// <summary>Current dynamic grid size (square). Recalculates on enemy death.</summary>
        public int GridSize { get; private set; }

        /// <summary>World-space SW corner of the grid (integer-snapped).</summary>
        public Vector3 Origin { get; private set; }

        /// <summary>Walkability grid, rebuilt on Initialize and Recalculate.</summary>
        public BattleGrid Grid { get; private set; }

        // ── Private state ─────────────────────────────────────────────────────
        private LineRenderer _boundary;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Set up the arena from the starting world positions of all combatants.
        /// Call once when the encounter begins.
        /// </summary>
        /// <param name="playerWorldPos">Player's world-space position.</param>
        /// <param name="enemies">List of enemy combatants (SpawnWorldPos must be set).</param>
        /// <param name="playerSpeed">Player's Speed stat — drives grid scaling.</param>
        public void Initialize(Vector3 playerWorldPos, List<BattleCombatant> enemies, int playerSpeed)
        {
            var allPositions = GatherPositions(playerWorldPos, enemies);
            GridSize = ComputeGridSize(playerSpeed, enemies.Count);
            Origin = ComputeOrigin(allPositions, GridSize);

            RebuildGrid();
            ScanGeometry();
            BuildBoundary();
        }

        /// <summary>
        /// Recalculate grid size and origin after one or more enemies have died.
        /// Living combatants are re-centered; any combatant positions outside the
        /// shrunken boundary are clamped to the nearest valid cell.
        /// </summary>
        /// <param name="aliveCombatants">All still-living combatants (player + enemies).</param>
        /// <param name="playerSpeed">Current player Speed stat.</param>
        public void Recalculate(List<BattleCombatant> aliveCombatants, int playerSpeed)
        {
            int aliveEnemyCount = 0;
            var positions = new List<Vector3>(aliveCombatants.Count);

            foreach (var c in aliveCombatants)
            {
                positions.Add(c.SpawnWorldPos);
                if (!c.IsPlayer) aliveEnemyCount++;
            }

            GridSize = ComputeGridSize(playerSpeed, aliveEnemyCount);
            Origin = ComputeOrigin(positions, GridSize);

            RebuildGrid();
            ScanGeometry();

            // Clamp grid-coordinate positions so combatants can't be outside the zone.
            foreach (var c in aliveCombatants)
            {
                var (gx, gy) = WorldToGrid(c.SpawnWorldPos);
                c.X = gx;
                c.Y = gy;
            }

            UpdateBoundaryPositions();
        }

        /// <summary>
        /// Tear down the arena and destroy the GameObject.
        /// Caller must null their reference after this call.
        /// </summary>
        public void Deactivate()
        {
            Destroy(gameObject);
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        /// <summary>Returns the world-space center of grid cell (x, y).</summary>
        public Vector3 GridToWorld(int x, int y)
        {
            return Origin + new Vector3(
                x * CellSize + CellSize * 0.5f,
                0f,
                y * CellSize + CellSize * 0.5f);
        }

        /// <summary>
        /// Maps a world position back to grid coordinates, clamped to [0, GridSize-1].
        /// </summary>
        public (int x, int y) WorldToGrid(Vector3 pos)
        {
            Vector3 local = pos - Origin;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / CellSize), 0, GridSize - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(local.z / CellSize), 0, GridSize - 1);
            return (x, y);
        }

        /// <summary>
        /// Returns true if <paramref name="pos"/> falls within the arena's world footprint.
        /// </summary>
        public bool ContainsWorldPos(Vector3 pos)
        {
            float totalSize = GridSize * CellSize;
            float dx = pos.x - Origin.x;
            float dz = pos.z - Origin.z;
            return dx >= 0f && dx < totalSize && dz >= 0f && dz < totalSize;
        }

        // ── Grid sizing ───────────────────────────────────────────────────────

        private static int ComputeGridSize(int playerSpeed, int enemyCount)
        {
            int raw = 2 * playerSpeed * Mathf.Max(1, enemyCount);
            return Mathf.Clamp(raw, MinGridSize, MaxGridSize);
        }

        private static Vector3 ComputeOrigin(List<Vector3> positions, int gridSize)
        {
            if (positions == null || positions.Count == 0)
                return Vector3.zero;

            // Centroid of all combatant positions.
            Vector3 centroid = Vector3.zero;
            foreach (var p in positions)
                centroid += p;
            centroid /= positions.Count;

            // SW corner = centroid minus half the grid.
            float halfGrid = gridSize * CellSize * 0.5f;
            return new Vector3(
                Mathf.Round(centroid.x - halfGrid),
                centroid.y,   // preserve Y so zone sits correctly on terrain
                Mathf.Round(centroid.z - halfGrid));
        }

        // ── Grid rebuild ──────────────────────────────────────────────────────

        private void RebuildGrid()
        {
            // Seed from origin so the layout is stable across recalculations at the same position.
            int seed = Mathf.RoundToInt(Origin.x * 31 + Origin.z * 17);
            Grid = new BattleGrid(GridSize, GridSize, seed);
        }

        // ── Geometry scan ─────────────────────────────────────────────────────

        /// <summary>
        /// Resets all cells walkable, marks border cells non-walkable, then runs
        /// Physics.OverlapBox on each interior cell. Colliders tagged "Player" or
        /// parented to a DungeonNPC are ignored. Guarantees three centre cells
        /// are always walkable so combat can always begin.
        /// </summary>
        private void ScanGeometry()
        {
            // Step 1: reset — border blocked, interior walkable.
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    bool border = x == 0 || x == GridSize - 1 || y == 0 || y == GridSize - 1;
                    Grid.Walkable[y * GridSize + x] = !border;
                }
            }

            // Step 2: OverlapBox each interior cell.
            var halfExtents = new Vector3(0.45f, 0.5f, 0.45f);

            for (int y = 1; y < GridSize - 1; y++)
            {
                for (int x = 1; x < GridSize - 1; x++)
                {
                    Vector3 cellCenter = Origin + new Vector3(
                        x * CellSize + CellSize * 0.5f,
                        0.5f,
                        y * CellSize + CellSize * 0.5f);

                    Collider[] hits = Physics.OverlapBox(cellCenter, halfExtents);
                    foreach (var col in hits)
                    {
                        if (col.CompareTag("Player")) continue;
                        if (col.GetComponentInParent<DungeonNPC>() != null) continue;

                        Grid.Walkable[y * GridSize + x] = false;
                        break;
                    }
                }
            }

            // Step 3: guarantee centre cells are always walkable.
            int mid = GridSize / 2;
            Grid.Walkable[mid * GridSize + mid]         = true;
            Grid.Walkable[mid * GridSize + (mid - 1)]   = true;
            Grid.Walkable[(mid - 1) * GridSize + mid]   = true;
        }

        // ── Boundary visual ───────────────────────────────────────────────────

        private void BuildBoundary()
        {
            var go = new GameObject("ArenaBoundary");
            go.transform.SetParent(transform, worldPositionStays: false);

            _boundary = go.AddComponent<LineRenderer>();
            _boundary.positionCount = 5;
            _boundary.loop = false;
            _boundary.useWorldSpace = true;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = BoundaryColor;
            _boundary.material = mat;

            _boundary.startWidth = BoundaryWidth;
            _boundary.endWidth   = BoundaryWidth;
            _boundary.startColor = BoundaryColor;
            _boundary.endColor   = BoundaryColor;

            UpdateBoundaryPositions();
        }

        private void UpdateBoundaryPositions()
        {
            if (_boundary == null) return;

            float size = GridSize * CellSize;
            float ox = Origin.x;
            float oz = Origin.z;
            float y  = BoundaryY;

            _boundary.SetPosition(0, new Vector3(ox,        y, oz));
            _boundary.SetPosition(1, new Vector3(ox + size, y, oz));
            _boundary.SetPosition(2, new Vector3(ox + size, y, oz + size));
            _boundary.SetPosition(3, new Vector3(ox,        y, oz + size));
            _boundary.SetPosition(4, new Vector3(ox,        y, oz));  // close the rectangle
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private static List<Vector3> GatherPositions(Vector3 playerPos, List<BattleCombatant> enemies)
        {
            var positions = new List<Vector3>(enemies.Count + 1) { playerPos };
            foreach (var e in enemies)
                positions.Add(e.SpawnWorldPos);
            return positions;
        }
    }
}
