using UnityEngine;
using ForeverEngine.Demo.Dungeon;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Per-enemy 8x8 grid zone. Snaps to integer world coordinates, scans scene
    /// geometry to determine walkability, and draws a LineRenderer boundary box.
    ///
    /// Lifecycle: Activate → (optional ReCenter) → Deactivate.
    /// Deactivate destroys the GameObject, so callers must null their reference.
    /// </summary>
    public class BattleZone : UnityEngine.MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────
        public const int GridSize = 8;
        public const float CellSize = 1f;

        // ── Public state ──────────────────────────────────────────────────────
        /// <summary>The enemy combatant that owns this zone.</summary>
        public BattleCombatant OwnerEnemy { get; private set; }

        /// <summary>World-space origin of the grid (integer-snapped, SW corner).</summary>
        public Vector3 Origin { get; private set; }

        /// <summary>8×8 walkability grid (constructed fresh on Activate / re-scanned on ReCenter).</summary>
        public BattleGrid Grid { get; private set; }

        // ── Private visual state ──────────────────────────────────────────────
        private LineRenderer _boundary;

        // ── Boundary visual constants ─────────────────────────────────────────
        private static readonly Color BoundaryColor = new Color(0.7f, 0.85f, 1.0f, 0.8f);
        private const float BoundaryWidth = 0.06f;
        private const float BoundaryY = 0.05f;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Initialise the zone: snap position, build grid, scan geometry, show boundary.
        /// </summary>
        public void Activate(BattleCombatant owner, Vector3 position)
        {
            OwnerEnemy = owner;
            // Center the zone so GridToWorld(GridSize/2, GridSize/2) ≈ position.
            // GridToWorld adds +0.5*CellSize for cell center, so offset = GridSize/2 * CellSize + 0.5*CellSize
            float centerOffset = (GridSize / 2) * CellSize + CellSize * 0.5f; // 4.5
            Origin = new Vector3(
                Mathf.Round(position.x - centerOffset),
                position.y,
                Mathf.Round(position.z - centerOffset));

            // Use enemy's grid position as the seed so each enemy gets consistent
            // random layout before the geometry scan overwrites it.
            int seed = owner != null ? (owner.X * 31 + owner.Y * 17) : 0;
            Grid = new BattleGrid(GridSize, GridSize, seed);

            ScanGeometry();
            BuildBoundary();
        }

        /// <summary>
        /// Shift the zone to a new world position, re-scan, and refresh the boundary.
        /// </summary>
        public void ReCenter(Vector3 newPosition)
        {
            Origin = SnapToGrid(newPosition);

            int seed = OwnerEnemy != null ? (OwnerEnemy.X * 31 + OwnerEnemy.Y * 17) : 0;
            Grid = new BattleGrid(GridSize, GridSize, seed);

            ScanGeometry();
            UpdateBoundaryPositions();
        }

        /// <summary>
        /// Tear down the boundary and destroy this zone's GameObject.
        /// Caller must null their reference after this call.
        /// </summary>
        public void Deactivate()
        {
            Destroy(gameObject);
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        /// <summary>
        /// Returns the world-space center of grid cell (x, y).
        /// </summary>
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
        /// Returns true if <paramref name="pos"/> falls within the 8×8 world footprint.
        /// </summary>
        public bool ContainsWorldPos(Vector3 pos)
        {
            float totalSize = GridSize * CellSize;
            float dx = pos.x - Origin.x;
            float dz = pos.z - Origin.z;
            return dx >= 0f && dx < totalSize && dz >= 0f && dz < totalSize;
        }

        // ── Geometry scan ─────────────────────────────────────────────────────

        /// <summary>
        /// Resets all cells walkable, marks border cells non-walkable, then runs
        /// Physics.OverlapBox on each interior cell. Colliders tagged "Player" or
        /// parented to a DungeonNPC are ignored. Guarantees the three centre cells
        /// are always walkable so combat can always begin.
        /// </summary>
        private void ScanGeometry()
        {
            // Step 1: reset everything to walkable, then block border.
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
                        0.5f,  // vertical mid of the overlap box
                        y * CellSize + CellSize * 0.5f);

                    Collider[] hits = Physics.OverlapBox(cellCenter, halfExtents);
                    foreach (var col in hits)
                    {
                        // Skip player-tagged objects.
                        if (col.CompareTag("Player")) continue;

                        // Skip dungeon NPCs (friendly entities that happen to be in the scene).
                        if (col.GetComponentInParent<DungeonNPC>() != null) continue;

                        // Any remaining collider blocks the cell.
                        Grid.Walkable[y * GridSize + x] = false;
                        break;
                    }
                }
            }

            // Step 3: guarantee centre cells are always walkable so combat can start.
            int mid = GridSize / 2; // = 4
            Grid.Walkable[mid * GridSize + mid]           = true; // (4,4)
            Grid.Walkable[mid * GridSize + (mid - 1)]     = true; // (3,4)
            Grid.Walkable[(mid - 1) * GridSize + mid]     = true; // (4,3)
        }

        // ── Boundary visual ───────────────────────────────────────────────────

        private void BuildBoundary()
        {
            var go = new GameObject("ZoneBoundary");
            go.transform.SetParent(transform, worldPositionStays: false);

            _boundary = go.AddComponent<LineRenderer>();
            _boundary.positionCount = 5;
            _boundary.loop = false; // We close it manually with point[4] == point[0]
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

        /// <summary>
        /// Rounds x and z to the nearest integer so cell boundaries align to world units.
        /// y is preserved so the zone sits correctly on sloped or elevated terrain.
        /// </summary>
        private static Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(Mathf.Round(pos.x), pos.y, Mathf.Round(pos.z));
        }
    }
}
