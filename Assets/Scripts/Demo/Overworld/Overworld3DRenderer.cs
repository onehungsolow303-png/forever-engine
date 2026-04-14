using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    /// <summary>
    /// 3D overworld renderer. Uses <see cref="OverworldPrefabMapper"/> to instantiate
    /// real asset pack prefabs (trees, buildings, gates) instead of primitives.
    /// Falls back to colored primitives when prefabs aren't assigned.
    /// </summary>
    public class Overworld3DRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private OverworldPrefabMapper _prefabMap;

        private Dictionary<(int, int), GameObject> _tileInstances = new();
        private GameObject _playerModel;
        private Dictionary<string, GameObject> _locationMarkers = new();
        private Transform _tileParent;
        private Light _directionalLight;

        // Fallback tile colors when no prefab/material is assigned
        private static readonly Color COLOR_PLAINS   = new Color(0.5f, 0.7f, 0.35f);
        private static readonly Color COLOR_FOREST   = new Color(0.25f, 0.5f, 0.2f);
        private static readonly Color COLOR_MOUNTAIN = new Color(0.6f, 0.55f, 0.5f);
        private static readonly Color COLOR_WATER    = new Color(0.2f, 0.35f, 0.65f);
        private static readonly Color COLOR_RUINS    = new Color(0.5f, 0.45f, 0.35f);
        private static readonly Color COLOR_LOCATION = new Color(1f, 0.85f, 0.3f);

        /// <summary>
        /// The player model's transform, used by PerspectiveCameraController for follow targeting.
        /// </summary>
        public Transform PlayerTransform => _playerModel != null ? _playerModel.transform : null;

        public void SetPrefabMap(OverworldPrefabMapper map) => _prefabMap = map;

        public void Initialize(Dictionary<(int, int), HexTile> tiles)
        {
            if (tiles == null) { Debug.LogError("[Overworld3DRenderer] tiles is null"); return; }
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;

            var tileParentGO = new GameObject("TileParent");
            tileParentGO.transform.SetParent(transform);
            _tileParent = tileParentGO.transform;

            _directionalLight = FindDirectionalLight();

            // Calculate map bounds for the ground plane
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var kv in tiles)
            {
                Vector3 pos = HexToWorld3D(kv.Key.Item1, kv.Key.Item2, 0, hexSize, 0f);
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;
            }

            // Ground plane
            CreateGroundPlane(minX, maxX, minZ, maxZ, hexSize);

            // Shared fallback materials
            var fallbackForestMat = CreateLitMaterial(COLOR_FOREST);
            var fallbackTrunkMat = CreateLitMaterial(new Color(0.35f, 0.25f, 0.15f));

            // Per-tile containers + scatter decoration
            foreach (var kv in tiles)
            {
                var (q, r) = kv.Key;
                var tile = kv.Value;
                Vector3 worldPos = HexToWorld3D(q, r, 0, hexSize, 0f);
                int seed = q * 73 + r * 137;

                var container = new GameObject($"Tile_{q}_{r}");
                container.transform.SetParent(_tileParent);
                container.transform.position = worldPos;

                PlaceScatter(container.transform, tile.Type, seed, hexSize,
                    fallbackForestMat, fallbackTrunkMat);

                _tileInstances[(q, r)] = container;
            }

            CreatePlayerModel(tiles, hexSize, 0f);
            CreateLocationMarkers(tiles, hexSize, 0f);

            int treeCount = 0;
            foreach (var kv in tiles)
                if (kv.Value.Type == TileType.Forest) treeCount++;
            Debug.Log($"[Overworld3DRenderer] Initialized with {tiles.Count} tiles, {treeCount} forest hexes");
        }

        private void CreateGroundPlane(float minX, float maxX, float minZ, float maxZ, float hexSize)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_tileParent);
            float centerX = (minX + maxX) / 2f;
            float centerZ = (minZ + maxZ) / 2f;
            float extentX = (maxX - minX) / 2f + hexSize * 2f;
            float extentZ = (maxZ - minZ) / 2f + hexSize * 2f;
            ground.transform.position = new Vector3(centerX, -0.05f, centerZ);
            ground.transform.localScale = new Vector3(extentX / 5f, 1f, extentZ / 5f);
            DestroyImmediate(ground.GetComponent<Collider>());

            // Always use a simple generated material for the ground plane.
            // Pack materials (Lordenfel etc.) use custom shaders that break on
            // simple geometry. Pack materials work on their own prefab meshes.
            ground.GetComponent<Renderer>().material = CreateLitMaterial(COLOR_PLAINS);
        }

        /// <summary>
        /// Place scatter decoration within a hex tile. Uses real pack prefabs from
        /// the mapper when available (requires URP material conversion), falls back
        /// to procedural primitives.
        /// </summary>
        private void PlaceScatter(Transform parent, TileType type, int seed, float hexSize,
            Material forestMat, Material trunkMat)
        {
            var rng = new System.Random(seed);
            GameObject[] scatter = _prefabMap != null ? _prefabMap.GetScatterPrefabs(type) : null;
            bool hasScatter = scatter != null && scatter.Length > 0;

            switch (type)
            {
                case TileType.Forest:
                    int treeCount = 2 + rng.Next(3);
                    for (int i = 0; i < treeCount; i++)
                    {
                        float offsetX = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.8f;
                        float offsetZ = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.8f;
                        Vector3 offset = new Vector3(offsetX, 0f, offsetZ);
                        float scale = 0.7f + (float)rng.NextDouble() * 0.6f;

                        if (hasScatter)
                        {
                            var prefab = scatter[rng.Next(scatter.Length)];
                            if (prefab != null)
                            {
                                var tree = Instantiate(prefab, parent);
                                tree.transform.localPosition = offset;
                                tree.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                                tree.transform.localScale = Vector3.one * scale * 0.5f;
                                tree.name = $"Tree_{i}";
                                StripMissingScripts(tree);
                            }
                        }
                        else
                        {
                            CreatePrimitiveTree(parent, offset, trunkMat, forestMat, scale);
                        }
                    }
                    break;

                case TileType.Mountain:
                    if (rng.Next(3) == 0)
                    {
                        int count = 1 + rng.Next(2);
                        for (int i = 0; i < count; i++)
                        {
                            float ox = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.6f;
                            float oz = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.6f;
                            float rs = 0.4f + (float)rng.NextDouble() * 0.6f;

                            GameObject[] mScatter = _prefabMap != null ? _prefabMap.MountainScatter : null;
                            if (mScatter != null && mScatter.Length > 0)
                            {
                                var prefab = mScatter[rng.Next(mScatter.Length)];
                                if (prefab != null)
                                {
                                    var obj = Instantiate(prefab, parent);
                                    obj.transform.localPosition = new Vector3(ox, 0f, oz);
                                    obj.transform.localScale = Vector3.one * rs * 0.4f;
                                    obj.name = $"MountainScatter_{i}";
                                    StripMissingScripts(obj);
                                }
                            }
                            else
                            {
                                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                rock.transform.SetParent(parent);
                                rock.transform.localPosition = new Vector3(ox, rs * 0.3f, oz);
                                rock.transform.localScale = new Vector3(rs, rs * 0.6f, rs * 0.8f);
                                rock.GetComponent<Renderer>().sharedMaterial =
                                    CreateLitMaterial(new Color(0.45f, 0.42f, 0.38f));
                                DestroyImmediate(rock.GetComponent<Collider>());
                            }
                        }
                    }
                    break;

                case TileType.Plains:
                    if (rng.Next(6) == 0)
                    {
                        float ox = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.5f;
                        float oz = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.5f;
                        var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        bush.transform.SetParent(parent);
                        bush.transform.localPosition = new Vector3(ox, 0.2f, oz);
                        bush.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                        bush.GetComponent<Renderer>().sharedMaterial =
                            CreateLitMaterial(new Color(0.3f, 0.55f, 0.25f));
                        DestroyImmediate(bush.GetComponent<Collider>());
                    }
                    break;

                case TileType.Road:
                    if (rng.Next(5) == 0)
                    {
                        float ox = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.4f;
                        float oz = ((float)rng.NextDouble() - 0.5f) * hexSize * 0.4f;
                        var rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rubble.transform.SetParent(parent);
                        rubble.transform.localPosition = new Vector3(ox, 0.15f, oz);
                        rubble.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                        rubble.transform.localRotation = Quaternion.Euler(10f, rng.Next(360), 5f);
                        rubble.GetComponent<Renderer>().sharedMaterial =
                            CreateLitMaterial(new Color(0.4f, 0.38f, 0.33f));
                        DestroyImmediate(rubble.GetComponent<Collider>());
                    }
                    break;
            }
        }

        // Missing script warnings from pack prefabs are cosmetic — the visuals render fine.
        private static void StripMissingScripts(GameObject _) { }

        private static void CreatePrimitiveTree(Transform parent, Vector3 offset,
            Material trunkMat, Material canopyMat, float scale)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(parent);
            trunk.transform.localPosition = offset + Vector3.up * scale;
            trunk.transform.localScale = new Vector3(0.15f * scale, scale, 0.15f * scale);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
            DestroyImmediate(trunk.GetComponent<Collider>());

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(parent);
            canopy.transform.localPosition = offset + Vector3.up * (scale * 2.2f);
            canopy.transform.localScale = new Vector3(1.5f * scale, 1.2f * scale, 1.5f * scale);
            canopy.GetComponent<Renderer>().sharedMaterial = canopyMat;
            DestroyImmediate(canopy.GetComponent<Collider>());
        }

        private void CreatePlayerModel(Dictionary<(int, int), HexTile> tiles, float hexSize, float elevScale)
        {
            var gm = GameManager.Instance;
            int startQ = gm != null ? gm.Player.HexQ : 2;
            int startR = gm != null ? gm.Player.HexR : 2;
            int startHeight = 0;
            if (tiles.TryGetValue((startQ, startR), out var startTile))
                startHeight = startTile.Height;

            Vector3 startPos = HexToWorld3D(startQ, startR, startHeight, hexSize, elevScale);

            // Try to load a GLB character model for the player
            bool modelLoaded = false;

            // Use mapper's player prefab if assigned
            if (_prefabMap != null && _prefabMap.PlayerPrefab != null)
            {
                _playerModel = Instantiate(_prefabMap.PlayerPrefab);
                _playerModel.transform.SetParent(transform);
                _playerModel.transform.position = startPos + Vector3.up * 0.1f;
                modelLoaded = true;
            }

            // Try loading a GLB from Resources
            if (!modelLoaded)
            {
                string modelKey = GameManager.Instance?.Character?.ModelId;
                if (string.IsNullOrEmpty(modelKey))
                    modelKey = "Human male fighter";

                var glb = Resources.Load<GameObject>($"Models/NPCs/{modelKey}");
                if (glb != null)
                {
                    _playerModel = Instantiate(glb);
                    _playerModel.transform.SetParent(transform);
                    _playerModel.transform.position = startPos + Vector3.up * 0.1f;
                    _playerModel.transform.localScale = Vector3.one * 0.6f;
                    modelLoaded = true;
                    Debug.Log($"[Overworld3DRenderer] Loaded player model: {modelKey}");
                }
            }

            if (!modelLoaded)
            {
                // Fallback: improved capsule
                _playerModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _playerModel.transform.SetParent(transform);
                _playerModel.transform.position = startPos + Vector3.up * 0.5f;
                _playerModel.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f);
                var mr = _playerModel.GetComponent<Renderer>();
                if (mr != null)
                {
                    var mat = CreateLitMaterial(new Color(0.25f, 0.55f, 0.85f));
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
                    mr.material = mat;
                }
            }

            _playerModel.name = "PlayerModel";
            _playerModel.tag = "Player";
        }

        private void CreateLocationMarkers(Dictionary<(int, int), HexTile> tiles, float hexSize, float elevScale)
        {
            foreach (var loc in LocationData.GetAll())
            {
                int height = 0;
                if (tiles.TryGetValue((loc.HexQ, loc.HexR), out var locTile))
                    height = locTile.Height;

                Vector3 worldPos = HexToWorld3D(loc.HexQ, loc.HexR, height, hexSize, elevScale);

                // Try pack prefab for location marker
                GameObject marker = null;
                if (_prefabMap != null)
                {
                    var prefab = _prefabMap.GetLocationPrefab(loc.Type ?? "town");
                    if (prefab != null)
                    {
                        marker = Instantiate(prefab, transform);
                        marker.transform.position = worldPos;
                        marker.transform.localScale = Vector3.one * 0.35f;
                        StripMissingScripts(marker);
                    }
                }

                if (marker == null)
                {
                    // Fallback: beacon tower
                    marker = new GameObject("MarkerGroup");
                    marker.transform.SetParent(transform);
                    marker.transform.position = worldPos;

                    var markerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    markerBase.transform.SetParent(marker.transform);
                    markerBase.transform.localPosition = Vector3.up * 0.5f;
                    markerBase.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
                    markerBase.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.55f, 0.45f, 0.3f));
                    DestroyImmediate(markerBase.GetComponent<Collider>());

                    var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tower.transform.SetParent(marker.transform);
                    tower.transform.localPosition = Vector3.up * 2f;
                    tower.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
                    tower.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.6f, 0.5f, 0.35f));
                    DestroyImmediate(tower.GetComponent<Collider>());
                }

                // Beacon light at location
                var beaconLight = new GameObject("BeaconLight");
                beaconLight.transform.SetParent(marker.transform);
                beaconLight.transform.localPosition = Vector3.up * 3f;
                var pointLight = beaconLight.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.color = new Color(1f, 0.8f, 0.4f);
                pointLight.intensity = 2f;
                pointLight.range = 8f;

                marker.name = $"Location_{loc.Id}";

                // Location label
                var labelGO = new GameObject($"Label_{loc.Id}");
                labelGO.transform.SetParent(marker.transform);
                labelGO.transform.localPosition = new Vector3(0f, 4f, 0f);

                var tm = labelGO.AddComponent<TextMesh>();
                tm.text = loc.Name;
                tm.characterSize = 0.35f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = COLOR_LOCATION;

                labelGO.AddComponent<Billboard>();

                _locationMarkers[loc.Id] = marker;
            }
        }

        public void UpdateVisuals(int playerQ, int playerR, OverworldFog fog, bool isNight)
        {
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;

            if (_playerModel != null)
                _playerModel.transform.position = new Vector3(
                    _playerModel.transform.position.x, 0.1f, _playerModel.transform.position.z);

            // Tile visibility from fog
            foreach (var kv in _tileInstances)
            {
                var (q, r) = kv.Key;
                var tileGO = kv.Value;
                if (tileGO == null) continue;

                bool visible = fog.IsVisible(q, r);
                bool explored = fog.IsExplored(q, r);
                tileGO.SetActive(visible || explored);
            }

            // Day/night light
            if (_directionalLight != null)
            {
                float targetIntensity = isNight ? 0.3f : 1.4f;
                _directionalLight.intensity = Mathf.Lerp(
                    _directionalLight.intensity, targetIntensity, Time.deltaTime * 2f);
            }

            // Night atmosphere
            var atmos = FindAnyObjectByType<AtmosphereSetup>();
            if (atmos != null)
                atmos.SetNightMode(isNight);

            // Location marker visibility
            foreach (var kv in _locationMarkers)
            {
                var loc = LocationData.Get(kv.Key);
                if (loc != null)
                    kv.Value.SetActive(fog.IsExplored(loc.HexQ, loc.HexR));
            }
        }

        /// <summary>
        /// Convert hex grid coordinates to 3D world position on XZ plane.
        /// </summary>
        public static Vector3 HexToWorld3D(int q, int r, int height, float hexSize, float elevScale)
        {
            float x = hexSize * 1.5f * q;
            float z = hexSize * Mathf.Sqrt(3f) * (r + q * 0.5f);
            float y = height * elevScale;
            return new Vector3(x, y, z);
        }

        private static Color GetFallbackColor(TileType type) => type switch
        {
            TileType.Plains   => COLOR_PLAINS,
            TileType.Forest   => COLOR_FOREST,
            TileType.Mountain => COLOR_MOUNTAIN,
            TileType.Water    => COLOR_WATER,
            TileType.Road     => COLOR_RUINS,
            _                 => COLOR_PLAINS
        };

        private static Material CreateUnlitMaterial(Color color)
        {
            // Clone the lit base material and just set color — more reliable than
            // Shader.Find("Unlit") which can return null in URP at runtime
            var mat = CreateLitMaterial(color);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0f);
            return mat;
        }

        private static bool _shaderLogged;
        private static Material CreateLitMaterial(Color color)
        {
            // Try multiple strategies to get a working shader.
            // Shader.Find can return null in URP if the shader was stripped.
            Shader shader = null;

            // Strategy 1: get the default shader from the render pipeline
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp != null)
                shader = rp.defaultShader;

            // Strategy 2: Shader.Find chain
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Strategy 3: guaranteed built-in shaders
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("[Overworld3DRenderer] No shader found at all!");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            // Log only once
            if (!_shaderLogged) { Debug.Log($"[Overworld3DRenderer] Using shader: {shader.name}"); _shaderLogged = true; }

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            else
                mat.color = color;
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.1f);
            return mat;
        }

        private static Light FindDirectionalLight()
        {
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                    return light;
            }
            return null;
        }
    }

    /// <summary>
    /// Simple billboard that rotates to always face the main camera.
    /// </summary>
    public class Billboard : UnityEngine.MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
                transform.forward = cam.transform.forward;
        }
    }
}
