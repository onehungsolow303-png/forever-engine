using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    /// <summary>
    /// 3D overworld renderer that instantiates prefabs from OverworldPrefabMapper
    /// on the XZ plane with elevation. Replaces the procedural hex mesh approach
    /// of OverworldRenderer for the 3D engine transition.
    /// </summary>
    public class Overworld3DRenderer : UnityEngine.MonoBehaviour
    {
        [SerializeField] private OverworldPrefabMapper _prefabMap;

        private Dictionary<(int, int), GameObject> _tileInstances = new();
        private GameObject _playerModel;
        private Dictionary<string, GameObject> _locationMarkers = new();
        private Transform _tileParent;
        private Light _directionalLight;

        // Fallback tile colors when no prefab is assigned
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

        /// <summary>
        /// Assign the prefab map at runtime (used by Overworld3DSetup bootstrapper).
        /// Must be called before Initialize().
        /// </summary>
        public void SetPrefabMap(OverworldPrefabMapper map)
        {
            _prefabMap = map;
        }

        public void Initialize(Dictionary<(int, int), HexTile> tiles)
        {
            if (tiles == null) { Debug.LogError("[Overworld3DRenderer] tiles is null — GameManager not loaded?"); return; }
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;

            // Create parent transform
            var tileParentGO = new GameObject("TileParent");
            tileParentGO.transform.SetParent(transform);
            _tileParent = tileParentGO.transform;

            _directionalLight = FindDirectionalLight();

            // Calculate map bounds for the single ground plane
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

            // Single large ground plane covering the entire map
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_tileParent);
            float centerX = (minX + maxX) / 2f;
            float centerZ = (minZ + maxZ) / 2f;
            float extentX = (maxX - minX) / 2f + hexSize * 2f;
            float extentZ = (maxZ - minZ) / 2f + hexSize * 2f;
            ground.transform.position = new Vector3(centerX, 0f, centerZ);
            ground.transform.localScale = new Vector3(extentX / 5f, 1f, extentZ / 5f);
            DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().material = CreateLitMaterial(COLOR_PLAINS);

            // Shared materials (reuse instead of creating per tile)
            var forestMat = CreateLitMaterial(COLOR_FOREST);
            var trunkMat = CreateLitMaterial(new Color(0.35f, 0.25f, 0.15f));

            // Lightweight per-tile containers (empty GameObjects for fog visibility)
            // and sparse tree decorations
            foreach (var kv in tiles)
            {
                var (q, r) = kv.Key;
                var tile = kv.Value;
                Vector3 worldPos = HexToWorld3D(q, r, 0, hexSize, 0f);
                int seed = q * 73 + r * 137;

                // Empty container for show/hide via fog
                var container = new GameObject($"Tile_{q}_{r}");
                container.transform.SetParent(_tileParent);
                container.transform.position = worldPos;

                // Sparse trees on some forest tiles (reuse shared materials)
                if (tile.Type == TileType.Forest && Mathf.Abs(seed) % 4 == 0)
                {
                    var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    trunk.transform.SetParent(container.transform);
                    trunk.transform.localPosition = new Vector3(0f, 1f, 0f);
                    trunk.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
                    trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
                    DestroyImmediate(trunk.GetComponent<Collider>());

                    var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    canopy.transform.SetParent(container.transform);
                    canopy.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    canopy.transform.localScale = new Vector3(1.5f, 1.2f, 1.5f);
                    canopy.GetComponent<Renderer>().sharedMaterial = forestMat;
                    DestroyImmediate(canopy.GetComponent<Collider>());
                }

                _tileInstances[(q, r)] = container;
            }

            CreatePlayerModel(tiles, hexSize, 0f);
            CreateLocationMarkers(tiles, hexSize, 0f);
        }

        public void UpdateVisuals(int playerQ, int playerR, OverworldFog fog, bool isNight)
        {
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;
            float elevScale = _prefabMap != null ? _prefabMap.ElevationScale : 2f;

            // Player position is controlled by OverworldManager (free movement).
            // Keep Y at ground level.
            if (_playerModel != null)
                _playerModel.transform.position = new Vector3(
                    _playerModel.transform.position.x, 0.1f, _playerModel.transform.position.z);

            // Update tile visibility based on fog (show/hide only — no material modifications)
            foreach (var kv in _tileInstances)
            {
                var (q, r) = kv.Key;
                var tileGO = kv.Value;
                if (tileGO == null) continue;

                bool visible = fog.IsVisible(q, r);
                bool explored = fog.IsExplored(q, r);
                tileGO.SetActive(visible || explored);
            }

            // Adjust directional light for day/night
            if (_directionalLight != null)
            {
                float targetIntensity = isNight ? 0.3f : 1.2f;
                _directionalLight.intensity = Mathf.Lerp(
                    _directionalLight.intensity, targetIntensity, Time.deltaTime * 2f);
            }

            // Update location marker visibility
            foreach (var kv in _locationMarkers)
            {
                var loc = LocationData.Get(kv.Key);
                if (loc != null)
                    kv.Value.SetActive(fog.IsExplored(loc.HexQ, loc.HexR));
            }
        }

        /// <summary>
        /// Convert hex grid coordinates to 3D world position on the XZ plane with Y elevation.
        /// </summary>
        public static Vector3 HexToWorld3D(int q, int r, int height, float hexSize, float elevScale)
        {
            float x = hexSize * 1.5f * q;
            float z = hexSize * Mathf.Sqrt(3f) * (r + q * 0.5f);
            float y = height * elevScale;
            return new Vector3(x, y, z);
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

            // Lightweight capsule player (asset pack models crash Unity)
            _playerModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _playerModel.transform.SetParent(transform);
            _playerModel.transform.position = startPos + Vector3.up * 0.5f;
            _playerModel.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            var playerRenderer = _playerModel.GetComponent<Renderer>();
            if (playerRenderer != null)
                playerRenderer.material = CreateLitMaterial(new Color(0.2f, 0.6f, 1f));

            _playerModel.name = "PlayerModel";
        }

        private void CreateLocationMarkers(Dictionary<(int, int), HexTile> tiles, float hexSize, float elevScale)
        {
            foreach (var loc in LocationData.GetAll())
            {
                int height = 0;
                if (tiles.TryGetValue((loc.HexQ, loc.HexR), out var locTile))
                    height = locTile.Height;

                Vector3 worldPos = HexToWorld3D(loc.HexQ, loc.HexR, height, hexSize, elevScale);

                // Lightweight cylinder marker (asset pack prefabs crash Unity)
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.transform.SetParent(transform);
                marker.transform.position = worldPos + Vector3.up * 1.5f;
                marker.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
                var markerRenderer = marker.GetComponent<Renderer>();
                if (markerRenderer != null)
                    markerRenderer.material = CreateUnlitMaterial(COLOR_LOCATION);
                var markerCol = marker.GetComponent<Collider>();
                if (markerCol != null) Object.Destroy(markerCol);

                marker.name = $"Location_{loc.Id}";

                var labelGO = new GameObject($"Label_{loc.Id}");
                labelGO.transform.SetParent(marker.transform);
                labelGO.transform.localPosition = new Vector3(0f, 3f, 0f);

                var tm = labelGO.AddComponent<TextMesh>();
                tm.text = loc.Name;
                tm.characterSize = 0.3f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = COLOR_LOCATION;

                labelGO.AddComponent<Billboard>();

                _locationMarkers[loc.Id] = marker;
            }
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

        /// <summary>
        /// Returns a biome color with subtle per-tile variation for visual interest.
        /// </summary>
        private static Color GetBiomeColor(TileType type, int seed)
        {
            Color baseColor = GetFallbackColor(type);
            float variation = ((seed * 31 + 17) % 100) / 100f * 0.15f - 0.075f;
            return new Color(
                Mathf.Clamp01(baseColor.r + variation),
                Mathf.Clamp01(baseColor.g + variation * 0.7f),
                Mathf.Clamp01(baseColor.b + variation * 0.5f),
                1f);
        }


        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private static Material CreateLitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
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
    /// Simple billboard component that rotates a transform to always face the main camera.
    /// Attach to TextMesh labels so they remain readable from any camera angle.
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
