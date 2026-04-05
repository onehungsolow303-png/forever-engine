using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleRenderer : UnityEngine.MonoBehaviour
    {
        private List<GameObject> _tileObjects = new();
        private Dictionary<BattleCombatant, GameObject> _tokenObjects = new();
        private Mesh _squareMesh;
        private Material _baseMaterial;
        private Camera _cam;

        private static readonly Color COLOR_FLOOR = new Color(0.25f, 0.22f, 0.2f);
        private static readonly Color COLOR_WALL = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color COLOR_OBSTACLE = new Color(0.18f, 0.16f, 0.15f);
        private static readonly Color COLOR_PLAYER = new Color(0.2f, 0.6f, 1f);
        private static readonly Color COLOR_ENEMY = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color COLOR_DEAD = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        private static readonly Color COLOR_HIGHLIGHT = new Color(0.4f, 0.5f, 0.3f);

        public void Initialize(BattleGrid grid, List<BattleCombatant> combatants, Camera cam)
        {
            _cam = cam;
            CreateMeshAndMaterial();

            // Render grid tiles
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var go = CreateTile($"Tile_{x}_{y}", new Vector3(x, y, 0),
                        grid.IsWalkable(x, y) ? COLOR_FLOOR : (x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1 ? COLOR_WALL : COLOR_OBSTACLE));
                    _tileObjects.Add(go);
                }
            }

            // Render combatant tokens
            foreach (var c in combatants)
            {
                var token = CreateToken(c.Name, c.IsPlayer ? COLOR_PLAYER : COLOR_ENEMY, 0.35f);
                token.transform.position = new Vector3(c.X, c.Y, -1f);

                // Name label
                var labelGO = new GameObject($"Label_{c.Name}");
                labelGO.transform.SetParent(token.transform);
                labelGO.transform.localPosition = new Vector3(0, 0.55f, 0);
                var tm = labelGO.AddComponent<TextMesh>();
                tm.text = c.Name;
                tm.characterSize = 0.12f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = c.IsPlayer ? COLOR_PLAYER : COLOR_ENEMY;

                // HP bar background
                var hpBgGO = CreateTile($"HPBg_{c.Name}", Vector3.zero, Color.black);
                hpBgGO.transform.SetParent(token.transform);
                hpBgGO.transform.localPosition = new Vector3(0, -0.45f, -0.1f);
                hpBgGO.transform.localScale = new Vector3(0.7f, 0.08f, 1f);

                // HP bar fill
                var hpGO = CreateTile($"HP_{c.Name}", Vector3.zero, Color.red);
                hpGO.transform.SetParent(token.transform);
                hpGO.transform.localPosition = new Vector3(0, -0.45f, -0.2f);
                hpGO.transform.localScale = new Vector3(0.7f, 0.08f, 1f);
                hpGO.name = "HPFill";

                _tokenObjects[c] = token;
            }

            // Center camera on grid
            if (_cam != null)
            {
                _cam.transform.position = new Vector3(grid.Width / 2f, grid.Height / 2f, -10f);
                _cam.orthographicSize = Mathf.Max(grid.Width, grid.Height) / 2f + 1f;
            }
        }

        public void UpdateVisuals(List<BattleCombatant> combatants, BattleCombatant currentTurn)
        {
            foreach (var c in combatants)
            {
                if (!_tokenObjects.TryGetValue(c, out var token)) continue;

                if (!c.IsAlive)
                {
                    token.SetActive(false);
                    continue;
                }

                // Smooth move to position
                Vector3 targetPos = new Vector3(c.X, c.Y, -1f);
                token.transform.position = Vector3.Lerp(token.transform.position, targetPos, Time.deltaTime * 15f);

                // Update HP bar
                var hpFill = token.transform.Find("HPFill");
                if (hpFill != null)
                {
                    float hpPercent = c.MaxHP > 0 ? (float)c.HP / c.MaxHP : 0;
                    hpFill.localScale = new Vector3(0.7f * hpPercent, 0.08f, 1f);
                    hpFill.localPosition = new Vector3(-0.35f * (1f - hpPercent), -0.45f, -0.2f);
                    var mr = hpFill.GetComponent<MeshRenderer>();
                    if (mr != null)
                        mr.material.color = hpPercent > 0.5f ? Color.green : hpPercent > 0.25f ? Color.yellow : Color.red;
                }

                // Highlight current turn
                var tokenMR = token.GetComponent<MeshRenderer>();
                if (tokenMR != null)
                {
                    Color baseColor = c.IsPlayer ? COLOR_PLAYER : COLOR_ENEMY;
                    if (c == currentTurn)
                        tokenMR.material.color = Color.Lerp(baseColor, Color.white, Mathf.PingPong(Time.time * 2f, 0.3f));
                    else
                        tokenMR.material.color = baseColor;
                }
            }
        }

        private GameObject CreateTile(string name, Vector3 position, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = _squareMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(_baseMaterial);
            mr.sharedMaterial.color = color;
            return go;
        }

        private GameObject CreateToken(string name, Color color, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            int segments = 16;
            var mesh = new Mesh();
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(_baseMaterial);
            mr.sharedMaterial.color = color;

            return go;
        }

        private void CreateMeshAndMaterial()
        {
            // Unit square (0.9 size for gap between tiles)
            _squareMesh = new Mesh();
            float s = 0.45f;
            _squareMesh.vertices = new[] { new Vector3(-s,-s,0), new Vector3(s,-s,0), new Vector3(s,s,0), new Vector3(-s,s,0) };
            _squareMesh.triangles = new[] { 0,2,1, 0,3,2 };
            _squareMesh.RecalculateNormals();

            _baseMaterial = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
