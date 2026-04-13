using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Generates a simple linear dungeon layout and instantiates room prefabs.
    /// Room 0 = Entrance, last room = Boss, interior rooms are Corridor/Chamber mix.
    /// Falls back to primitive cubes when no prefab is assigned.
    /// </summary>
    public class DungeonAssembler : UnityEngine.MonoBehaviour
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const float RoomSpacing = 12f;   // World units between room centres
        private const float WallHeight  = 3f;
        private const float RoomFloorY  = 0f;

        // ── Lighting presets ─────────────────────────────────────────────────
        private static readonly Dictionary<string, Color> LightPresetColors = new()
        {
            { "torch",    new Color(1.0f, 0.6f, 0.2f) },
            { "dark",     new Color(0.1f, 0.1f, 0.2f) },
            { "boss_glow",new Color(0.6f, 0.0f, 0.8f) },
        };

        // ── Runtime data ─────────────────────────────────────────────────────
        public struct RoomInstance
        {
            public int Index;
            public RoomTag Tag;
            public Vector3 WorldPosition;
            public GameObject Root;
            public Light RoomLight;
            public string LightingPreset;
            /// <summary>
            /// The full intensity the light was created with.
            /// Used by DungeonExplorer to dim visited-but-not-current rooms to 50%.
            /// </summary>
            public float OriginalLightIntensity;
        }

        private RoomInstance[] _rooms;
        public RoomInstance[] Rooms => _rooms;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Build the dungeon layout from a catalog.
        /// Returns the array of instantiated RoomInstances.
        /// Encounter zones are added automatically to Chamber and Boss rooms.
        /// </summary>
        public RoomInstance[] Assemble(RoomCatalog catalog, int roomCount, int seed)
        {
            roomCount = Mathf.Max(3, roomCount); // Minimum: entrance + 1 middle + boss
            _rooms = new RoomInstance[roomCount];

            var rng = new System.Random(seed);

            // Generate a snake-path layout so rooms don't overlap
            var positions = GenerateLayout(roomCount, rng);

            int encounterZoneIndex = 0;

            for (int i = 0; i < roomCount; i++)
            {
                RoomTag tag;
                if (i == 0) tag = RoomTag.Entrance;
                else if (i == roomCount - 1) tag = RoomTag.Boss;
                else tag = (rng.Next(2) == 0) ? RoomTag.Chamber : RoomTag.Corridor;

                RoomEntry entry = catalog?.PickRandom(tag);
                // If no catalog entry exists for this tag fall back gracefully
                if (entry == null && catalog != null)
                    entry = catalog.PickRandom(RoomTag.Corridor);

                string preset = entry?.LightingPreset ?? (tag == RoomTag.Boss ? "boss_glow" : (i == 0 ? "torch" : "dark"));

                Vector3 worldPos = positions[i];

                GameObject root = InstantiateRoom(entry, worldPos, tag, i, seed + i);

                Light roomLight = AddRoomLight(root, worldPos, preset);

                // Entrance starts lit; all others dark (fog of war)
                if (roomLight != null)
                    roomLight.enabled = (i == 0);

                _rooms[i] = new RoomInstance
                {
                    Index                 = i,
                    Tag                   = tag,
                    WorldPosition         = worldPos,
                    Root                  = root,
                    RoomLight             = roomLight,
                    LightingPreset        = preset,
                    OriginalLightIntensity = roomLight != null ? roomLight.intensity : 1.2f,
                };

                // Add encounter zones to combat rooms
                if (tag == RoomTag.Chamber || tag == RoomTag.Boss)
                {
                    AddEncounterZone(root, worldPos, tag, i, encounterZoneIndex);
                    encounterZoneIndex++;
                }
            }

            return _rooms;
        }

        // ── Layout generation ────────────────────────────────────────────────

        private static Vector3[] GenerateLayout(int roomCount, System.Random rng)
        {
            var positions = new Vector3[roomCount];
            var occupied  = new HashSet<(int, int)>();

            int cx = 0, cz = 0;
            occupied.Add((cx, cz));
            positions[0] = new Vector3(cx * RoomSpacing, RoomFloorY, cz * RoomSpacing);

            // Four cardinal directions: N, S, E, W
            int[][] dirs = { new[]{0,1}, new[]{0,-1}, new[]{1,0}, new[]{-1,0} };

            for (int i = 1; i < roomCount; i++)
            {
                // Shuffle directions and pick the first unoccupied cell
                ShuffleDirs(dirs, rng);
                bool moved = false;
                foreach (var d in dirs)
                {
                    int nx = cx + d[0], nz = cz + d[1];
                    if (!occupied.Contains((nx, nz)))
                    {
                        cx = nx; cz = nz;
                        occupied.Add((cx, cz));
                        positions[i] = new Vector3(cx * RoomSpacing, RoomFloorY, cz * RoomSpacing);
                        moved = true;
                        break;
                    }
                }

                // Fallback: step in any direction if surrounded (rare but safe)
                if (!moved)
                {
                    cx += 1;
                    positions[i] = new Vector3(cx * RoomSpacing, RoomFloorY, cz * RoomSpacing);
                    occupied.Add((cx, cz));
                }
            }

            return positions;
        }

        private static void ShuffleDirs(int[][] dirs, System.Random rng)
        {
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = dirs[i];
                dirs[i] = dirs[j];
                dirs[j] = tmp;
            }
        }

        // ── Room instantiation ───────────────────────────────────────────────

        private static GameObject InstantiateRoom(RoomEntry entry, Vector3 worldPos,
                                                   RoomTag tag, int index, int seed)
        {
            GameObject root;

            if (entry?.Prefab != null)
            {
                root = Instantiate(entry.Prefab, worldPos, Quaternion.identity);
                root.name = $"Room_{index}_{tag}";
            }
            else
            {
                // Primitive fallback — floor + 4 walls
                root = new GameObject($"Room_{index}_{tag}");
                root.transform.position = worldPos;

                Vector2Int dim = entry?.Dimensions ?? new Vector2Int(4, 4);
                float w = dim.x;
                float d = dim.y;

                BuildFloor(root.transform, w, d);
                BuildWall(root.transform, new Vector3(0, WallHeight / 2f,  d / 2f), new Vector3(w, WallHeight, 0.2f)); // North
                BuildWall(root.transform, new Vector3(0, WallHeight / 2f, -d / 2f), new Vector3(w, WallHeight, 0.2f)); // South
                BuildWall(root.transform, new Vector3( w / 2f, WallHeight / 2f, 0), new Vector3(0.2f, WallHeight, d)); // East
                BuildWall(root.transform, new Vector3(-w / 2f, WallHeight / 2f, 0), new Vector3(0.2f, WallHeight, d)); // West
            }

            return root;
        }

        private static void BuildFloor(Transform parent, float w, float d)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(w, 0.2f, d);
            ApplyColor(floor, new Color(0.25f, 0.20f, 0.18f));
        }

        private static void BuildWall(Transform parent, Vector3 localPos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            ApplyColor(wall, new Color(0.30f, 0.27f, 0.25f));
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // Try URP Lit first, fall back to Unlit/Color
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;

            var mat = new Material(shader) { color = color };
            renderer.material = mat;
        }

        // ── Lighting ─────────────────────────────────────────────────────────

        private static Light AddRoomLight(GameObject root, Vector3 worldPos, string preset)
        {
            var lightGO = new GameObject("RoomLight");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.position = worldPos + Vector3.up * (WallHeight - 0.5f);

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = RoomSpacing * 0.9f;
            light.intensity = 1.2f;

            if (LightPresetColors.TryGetValue(preset, out var col))
                light.color = col;
            else
                light.color = LightPresetColors["torch"];

            return light;
        }

        // ── Encounter zones ──────────────────────────────────────────────────

        private static void AddEncounterZone(GameObject root, Vector3 worldPos,
                                              RoomTag tag, int roomIndex, int zoneIndex)
        {
            var zoneGO = new GameObject("EncounterZone");
            zoneGO.transform.SetParent(root.transform);
            zoneGO.transform.position = worldPos + Vector3.up * 0.5f;

            // BoxCollider added by EncounterZone [RequireComponent]
            var zone = zoneGO.AddComponent<EncounterZone>();
            zone.EncounterId = $"dungeon_room_{roomIndex}";
            zone.ZoneIndex   = zoneIndex;
            zone.IsBoss      = (tag == RoomTag.Boss);

            // Size the trigger to cover the room floor area
            zoneGO.GetComponent<BoxCollider>().size = new Vector3(3f, 1f, 3f);
        }
    }
}
