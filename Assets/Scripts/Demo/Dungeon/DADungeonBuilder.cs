using System.Collections.Generic;
using UnityEngine;
using DungeonArchitect.Builders.Snap;
using DA = DungeonArchitect;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Hooks into Dungeon Architect's Snap builder to set up encounter zones,
    /// fog of war lighting, and room tracking after dungeon generation.
    /// </summary>
    public class DADungeonBuilder : DA.DungeonEventListener
    {
        public struct RoomInfo
        {
            public int Index;
            public string Name;
            public Bounds WorldBounds;
            public GameObject RoomObject;
            public Light FogLight;
            public float OriginalLightIntensity;
            public bool IsEntrance;
            public bool IsBoss;
            public bool IsCorridor;
            public int Tier; // 1, 2, 3 for room tiers, 0 for corridors
        }

        public RoomInfo[] Rooms { get; private set; }
        public int EntranceIndex { get; private set; } = -1;
        public int BossIndex { get; private set; } = -1;
        public SnapQuery Query { get; private set; }

        private Dictionary<int, List<int>> _roomGraph = new();
        public IReadOnlyDictionary<int, List<int>> RoomGraph => _roomGraph;

        public override void OnPostDungeonBuild(DA.Dungeon dungeon, DA.DungeonModel model)
        {
            Query = dungeon.GetComponent<SnapQuery>();
            if (Query == null || Query.modules == null || Query.modules.Length == 0)
            {
                Debug.LogError("[DADungeonBuilder] No modules found after build");
                return;
            }

            var modules = Query.modules;
            Rooms = new RoomInfo[modules.Length];

            for (int i = 0; i < modules.Length; i++)
            {
                var mod = modules[i];
                string name = mod.moduleGameObject != null ? mod.moduleGameObject.name : $"Room_{i}";

                bool isCorridor = name.Contains("Corridor") || name.Contains("Stair");
                bool isBoss = name.Contains("Boss");
                int tier = 0;
                if (name.Contains("Tier1")) tier = 1;
                else if (name.Contains("Tier2")) tier = 2;
                else if (name.Contains("Tier3")) tier = 3;

                // First non-corridor room is entrance, or just the first room
                bool isEntrance = (i == 0);

                // Add fog of war light
                var lightObj = new GameObject($"FogLight_{i}");
                lightObj.transform.position = mod.instanceInfo.WorldBounds.center + Vector3.up * 3f;
                if (mod.moduleGameObject != null)
                    lightObj.transform.SetParent(mod.moduleGameObject.transform);

                var light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = Mathf.Max(mod.instanceInfo.WorldBounds.size.x, mod.instanceInfo.WorldBounds.size.z) * 0.8f;

                // Lighting preset based on room type
                if (isBoss)
                {
                    light.color = new Color(0.8f, 0.3f, 0.3f);
                    light.intensity = 2.0f;
                }
                else if (isCorridor)
                {
                    light.color = new Color(0.4f, 0.45f, 0.5f);
                    light.intensity = 0.8f;
                }
                else
                {
                    light.color = new Color(1f, 0.85f, 0.6f);
                    light.intensity = 1.5f;
                }

                // Start dark (fog of war) except entrance
                light.enabled = isEntrance;

                Rooms[i] = new RoomInfo
                {
                    Index = i,
                    Name = name,
                    WorldBounds = mod.instanceInfo.WorldBounds,
                    RoomObject = mod.moduleGameObject,
                    FogLight = light,
                    OriginalLightIntensity = light.intensity,
                    IsEntrance = isEntrance,
                    IsBoss = isBoss,
                    IsCorridor = isCorridor,
                    Tier = tier,
                };

                if (isEntrance) EntranceIndex = i;
                if (isBoss) BossIndex = i;

                // Place encounter zone in non-corridor rooms
                if (!isCorridor)
                {
                    var zoneObj = new GameObject($"EncounterZone_{i}");
                    zoneObj.transform.position = mod.instanceInfo.WorldBounds.center;
                    if (mod.moduleGameObject != null)
                        zoneObj.transform.SetParent(mod.moduleGameObject.transform);

                    var col = zoneObj.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = mod.instanceInfo.WorldBounds.size * 0.5f; // Trigger in center half of room
                    col.center = Vector3.zero;

                    var zone = zoneObj.AddComponent<EncounterZone>();
                    zone.ZoneIndex = i;
                    zone.IsBoss = isBoss;
                    // Encounter ID encodes biome + room index for EncounterData to resolve
                    zone.EncounterId = isBoss ? "boss_dungeon" : $"random_dungeon_t{tier}_room{i}";
                    zone.Tier = tier;
                }
            }

            // Build room adjacency graph from DA Snap connections
            BuildRoomGraph(dungeon);

            // If no explicit boss found, mark last non-corridor room as boss
            if (BossIndex < 0)
            {
                for (int i = Rooms.Length - 1; i >= 0; i--)
                {
                    if (!Rooms[i].IsCorridor)
                    {
                        BossIndex = i;
                        var r = Rooms[i];
                        r.IsBoss = true;
                        Rooms[i] = r;
                        break;
                    }
                }
            }

            // Disable all rooms except entrance to reduce initial GC pressure
            for (int i = 0; i < Rooms.Length; i++)
            {
                if (Rooms[i].RoomObject != null && i != EntranceIndex)
                    Rooms[i].RoomObject.SetActive(false);
            }

            Debug.Log($"[DADungeonBuilder] Built {Rooms.Length} rooms — entrance:{EntranceIndex}, boss:{BossIndex}");

            // Decorate rooms with props from the RoomCatalog
            var catalog = UnityEngine.Resources.Load<RoomCatalog>("RoomCatalog");
            if (catalog != null && catalog.Props.Count > 0)
            {
                int seed = dungeon.GetInstanceID();
                RoomDecorator.DecorateAll(Rooms, catalog, seed);
            }
        }

        /// <summary>
        /// Get the room index containing the given world position, or -1.
        /// </summary>
        public int GetRoomAtPosition(Vector3 position)
        {
            if (Query != null && Query.GetModuleInfo(position, out var info))
            {
                // Find matching room by bounds
                for (int i = 0; i < Rooms.Length; i++)
                {
                    if (Rooms[i].WorldBounds.Contains(position))
                        return i;
                }
            }
            // Fallback: distance-based
            float closest = float.MaxValue;
            int closestIdx = -1;
            for (int i = 0; i < Rooms.Length; i++)
            {
                float dist = Vector3.Distance(position, Rooms[i].WorldBounds.center);
                if (dist < closest) { closest = dist; closestIdx = i; }
            }
            return closestIdx;
        }

        private void BuildRoomGraph(DA.Dungeon dungeon)
        {
            _roomGraph.Clear();

            // Initialize empty adjacency lists for all rooms
            for (int i = 0; i < Rooms.Length; i++)
                _roomGraph[i] = new List<int>();

            // Build InstanceID → room index mapping
            var idToIndex = new Dictionary<string, int>();
            if (Query != null && Query.modules != null)
            {
                for (int i = 0; i < Query.modules.Length; i++)
                {
                    var instanceId = Query.modules[i].instanceInfo.InstanceID;
                    if (!string.IsNullOrEmpty(instanceId))
                        idToIndex[instanceId] = i;
                }
            }

            // Read connections from SnapModel
            var snapModel = dungeon.GetComponent<DungeonArchitect.Builders.Snap.SnapModel>();
            if (snapModel != null && snapModel.connections != null)
            {
                foreach (var conn in snapModel.connections)
                {
                    if (idToIndex.TryGetValue(conn.ModuleAInstanceID, out int idxA) &&
                        idToIndex.TryGetValue(conn.ModuleBInstanceID, out int idxB))
                    {
                        if (!_roomGraph[idxA].Contains(idxB))
                            _roomGraph[idxA].Add(idxB);
                        if (!_roomGraph[idxB].Contains(idxA))
                            _roomGraph[idxB].Add(idxA);
                    }
                }
            }

            // Fallback: if no connections found, use spatial proximity
            int totalEdges = 0;
            foreach (var edges in _roomGraph.Values) totalEdges += edges.Count;

            if (totalEdges == 0)
            {
                Debug.LogWarning("[DADungeonBuilder] No SnapModel connections found — using spatial proximity fallback");
                for (int i = 0; i < Rooms.Length; i++)
                {
                    for (int j = i + 1; j < Rooms.Length; j++)
                    {
                        float dist = Vector3.Distance(Rooms[i].WorldBounds.center, Rooms[j].WorldBounds.center);
                        float threshold = Mathf.Max(
                            Rooms[i].WorldBounds.size.magnitude,
                            Rooms[j].WorldBounds.size.magnitude) * 0.75f;

                        if (dist < threshold)
                        {
                            _roomGraph[i].Add(j);
                            _roomGraph[j].Add(i);
                        }
                    }
                }

                // Recount
                totalEdges = 0;
                foreach (var edges in _roomGraph.Values) totalEdges += edges.Count;
            }

            Debug.Log($"[DADungeonBuilder] Room graph: {Rooms.Length} nodes, {totalEdges / 2} edges");
        }
    }
}
