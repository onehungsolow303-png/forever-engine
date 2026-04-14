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

                string locType = (loc.Type ?? "town").ToLowerInvariant();
                GameObject marker = CreateLocationMarker(locType, worldPos);
                marker.transform.SetParent(transform);

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

        /// <summary>
        /// Instantiate an asset pack prefab for the location, with a point light for visibility.
        /// Falls back to procedural markers when prefabs aren't assigned.
        /// For camps, places both a brazier and a small building nearby.
        /// </summary>
        private GameObject CreateLocationMarker(string locationType, Vector3 position)
        {
            if (_prefabMap == null) return CreateFallbackMarker(locationType, position);

            // Per-type scale tuning so each location reads well on the overworld
            float scale = locationType switch
            {
                "camp"     => 0.4f,
                "town"     => 0.5f,
                "shrine"   => 0.5f,
                "glade"    => 0.45f,
                "dungeon"  => 0.5f,
                "fortress" => 0.35f,
                "castle"   => 0.3f,
                "ruins"    => 0.45f,
                _          => 0.5f,
            };

            GameObject prefab = locationType switch
            {
                "camp"     => _prefabMap.CampFirePrefab ?? _prefabMap.CampPrefab,
                "town"     => _prefabMap.TownPrefab,
                "shrine"   => _prefabMap.ShrinePrefab,
                "glade"    => _prefabMap.GladePrefab,
                "dungeon"  => _prefabMap.DungeonEntrancePrefab,
                "fortress" => _prefabMap.FortressPrefab,
                "castle"   => _prefabMap.CastlePrefab,
                "ruins"    => _prefabMap.LocationRuinsPrefabs is { Length: > 0 }
                    ? _prefabMap.LocationRuinsPrefabs[Random.Range(0, _prefabMap.LocationRuinsPrefabs.Length)]
                    : null,
                _          => _prefabMap.TownPrefab,
            };

            if (prefab == null) return CreateFallbackMarker(locationType, position);

            var root = new GameObject($"Marker_{locationType}");
            root.transform.position = position;

            var main = Instantiate(prefab, root.transform);
            main.transform.localPosition = Vector3.zero;
            main.transform.localScale = Vector3.one * scale;
            StripMissingScripts(main);

            // For camps: also place a small building (market stall / tent substitute) nearby
            if (locationType == "camp" && _prefabMap.CampPrefab != null
                && _prefabMap.CampFirePrefab != null) // only add building if fire is the primary
            {
                var building = Instantiate(_prefabMap.CampPrefab, root.transform);
                building.transform.localPosition = new Vector3(1.5f, 0f, 0.8f);
                building.transform.localScale = Vector3.one * 0.35f;
                building.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
                StripMissingScripts(building);
            }

            // Add a warm point light so the location is visible
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 2f;
            light.color = locationType switch
            {
                "camp"     => new Color(1f, 0.6f, 0.2f),
                "shrine"   => new Color(0.8f, 0.9f, 1f),
                "dungeon"  => new Color(0.5f, 0.3f, 0.7f),
                "glade"    => new Color(0.4f, 0.9f, 0.4f),
                "fortress" => new Color(0.8f, 0.7f, 0.5f),
                "castle"   => new Color(1f, 0.85f, 0.5f),
                "ruins"    => new Color(0.6f, 0.55f, 0.45f),
                _          => new Color(1f, 0.85f, 0.6f),
            };

            // Strip colliders — these are decorative markers only
            foreach (var col in root.GetComponentsInChildren<Collider>())
                DestroyImmediate(col);

            return root;
        }

        /// <summary>
        /// Procedural fallback markers for when asset pack prefabs aren't assigned.
        /// Each type uses a unique shape + colour so players can identify locations at a glance.
        /// </summary>
        private GameObject CreateFallbackMarker(string locationType, Vector3 position)
        {
            var root = new GameObject($"Marker_{locationType}_fallback");
            root.transform.position = position;

            switch (locationType.ToLowerInvariant())
            {
                case "camp":
                {
                    var fireBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fireBase.transform.SetParent(root.transform);
                    fireBase.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                    fireBase.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
                    fireBase.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.4f, 0.25f, 0.1f));

                    var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    flame.transform.SetParent(root.transform);
                    flame.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                    flame.transform.localScale = Vector3.one * 0.25f;
                    var flameMat = CreateLitMaterial(new Color(1f, 0.5f, 0.1f));
                    if (flameMat.HasProperty("_EmissionColor"))
                    {
                        flameMat.EnableKeyword("_EMISSION");
                        flameMat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0f) * 2f);
                    }
                    flame.GetComponent<Renderer>().material = flameMat;

                    var campLight = root.AddComponent<Light>();
                    campLight.type = LightType.Point;
                    campLight.color = new Color(1f, 0.6f, 0.2f);
                    campLight.intensity = 3f;
                    campLight.range = 5f;
                    break;
                }

                case "town":
                {
                    var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    building.transform.SetParent(root.transform);
                    building.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    building.transform.localScale = new Vector3(0.6f, 1f, 0.5f);
                    building.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.5f, 0.35f, 0.2f));

                    var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roof.transform.SetParent(root.transform);
                    roof.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                    roof.transform.localScale = new Vector3(0.7f, 0.3f, 0.6f);
                    roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    roof.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.6f, 0.15f, 0.1f));

                    var townLight = root.AddComponent<Light>();
                    townLight.type = LightType.Point;
                    townLight.color = new Color(1f, 0.9f, 0.6f);
                    townLight.intensity = 1.5f;
                    townLight.range = 6f;
                    break;
                }

                case "shrine":
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.transform.SetParent(root.transform);
                    pillar.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                    pillar.transform.localScale = new Vector3(0.15f, 0.75f, 0.15f);
                    pillar.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.9f, 0.88f, 0.75f));

                    var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    orb.transform.SetParent(root.transform);
                    orb.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                    orb.transform.localScale = Vector3.one * 0.3f;
                    var orbMat = CreateLitMaterial(new Color(1f, 0.95f, 0.6f));
                    if (orbMat.HasProperty("_EmissionColor"))
                    {
                        orbMat.EnableKeyword("_EMISSION");
                        orbMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 1.5f);
                    }
                    orb.GetComponent<Renderer>().material = orbMat;

                    var shrineLight = root.AddComponent<Light>();
                    shrineLight.type = LightType.Point;
                    shrineLight.color = new Color(0.9f, 0.95f, 1f);
                    shrineLight.intensity = 2f;
                    shrineLight.range = 5f;
                    break;
                }

                case "glade":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = i * 120f * Mathf.Deg2Rad;
                        float rx = Mathf.Cos(angle) * 0.3f;
                        float rz = Mathf.Sin(angle) * 0.3f;
                        float ts = 0.7f + i * 0.1f;

                        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        trunk.transform.SetParent(root.transform);
                        trunk.transform.localPosition = new Vector3(rx, ts * 0.5f, rz);
                        trunk.transform.localScale = new Vector3(0.1f, ts * 0.5f, 0.1f);
                        trunk.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.35f, 0.22f, 0.1f));

                        var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        canopy.transform.SetParent(root.transform);
                        canopy.transform.localPosition = new Vector3(rx, ts * 1.1f, rz);
                        canopy.transform.localScale = Vector3.one * (0.4f + i * 0.05f);
                        canopy.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.2f, 0.6f, 0.2f));
                    }

                    var gladeLight = root.AddComponent<Light>();
                    gladeLight.type = LightType.Point;
                    gladeLight.color = new Color(0.4f, 0.9f, 0.4f);
                    gladeLight.intensity = 1f;
                    gladeLight.range = 4f;
                    break;
                }

                case "dungeon":
                {
                    var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arch.transform.SetParent(root.transform);
                    arch.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                    arch.transform.localScale = new Vector3(0.8f, 1.2f, 0.15f);
                    arch.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.25f, 0.22f, 0.2f));

                    var entrance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    entrance.transform.SetParent(root.transform);
                    entrance.transform.localPosition = new Vector3(0f, 0.45f, -0.01f);
                    entrance.transform.localScale = new Vector3(0.4f, 0.7f, 0.18f);
                    entrance.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.05f, 0.04f, 0.04f));

                    var dungeonLight = root.AddComponent<Light>();
                    dungeonLight.type = LightType.Point;
                    dungeonLight.color = new Color(0.5f, 0.2f, 0.6f);
                    dungeonLight.intensity = 1.5f;
                    dungeonLight.range = 5f;
                    break;
                }

                case "fortress":
                {
                    var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tower.transform.SetParent(root.transform);
                    tower.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                    tower.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f);
                    tower.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.45f, 0.43f, 0.4f));

                    for (int i = 0; i < 4; i++)
                    {
                        float ca = i * 90f * Mathf.Deg2Rad;
                        var crenel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        crenel.transform.SetParent(root.transform);
                        crenel.transform.localPosition = new Vector3(
                            Mathf.Cos(ca) * 0.22f, 1.6f, Mathf.Sin(ca) * 0.22f);
                        crenel.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);
                        crenel.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.4f, 0.38f, 0.35f));
                    }

                    var fortLight = root.AddComponent<Light>();
                    fortLight.type = LightType.Point;
                    fortLight.color = new Color(0.8f, 0.7f, 0.5f);
                    fortLight.intensity = 2f;
                    fortLight.range = 7f;
                    break;
                }

                case "castle":
                {
                    var keep = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    keep.transform.SetParent(root.transform);
                    keep.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    keep.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                    keep.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.42f, 0.4f, 0.38f));

                    Vector3[] turretOffsets = {
                        new Vector3( 0.4f, 0f,  0.4f),
                        new Vector3(-0.4f, 0f, -0.4f),
                    };
                    foreach (var offset in turretOffsets)
                    {
                        var turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        turret.transform.SetParent(root.transform);
                        turret.transform.localPosition = offset + Vector3.up * 0.6f;
                        turret.transform.localScale = new Vector3(0.25f, 0.6f, 0.25f);
                        turret.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.38f, 0.36f, 0.34f));
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        float ca = i * 90f * Mathf.Deg2Rad;
                        var crenel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        crenel.transform.SetParent(root.transform);
                        crenel.transform.localPosition = new Vector3(
                            Mathf.Cos(ca) * 0.28f, 1.9f, Mathf.Sin(ca) * 0.28f);
                        crenel.transform.localScale = new Vector3(0.17f, 0.22f, 0.17f);
                        crenel.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.38f, 0.36f, 0.33f));
                    }

                    var castleLight = root.AddComponent<Light>();
                    castleLight.type = LightType.Point;
                    castleLight.color = new Color(1f, 0.85f, 0.5f);
                    castleLight.intensity = 3f;
                    castleLight.range = 9f;
                    break;
                }

                case "ruins":
                {
                    float[] ruinAngles = { 12f, -8f, 5f };
                    Vector3[] ruinOffsets = {
                        new Vector3(-0.25f, 0.3f,  0.05f),
                        new Vector3( 0.25f, 0.25f, -0.1f),
                        new Vector3( 0f,    0.2f,   0.3f),
                    };
                    for (int i = 0; i < 3; i++)
                    {
                        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wall.transform.SetParent(root.transform);
                        wall.transform.localPosition = ruinOffsets[i];
                        wall.transform.localScale = new Vector3(0.12f, 0.6f, 0.4f);
                        wall.transform.localRotation = Quaternion.Euler(ruinAngles[i], i * 40f, ruinAngles[i] * 0.5f);
                        wall.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.48f, 0.44f, 0.38f));
                    }

                    var ruinsLight = root.AddComponent<Light>();
                    ruinsLight.type = LightType.Point;
                    ruinsLight.color = new Color(0.6f, 0.55f, 0.45f);
                    ruinsLight.intensity = 0.8f;
                    ruinsLight.range = 4f;
                    break;
                }

                default:
                {
                    var defBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    defBase.transform.SetParent(root.transform);
                    defBase.transform.localPosition = Vector3.up * 0.5f;
                    defBase.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
                    defBase.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.55f, 0.45f, 0.3f));

                    var defTower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    defTower.transform.SetParent(root.transform);
                    defTower.transform.localPosition = Vector3.up * 2f;
                    defTower.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
                    defTower.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.6f, 0.5f, 0.35f));

                    var defLight = root.AddComponent<Light>();
                    defLight.type = LightType.Point;
                    defLight.color = new Color(1f, 0.8f, 0.4f);
                    defLight.intensity = 2f;
                    defLight.range = 8f;
                    break;
                }
            }

            // Strip colliders — these are decorative markers only
            foreach (var col in root.GetComponentsInChildren<Collider>())
                DestroyImmediate(col);

            return root;
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
