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
        private static readonly Color COLOR_PLAINS   = new Color(0.45f, 0.55f, 0.3f);
        private static readonly Color COLOR_FOREST   = new Color(0.2f, 0.4f, 0.15f);
        private static readonly Color COLOR_MOUNTAIN = new Color(0.5f, 0.45f, 0.4f);
        private static readonly Color COLOR_WATER    = new Color(0.15f, 0.25f, 0.5f);
        private static readonly Color COLOR_RUINS    = new Color(0.4f, 0.35f, 0.3f);
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
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;
            float elevScale = _prefabMap != null ? _prefabMap.ElevationScale : 2f;

            // Create parent transform for tile organization
            var tileParentGO = new GameObject("TileParent");
            tileParentGO.transform.SetParent(transform);
            _tileParent = tileParentGO.transform;

            // Cache directional light for day/night
            _directionalLight = FindDirectionalLight();

            // Instantiate tiles
            foreach (var kv in tiles)
            {
                var (q, r) = kv.Key;
                var tile = kv.Value;
                Vector3 worldPos = HexToWorld3D(q, r, tile.Height, hexSize, elevScale);
                int seed = q * 73 + r * 137;

                GameObject prefab = _prefabMap != null ? _prefabMap.GetPrefabForTile(tile.Type, seed) : null;
                GameObject instance;

                if (prefab != null)
                {
                    instance = Instantiate(prefab, worldPos, Quaternion.Euler(0f, seed % 360, 0f), _tileParent);
                }
                else
                {
                    // Fallback: colored plane
                    instance = CreateFallbackTile(worldPos, tile.Type, seed);
                }

                instance.name = $"Tile_{q}_{r}";
                _tileInstances[(q, r)] = instance;
            }

            // Create player model
            CreatePlayerModel(tiles, hexSize, elevScale);

            // Create location markers
            CreateLocationMarkers(tiles, hexSize, elevScale);
        }

        public void UpdateVisuals(int playerQ, int playerR, OverworldFog fog, bool isNight)
        {
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;
            float elevScale = _prefabMap != null ? _prefabMap.ElevationScale : 2f;

            // Lerp player model to target hex position
            if (_playerModel != null)
            {
                int height = 0;
                if (OverworldManager.Instance != null &&
                    OverworldManager.Instance.Tiles.TryGetValue((playerQ, playerR), out var playerTile))
                {
                    height = playerTile.Height;
                }

                Vector3 targetPos = HexToWorld3D(playerQ, playerR, height, hexSize, elevScale);
                _playerModel.transform.position = Vector3.Lerp(
                    _playerModel.transform.position, targetPos, Time.deltaTime * 12f);
            }

            // Update tile visibility based on fog
            foreach (var kv in _tileInstances)
            {
                var (q, r) = kv.Key;
                var tileGO = kv.Value;
                if (tileGO == null) continue;

                if (fog.IsVisible(q, r))
                {
                    tileGO.SetActive(true);
                    SetTileBrightness(tileGO, 1f);
                }
                else if (fog.IsExplored(q, r))
                {
                    tileGO.SetActive(true);
                    SetTileBrightness(tileGO, 0.4f);
                }
                else
                {
                    tileGO.SetActive(false);
                }
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

            if (_prefabMap != null && _prefabMap.PlayerPrefab != null)
            {
                _playerModel = Instantiate(_prefabMap.PlayerPrefab, startPos, Quaternion.identity, transform);
            }
            else
            {
                // Fallback: blue capsule
                _playerModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _playerModel.transform.SetParent(transform);
                _playerModel.transform.position = startPos + Vector3.up * 0.5f;
                var renderer = _playerModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = CreateUnlitMaterial(new Color(0.2f, 0.6f, 1f));
                }
            }

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

                // Marker: yellow cylinder
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.transform.SetParent(transform);
                marker.transform.position = worldPos + Vector3.up * 1.5f;
                marker.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
                marker.name = $"Location_{loc.Id}";

                var markerRenderer = marker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    markerRenderer.material = CreateUnlitMaterial(COLOR_LOCATION);
                }

                // Label: TextMesh that faces camera via Billboard
                var labelGO = new GameObject($"Label_{loc.Id}");
                labelGO.transform.SetParent(marker.transform);
                labelGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);

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

        private GameObject CreateFallbackTile(Vector3 worldPos, TileType type, int seed)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.transform.SetParent(_tileParent);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, seed % 360, 0f);
            // Default Unity plane is 10x10; scale down to hex-appropriate size
            float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;
            float planeScale = hexSize / 10f;
            go.transform.localScale = new Vector3(planeScale, 1f, planeScale);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateUnlitMaterial(GetFallbackColor(type));
            }

            return go;
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

        private static void SetTileBrightness(GameObject tileGO, float brightness)
        {
            var renderers = tileGO.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material.HasProperty("_Color"))
                {
                    var baseColor = r.material.color;
                    r.material.color = new Color(
                        baseColor.r * brightness,
                        baseColor.g * brightness,
                        baseColor.b * brightness,
                        baseColor.a);
                }
            }
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = color;
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
