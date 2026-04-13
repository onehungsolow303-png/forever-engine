using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    public enum RoomTag { Entrance, Corridor, Chamber, DeadEnd, Boss, Treasure }
    public enum DoorSide { North, South, East, West }

    [Serializable]
    public class DoorPosition
    {
        public DoorSide Side;
        public int Offset;
    }

    [Serializable]
    public class RoomEntry
    {
        public string Id;
        public RoomTag Tag;
        public GameObject Prefab;
        public Vector2Int Dimensions = new(4, 4);
        public DoorPosition[] Doors;
        [Tooltip("torch, dark, boss_glow")]
        public string LightingPreset = "torch";
        [Tooltip("Source asset pack for reference")]
        public string Pack;
    }

    [CreateAssetMenu(menuName = "Forever Engine/Room Catalog")]
    public class RoomCatalog : ScriptableObject
    {
        public RoomEntry[] Rooms;

        public RoomEntry[] GetByTag(RoomTag tag)
        {
            var results = new List<RoomEntry>();
            if (Rooms == null) return results.ToArray();
            foreach (var r in Rooms)
                if (r.Tag == tag) results.Add(r);
            return results.ToArray();
        }

        public RoomEntry PickRandom(RoomTag tag)
        {
            var matching = GetByTag(tag);
            if (matching.Length == 0) return null;
            return matching[UnityEngine.Random.Range(0, matching.Length)];
        }
    }
}
