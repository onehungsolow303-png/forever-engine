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
        private int _gridWidth, _gridHeight;
        private float _cellSize;

        private static readonly Color COLOR_REACHABLE = new Color(0.2f, 0.5f, 1f, 0.25f);
        private static readonly Color COLOR_THREATENED = new Color(1f, 0.2f, 0.2f, 0.25f);
        private static readonly Color COLOR_CURRENT = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color COLOR_PATH = new Color(0.3f, 0.7f, 1f, 0.4f);

        public void Initialize(BattleGrid grid, float cellSize)
        {
            _gridWidth = grid.Width;
            _gridHeight = grid.Height;
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

            gameObject.SetActive(false);
        }

        public void Show(BattleGrid grid, BattleCombatant mover, List<BattleCombatant> combatants, float cellSize)
        {
            _cellSize = cellSize;
            gameObject.SetActive(true);
            RebuildMesh(grid, mover, combatants);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RebuildMesh(BattleGrid grid, BattleCombatant mover, List<BattleCombatant> combatants)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var colors = new List<Color>();

            var occupied = new HashSet<(int, int)>();
            var threatened = new HashSet<(int, int)>();
            foreach (var c in combatants)
            {
                if (!c.IsAlive) continue;
                occupied.Add((c.X, c.Y));
                if (!c.IsPlayer)
                {
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (dx != 0 || dy != 0)
                                threatened.Add((c.X + dx, c.Y + dy));
                }
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!grid.IsWalkable(x, y)) continue;

                    Color cellColor;
                    if (x == mover.X && y == mover.Y)
                        cellColor = COLOR_CURRENT;
                    else if (threatened.Contains((x, y)))
                        cellColor = COLOR_THREATENED;
                    else
                    {
                        int dist = Mathf.Abs(x - mover.X) + Mathf.Abs(y - mover.Y);
                        if (dist <= mover.MovementRemaining && !occupied.Contains((x, y)))
                            cellColor = COLOR_REACHABLE;
                        else
                            continue;
                    }

                    AddQuad(verts, tris, colors, x, y, cellColor);
                }
            }

            if (_mesh == null) _mesh = new Mesh();
            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.SetColors(colors);
            _mesh.RecalculateNormals();
            _mf.mesh = _mesh;
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
