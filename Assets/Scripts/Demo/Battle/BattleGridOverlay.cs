using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleGridOverlay : UnityEngine.MonoBehaviour
    {
        private Mesh _mesh;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Material _material;
        private float _cellSize;

        private static readonly Color COLOR_REACHABLE = new Color(0.1f, 0.8f, 0.2f, 0.35f);
        private static readonly Color COLOR_OUT_OF_RANGE = new Color(0.9f, 0.15f, 0.1f, 0.35f);
        private static readonly Color COLOR_CURRENT = new Color(1f, 1f, 1f, 0.25f);

        public void Initialize(BattleGrid grid, float cellSize)
        {
            _cellSize = cellSize;

            _mf = gameObject.AddComponent<MeshFilter>();
            _mr = gameObject.AddComponent<MeshRenderer>();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _material = new Material(shader);
            _material.SetFloat("_Surface", 1f);
            _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_ZWrite", 0);
            _material.renderQueue = 3000;
            _mr.material = _material;

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Show path from mover to target cell. Green if reachable, red if out of range.
        /// Only highlights the path cells, not the entire grid.
        /// </summary>
        public void ShowPath(BattleGrid grid, BattleCombatant mover, int targetX, int targetY,
            List<BattleCombatant> combatants, float cellSize)
        {
            _cellSize = cellSize;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var colors = new List<Color>();

            // Build occupied set
            var occupied = new HashSet<(int, int)>();
            foreach (var c in combatants)
                if (c.IsAlive && c != mover)
                    occupied.Add((c.X, c.Y));

            // Compute Manhattan path (step through cardinal directions toward target)
            var path = ComputePath(mover.X, mover.Y, targetX, targetY, grid, occupied);

            bool reachable = path.Count <= mover.MovementRemaining
                && grid.IsWalkable(targetX, targetY)
                && !occupied.Contains((targetX, targetY));

            // Show current tile
            AddQuad(verts, tris, colors, mover.X, mover.Y, COLOR_CURRENT);

            // Show path cells — green if reachable, red if not
            for (int i = 0; i < path.Count; i++)
            {
                var (px, py) = path[i];
                float t = (float)(i + 1) / Mathf.Max(1, path.Count);
                Color cellColor = reachable
                    ? Color.Lerp(COLOR_REACHABLE, COLOR_REACHABLE, t)
                    : Color.Lerp(COLOR_REACHABLE, COLOR_OUT_OF_RANGE,
                        Mathf.Clamp01((float)(i + 1 - mover.MovementRemaining) / Mathf.Max(1, path.Count - mover.MovementRemaining)));

                // Cells beyond movement range are always red
                if (i >= mover.MovementRemaining)
                    cellColor = COLOR_OUT_OF_RANGE;

                AddQuad(verts, tris, colors, px, py, cellColor);
            }

            if (_mesh == null) _mesh = new Mesh();
            _mesh.Clear();
            if (verts.Count > 0)
            {
                _mesh.SetVertices(verts);
                _mesh.SetTriangles(tris, 0);
                _mesh.SetColors(colors);
                _mesh.RecalculateNormals();
            }
            _mf.mesh = _mesh;
        }

        public void ClearPath()
        {
            if (_mesh != null) _mesh.Clear();
            if (_mf != null) _mf.mesh = _mesh;
        }

        /// <summary>
        /// Simple Manhattan walk toward target, avoiding obstacles and occupied cells.
        /// Returns list of cells from mover toward target (not including mover's cell).
        /// </summary>
        private List<(int x, int y)> ComputePath(int fromX, int fromY, int toX, int toY,
            BattleGrid grid, HashSet<(int, int)> occupied)
        {
            var path = new List<(int, int)>();
            int cx = fromX, cy = fromY;
            int maxSteps = Mathf.Abs(toX - fromX) + Mathf.Abs(toY - fromY);
            maxSteps = Mathf.Min(maxSteps, 20); // Safety cap

            for (int i = 0; i < maxSteps; i++)
            {
                if (cx == toX && cy == toY) break;

                // Prefer the axis with greater distance
                int dx = toX - cx, dy = toY - cy;
                int nx, ny;

                if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                {
                    nx = cx + (dx > 0 ? 1 : -1); ny = cy;
                    if (!grid.IsWalkable(nx, ny) || occupied.Contains((nx, ny)))
                    {
                        // Try other axis
                        nx = cx; ny = cy + (dy > 0 ? 1 : (dy < 0 ? -1 : 0));
                        if (dy == 0 || !grid.IsWalkable(nx, ny) || occupied.Contains((nx, ny)))
                            break; // Stuck
                    }
                }
                else
                {
                    nx = cx; ny = cy + (dy > 0 ? 1 : -1);
                    if (!grid.IsWalkable(nx, ny) || occupied.Contains((nx, ny)))
                    {
                        nx = cx + (dx > 0 ? 1 : (dx < 0 ? -1 : 0)); ny = cy;
                        if (dx == 0 || !grid.IsWalkable(nx, ny) || occupied.Contains((nx, ny)))
                            break; // Stuck
                    }
                }

                cx = nx; cy = ny;
                path.Add((cx, cy));
            }

            return path;
        }

        private void AddQuad(List<Vector3> verts, List<int> tris, List<Color> colors,
            int x, int y, Color color)
        {
            float padding = 0.05f;
            float y3d = 0.01f;
            int idx = verts.Count;

            float x0 = x * _cellSize + padding;
            float x1 = (x + 1) * _cellSize - padding;
            float z0 = y * _cellSize + padding;
            float z1 = (y + 1) * _cellSize - padding;

            verts.Add(new Vector3(x0, y3d, z0));
            verts.Add(new Vector3(x0, y3d, z1));
            verts.Add(new Vector3(x1, y3d, z1));
            verts.Add(new Vector3(x1, y3d, z0));

            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);

            colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
        }
    }
}
