using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleRenderer : UnityEngine.MonoBehaviour
    {
        private List<GameObject> _tileObjects = new();
        private Dictionary<BattleCombatant, GameObject> _tokenObjects = new();
        private Dictionary<BattleCombatant, float> _targetHpPercent = new();
        private Dictionary<BattleCombatant, float> _displayHpPercent = new();
        private List<DamageNumber> _damageNumbers = new();
        private Mesh _squareMesh;
        private Material _baseMaterial;
        private Camera _cam;

        private static readonly Color COLOR_FLOOR = new Color(0.25f, 0.22f, 0.2f);
        private static readonly Color COLOR_WALL = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color COLOR_OBSTACLE = new Color(0.18f, 0.16f, 0.15f);
        private static readonly Color COLOR_PLAYER = new Color(0.2f, 0.6f, 1f);
        private static readonly Color COLOR_ENEMY = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color COLOR_HIGHLIGHT = new Color(0.4f, 0.5f, 0.3f);

        // Condition tint colors
        private static readonly Color TINT_POISON = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        private static readonly Color TINT_STUN = new Color(1f, 1f, 0.2f, 0.3f);
        private static readonly Color TINT_FROZEN = new Color(0.4f, 0.7f, 1f, 0.3f);
        private static readonly Color TINT_BURNING = new Color(1f, 0.4f, 0.1f, 0.3f);

        private const float TOKEN_RADIUS = 0.45f;
        private const float HP_BAR_LERP_SPEED = 4f;
        private const float DEATH_FADE_SPEED = 2f;

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
                var token = CreateToken(c.Name, c.IsPlayer ? COLOR_PLAYER : COLOR_ENEMY, TOKEN_RADIUS);
                token.transform.position = new Vector3(c.X, c.Y, -1f);

                // Name label
                var labelGO = new GameObject($"Label_{c.Name}");
                labelGO.transform.SetParent(token.transform);
                labelGO.transform.localPosition = new Vector3(0, 0.65f, 0);
                var tm = labelGO.AddComponent<TextMesh>();
                tm.text = c.Name;
                tm.characterSize = 0.14f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = c.IsPlayer ? COLOR_PLAYER : COLOR_ENEMY;

                // HP bar background
                var hpBgGO = CreateTile($"HPBg_{c.Name}", Vector3.zero, Color.black);
                hpBgGO.transform.SetParent(token.transform);
                hpBgGO.transform.localPosition = new Vector3(0, -0.55f, -0.1f);
                hpBgGO.transform.localScale = new Vector3(0.8f, 0.1f, 1f);

                // HP bar fill
                var hpGO = CreateTile($"HP_{c.Name}", Vector3.zero, Color.green);
                hpGO.transform.SetParent(token.transform);
                hpGO.transform.localPosition = new Vector3(0, -0.55f, -0.2f);
                hpGO.transform.localScale = new Vector3(0.8f, 0.1f, 1f);
                hpGO.name = "HPFill";

                // Condition tint overlay (hidden by default)
                var tintGO = CreateToken($"Tint_{c.Name}", Color.clear, TOKEN_RADIUS + 0.05f);
                tintGO.transform.SetParent(token.transform);
                tintGO.transform.localPosition = new Vector3(0, 0, -0.05f);
                tintGO.name = "ConditionTint";

                _tokenObjects[c] = token;
                _targetHpPercent[c] = 1f;
                _displayHpPercent[c] = 1f;
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
            float dt = Time.deltaTime;

            foreach (var c in combatants)
            {
                if (!_tokenObjects.TryGetValue(c, out var token)) continue;

                // Death fade
                if (!c.IsAlive)
                {
                    var tokenMR = token.GetComponent<MeshRenderer>();
                    if (tokenMR != null)
                    {
                        Color col = tokenMR.material.color;
                        col.a = Mathf.MoveTowards(col.a, 0f, dt * DEATH_FADE_SPEED);
                        tokenMR.material.color = col;
                        if (col.a <= 0.01f)
                            token.SetActive(false);
                    }
                    continue;
                }

                // Smooth move to position
                Vector3 targetPos = new Vector3(c.X, c.Y, -1f);
                token.transform.position = Vector3.Lerp(token.transform.position, targetPos, dt * 15f);

                // Animated HP bar
                float actualHp = c.MaxHP > 0 ? (float)c.HP / c.MaxHP : 0;
                _targetHpPercent[c] = actualHp;
                _displayHpPercent[c] = Mathf.MoveTowards(_displayHpPercent[c], actualHp, dt * HP_BAR_LERP_SPEED);
                float hpPercent = _displayHpPercent[c];

                var hpFill = token.transform.Find("HPFill");
                if (hpFill != null)
                {
                    hpFill.localScale = new Vector3(0.8f * hpPercent, 0.1f, 1f);
                    hpFill.localPosition = new Vector3(-0.4f * (1f - hpPercent), -0.55f, -0.2f);
                    var mr = hpFill.GetComponent<MeshRenderer>();
                    if (mr != null)
                        mr.material.color = hpPercent > 0.5f ? Color.green : hpPercent > 0.25f ? Color.yellow : Color.red;
                }

                // Active turn indicator — scale pulse
                var tokenMesh = token.GetComponent<MeshRenderer>();
                if (tokenMesh != null)
                {
                    Color baseColor = c.IsPlayer ? COLOR_PLAYER : COLOR_ENEMY;
                    if (c == currentTurn)
                    {
                        float pulse = Mathf.PingPong(Time.time * 2f, 0.3f);
                        tokenMesh.material.color = Color.Lerp(baseColor, Color.white, pulse);
                        token.transform.localScale = Vector3.one * (1f + pulse * 0.15f);
                    }
                    else
                    {
                        tokenMesh.material.color = baseColor;
                        token.transform.localScale = Vector3.one;
                    }
                }

                // Condition tint overlay
                var tint = token.transform.Find("ConditionTint");
                if (tint != null && c.Conditions != null)
                {
                    var tintMR = tint.GetComponent<MeshRenderer>();
                    if (tintMR != null)
                    {
                        Color tintColor = Color.clear;
                        var active = c.Conditions.ActiveFlags;
                        if ((active & RPG.Enums.Condition.Poisoned) != 0)
                            tintColor = TINT_POISON;
                        else if ((active & RPG.Enums.Condition.Stunned) != 0)
                            tintColor = TINT_STUN;
                        else if ((active & RPG.Enums.Condition.Paralyzed) != 0)
                            tintColor = TINT_FROZEN;
                        else if ((active & RPG.Enums.Condition.Frightened) != 0)
                            tintColor = TINT_BURNING;
                        tintMR.material.color = tintColor;
                    }
                }
            }

            // Update damage numbers
            UpdateDamageNumbers(dt);
        }

        // ── Damage Numbers ────────────────────────────────────────────────

        public void ShowDamageNumber(Vector3 worldPos, int damage, bool isCrit)
        {
            var go = new GameObject("DmgNum");
            go.transform.SetParent(transform);
            go.transform.position = worldPos + new Vector3(0, 0.3f, -2f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = isCrit ? $"CRIT {damage}!" : damage.ToString();
            tm.characterSize = isCrit ? 0.18f : 0.15f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = isCrit ? Color.yellow : Color.white;
            tm.fontStyle = isCrit ? FontStyle.Bold : FontStyle.Normal;

            _damageNumbers.Add(new DamageNumber { GO = go, Timer = 1.2f, StartY = worldPos.y + 0.3f });
        }

        public void ShowMiss(Vector3 worldPos)
        {
            var go = new GameObject("MissText");
            go.transform.SetParent(transform);
            go.transform.position = worldPos + new Vector3(0, 0.3f, -2f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = "MISS";
            tm.characterSize = 0.12f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.7f, 0.7f, 0.7f);

            _damageNumbers.Add(new DamageNumber { GO = go, Timer = 0.8f, StartY = worldPos.y + 0.3f });
        }

        private void UpdateDamageNumbers(float dt)
        {
            for (int i = _damageNumbers.Count - 1; i >= 0; i--)
            {
                var dn = _damageNumbers[i];
                dn.Timer -= dt;
                if (dn.Timer <= 0)
                {
                    Destroy(dn.GO);
                    _damageNumbers.RemoveAt(i);
                    continue;
                }

                // Float upward and fade
                float progress = 1f - dn.Timer / 1.2f;
                var pos = dn.GO.transform.position;
                pos.y = dn.StartY + progress * 0.8f;
                dn.GO.transform.position = pos;

                var tm = dn.GO.GetComponent<TextMesh>();
                if (tm != null)
                {
                    Color col = tm.color;
                    col.a = Mathf.Clamp01(dn.Timer / 0.3f); // Fade in last 0.3s
                    tm.color = col;
                }

                _damageNumbers[i] = dn;
            }
        }

        private struct DamageNumber
        {
            public GameObject GO;
            public float Timer;
            public float StartY;
        }

        // ── Primitives ────────────────────────────────────────────────────

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

            int segments = 24; // Smoother circles
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

            // Create white pixel texture — Sprites/Default needs a texture to render in URP
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _baseMaterial = new Material(Shader.Find("Sprites/Default"));
            _baseMaterial.mainTexture = tex;
        }
    }
}
