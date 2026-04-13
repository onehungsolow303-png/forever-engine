using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public class BattleRenderer3D : UnityEngine.MonoBehaviour
    {
        private BattleSceneTemplate _template;
        private GameObject _roomInstance;
        private Dictionary<BattleCombatant, GameObject> _models = new();
        private BattleGridOverlay _gridOverlay;
        private ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController _camCtrl;
        private float _cellSize = 1f;
        private Vector3 _gridOffset = Vector3.zero;
        private BattleUI _ui;

        public void Initialize(BattleSceneTemplate template, BattleGrid grid,
            List<BattleCombatant> combatants, Camera cam)
        {
            _template = template;
            _cellSize = 1f;

            // Instantiate room prefab (or empty container if no prefab assigned)
            GameObject roomPrefab = template.RoomPrefab;

            // Note: Lordenfel room prefabs are too heavy for battle scenes (GC crash).
            // Battle rooms use the flat plane fallback. Room prefabs reserved for
            // dungeon exploration via DA Snap builder.

            if (roomPrefab != null)
            {
                _roomInstance = Instantiate(roomPrefab, Vector3.zero, Quaternion.identity);
                _roomInstance.name = "BattleRoom";
            }
            else
            {
                _roomInstance = new GameObject("BattleRoom");
                BuildArena(template.Arena, grid);
            }

            // Setup lighting
            var lightGO = new GameObject("BattleLight");
            lightGO.transform.SetParent(_roomInstance.transform);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = template.LightIntensity;
            light.color = template.AmbientColor;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Setup camera
            _camCtrl = cam.GetComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            if (_camCtrl == null)
                _camCtrl = cam.gameObject.AddComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            var gridCenter = new GameObject("GridCenter");
            gridCenter.transform.position = GridToWorld(grid.Width / 2, grid.Height / 2);
            // Start following grid center; UpdateVisuals switches to active combatant
            _camCtrl.FollowTarget = gridCenter.transform;
            _camCtrl.SetDistance(10f);
            _camCtrl.SnapToTarget();

            // Spawn combatant models
            foreach (var c in combatants)
                SpawnModel(c);

            // Create grid overlay (hidden by default)
            var overlayGO = new GameObject("GridOverlay");
            _gridOverlay = overlayGO.AddComponent<BattleGridOverlay>();
            _gridOverlay.Initialize(grid, _cellSize);

            // Create battle UI
            var uiGO = new GameObject("BattleUI");
            _ui = uiGO.AddComponent<BattleUI>();
            var player = combatants.Find(c => c.IsPlayer);
            if (player != null) _ui.Initialize(player);
        }

        private void SpawnModel(BattleCombatant combatant)
        {
            GameObject model = null;
            if (!string.IsNullOrEmpty(combatant.ModelId))
            {
                var prefab = Resources.Load<GameObject>($"Models/{combatant.ModelId}");
                if (prefab != null)
                {
                    model = Instantiate(prefab);
                    model.transform.localScale *= combatant.ModelScale;
                }
            }

            if (model == null)
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
                var mr = model.GetComponent<Renderer>();
                Color color = combatant.IsPlayer
                    ? new Color(0.2f, 0.6f, 1f)
                    : new Color(0.9f, 0.2f, 0.2f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else
                    mat.color = color;
                mr.material = mat;
                var col = model.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }

            model.name = $"Model_{combatant.Name}";
            model.transform.position = GridToWorld(combatant.X, combatant.Y);
            _models[combatant] = model;
        }

        public void UpdateVisuals(List<BattleCombatant> combatants, BattleCombatant currentTurn)
        {
            // Camera follows current turn combatant
            if (currentTurn != null && _models.TryGetValue(currentTurn, out var turnModel) && turnModel != null)
                _camCtrl.FollowTarget = turnModel.transform;

            foreach (var c in combatants)
            {
                if (!_models.TryGetValue(c, out var model)) continue;
                if (model == null) continue;

                Vector3 targetPos = GridToWorld(c.X, c.Y);
                model.transform.position = Vector3.Lerp(
                    model.transform.position, targetPos, Time.deltaTime * 12f);

                if (!c.IsAlive && model.activeSelf)
                {
                    model.transform.rotation = Quaternion.Lerp(
                        model.transform.rotation,
                        Quaternion.Euler(90f, 0f, 0f),
                        Time.deltaTime * 4f);
                    var mr = model.GetComponentInChildren<Renderer>();
                    if (mr != null)
                    {
                        Color col = mr.material.color;
                        col.a = Mathf.Lerp(col.a, 0f, Time.deltaTime * 2f);
                        mr.material.color = col;
                        if (col.a < 0.05f) model.SetActive(false);
                    }
                }

                if (c.IsAlive)
                {
                    float baseScale = 0.6f * c.ModelScale;
                    if (c == currentTurn)
                    {
                        float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
                        model.transform.localScale = Vector3.one * baseScale * pulse;
                    }
                    else
                    {
                        model.transform.localScale = Vector3.one * baseScale;
                    }
                }

                BattleCombatant faceTarget = null;
                float bestDist = float.MaxValue;
                foreach (var other in combatants)
                {
                    if (other == c || !other.IsAlive || other.IsPlayer == c.IsPlayer) continue;
                    float dist = Mathf.Abs(c.X - other.X) + Mathf.Abs(c.Y - other.Y);
                    if (dist < bestDist) { bestDist = dist; faceTarget = other; }
                }
                if (faceTarget != null)
                {
                    Vector3 dir = GridToWorld(faceTarget.X, faceTarget.Y) - model.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        model.transform.rotation = Quaternion.Lerp(
                            model.transform.rotation,
                            Quaternion.LookRotation(dir),
                            Time.deltaTime * 8f);
                }
            }

            // Update HUD and turn order
            if (_ui != null)
            {
                var player = combatants.Find(c => c.IsPlayer);
                _ui.UpdateHUD(player);
                _ui.UpdateTurnOrder(combatants, currentTurn);
            }
        }

        public void ShowDamage(BattleCombatant target, int amount, bool isCrit)
        {
            if (!_models.TryGetValue(target, out var model) || model == null) return;
            var go = new GameObject("DmgNum");
            go.transform.position = model.transform.position + Vector3.up * 1.5f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = amount.ToString();
            tm.characterSize = 0.15f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            if (isCrit) tm.color = Color.yellow;
            else if (amount >= 15) tm.color = Color.red;
            else if (amount >= 8) tm.color = new Color(1f, 0.6f, 0f);
            else tm.color = Color.white;

            go.AddComponent<DamagePopup>();
        }

        public void ShowPathPreview(BattleGrid grid, BattleCombatant mover,
            int targetX, int targetY, List<BattleCombatant> combatants)
        {
            if (_gridOverlay != null)
                _gridOverlay.ShowPath(grid, mover, targetX, targetY, combatants, _cellSize);
        }

        public void ClearPathPreview()
        {
            if (_gridOverlay != null)
                _gridOverlay.ClearPath();
        }

        /// <summary>Flash a combatant's model white briefly to indicate a hit.</summary>
        public void ShowHitFlash(BattleCombatant target)
        {
            if (!_models.TryGetValue(target, out var model) || model == null) return;
            var flash = model.GetComponent<HitFlash>();
            if (flash == null) flash = model.AddComponent<HitFlash>();
            flash.Trigger();
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return _gridOffset + new Vector3(x * _cellSize + _cellSize * 0.5f, 0.1f, y * _cellSize + _cellSize * 0.5f);
        }

        public (int x, int y) WorldToGrid(Vector3 pos)
        {
            int x = Mathf.FloorToInt(pos.x / _cellSize);
            int y = Mathf.FloorToInt(pos.z / _cellSize);
            return (x, y);
        }

        public void Cleanup()
        {
            if (_roomInstance != null) Destroy(_roomInstance);
            foreach (var kv in _models)
                if (kv.Value != null) Destroy(kv.Value);
            _models.Clear();
            if (_gridOverlay != null) Destroy(_gridOverlay.gameObject);
            if (_ui != null) Destroy(_ui.gameObject);
        }

        private void BuildArena(ArenaType arenaType, BattleGrid grid)
        {
            float gridW = grid.Width * _cellSize;
            float gridH = grid.Height * _cellSize;
            float centerX = gridW / 2f;
            float centerZ = gridH / 2f;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            switch (arenaType)
            {
                case ArenaType.Dungeon:
                    BuildFloor(gridW, gridH, centerX, centerZ, new Color(0.3f, 0.3f, 0.35f), shader);
                    BuildDungeonWalls(gridW, gridH, centerX, centerZ, 3f, 0.3f, new Color(0.3f, 0.3f, 0.35f), shader);
                    break;

                case ArenaType.Boss:
                    BuildFloor(gridW, gridH, centerX, centerZ, new Color(0.25f, 0.2f, 0.22f), shader);
                    BuildBossWalls(gridW, gridH, centerX, centerZ, shader);
                    break;

                case ArenaType.Overworld:
                    BuildFloor(gridW * 1.5f, gridH * 1.5f, centerX, centerZ, new Color(0.4f, 0.5f, 0.3f), shader);
                    BuildOverworldRocks(grid, centerX, centerZ, shader);
                    break;
            }
        }

        private void BuildFloor(float w, float h, float cx, float cz, Color color, Shader shader)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(_roomInstance.transform);
            floor.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
            floor.transform.position = new Vector3(cx, 0f, cz);
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
            floor.GetComponent<Renderer>().material = mat;
        }

        private void BuildDungeonWalls(float gridW, float gridH, float cx, float cz,
            float wallHeight, float wallThickness, Color color, Shader shader)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;

            // North wall
            CreateWall("WallNorth", new Vector3(cx, wallHeight / 2f, gridH),
                new Vector3(gridW, wallHeight, wallThickness), mat);

            // East wall
            CreateWall("WallEast", new Vector3(gridW, wallHeight / 2f, cz),
                new Vector3(wallThickness, wallHeight, gridH), mat);

            // West wall
            CreateWall("WallWest", new Vector3(0f, wallHeight / 2f, cz),
                new Vector3(wallThickness, wallHeight, gridH), mat);

            // South wall — split with 2-unit door gap
            float doorGap = 2f;
            float segmentLen = (gridW - doorGap) / 2f;
            CreateWall("WallSouthL", new Vector3(segmentLen / 2f, wallHeight / 2f, 0f),
                new Vector3(segmentLen, wallHeight, wallThickness), mat);
            CreateWall("WallSouthR", new Vector3(gridW - segmentLen / 2f, wallHeight / 2f, 0f),
                new Vector3(segmentLen, wallHeight, wallThickness), mat);
        }

        private void BuildBossWalls(float gridW, float gridH, float cx, float cz, Shader shader)
        {
            float wallHeight = 5f;
            float wallThickness = 0.5f;
            var wallColor = new Color(0.25f, 0.2f, 0.22f);

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", wallColor);
            else
                mat.color = wallColor;

            // North wall
            CreateWall("WallNorth", new Vector3(cx, wallHeight / 2f, gridH),
                new Vector3(gridW, wallHeight, wallThickness), mat);

            // East wall
            CreateWall("WallEast", new Vector3(gridW, wallHeight / 2f, cz),
                new Vector3(wallThickness, wallHeight, gridH), mat);

            // West wall
            CreateWall("WallWest", new Vector3(0f, wallHeight / 2f, cz),
                new Vector3(wallThickness, wallHeight, gridH), mat);

            // South wall — 2.5-unit door gap
            float doorGap = 2.5f;
            float segmentLen = (gridW - doorGap) / 2f;
            CreateWall("WallSouthL", new Vector3(segmentLen / 2f, wallHeight / 2f, 0f),
                new Vector3(segmentLen, wallHeight, wallThickness), mat);
            CreateWall("WallSouthR", new Vector3(gridW - segmentLen / 2f, wallHeight / 2f, 0f),
                new Vector3(segmentLen, wallHeight, wallThickness), mat);

            // 4 corner pillars inside the arena
            float pillarSize = 0.8f;
            float inset = 2f;
            var pillarPositions = new Vector3[]
            {
                new(inset, wallHeight / 2f, inset),
                new(gridW - inset, wallHeight / 2f, inset),
                new(inset, wallHeight / 2f, gridH - inset),
                new(gridW - inset, wallHeight / 2f, gridH - inset),
            };
            for (int i = 0; i < pillarPositions.Length; i++)
                CreateWall($"Pillar_{i}", pillarPositions[i],
                    new Vector3(pillarSize, wallHeight, pillarSize), mat);
        }

        private void BuildOverworldRocks(BattleGrid grid, float cx, float cz, Shader shader)
        {
            var mat = new Material(shader);
            var rockColor = new Color(0.45f, 0.42f, 0.38f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", rockColor);
            else
                mat.color = rockColor;

            var rng = new System.Random(grid.Width * 1000 + grid.Height);
            int rockCount = 3 + rng.Next(3);

            for (int i = 0; i < rockCount; i++)
            {
                int rx, ry;
                int attempts = 0;
                do
                {
                    rx = rng.Next(grid.Width / 4, grid.Width * 3 / 4);
                    ry = rng.Next(grid.Height / 4, grid.Height * 3 / 4);
                    attempts++;
                } while (attempts < 20 && !grid.IsWalkable(rx, ry));

                if (attempts >= 20) continue;

                grid.Walkable[ry * grid.Width + rx] = false;

                float scaleX = 0.6f + (float)rng.NextDouble() * 0.6f;
                float scaleY = 0.4f + (float)rng.NextDouble() * 0.4f;
                float scaleZ = 0.6f + (float)rng.NextDouble() * 0.6f;
                float rotY = (float)rng.NextDouble() * 45f;

                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"Rock_{i}";
                rock.transform.SetParent(_roomInstance.transform);
                rock.transform.position = new Vector3(
                    rx * _cellSize + _cellSize * 0.5f, scaleY / 2f,
                    ry * _cellSize + _cellSize * 0.5f);
                rock.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
                rock.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
                rock.GetComponent<Renderer>().material = mat;
                var col = rock.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(_roomInstance.transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = mat;
            var col = wall.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }

    public class DamagePopup : UnityEngine.MonoBehaviour
    {
        private float _timer;
        private TextMesh _tm;

        private void Start() => _tm = GetComponent<TextMesh>();

        private void Update()
        {
            _timer += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 1.5f;
            if (_tm != null)
            {
                Color c = _tm.color;
                c.a = Mathf.Lerp(1f, 0f, _timer);
                _tm.color = c;
            }
            var cam = Camera.main;
            if (cam != null) transform.forward = cam.transform.forward;
            if (_timer >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Brief white flash on a model to indicate a successful hit.</summary>
    public class HitFlash : UnityEngine.MonoBehaviour
    {
        private float _timer = -1f;
        private Renderer _mr;
        private Color _originalColor;
        private static readonly Color FLASH_COLOR = Color.white;
        private const float FLASH_DURATION = 0.15f;

        public void Trigger()
        {
            if (_mr == null) _mr = GetComponentInChildren<Renderer>();
            if (_mr == null) return;
            _originalColor = _mr.material.color;
            _mr.material.color = FLASH_COLOR;
            _timer = FLASH_DURATION;
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                if (_mr != null) _mr.material.color = _originalColor;
                _timer = -1f;
            }
        }
    }
}
