using System.Collections.Generic;

namespace ForeverEngine.Demo
{
    [System.Serializable]
    public class LocationData
    {
        public string Id;
        public string Name;
        public string Type;
        public int HexQ, HexR;
        public bool IsSafe;
        public string MapType;
        public int InteriorSize = 64;

        private static Dictionary<string, LocationData> _locations;

        public static LocationData Get(string id)
        {
            if (_locations == null) Init();
            return _locations.TryGetValue(id, out var loc) ? loc : null;
        }

        public static List<LocationData> GetAll()
        {
            if (_locations == null) Init();
            return new List<LocationData>(_locations.Values);
        }

        private static void Init()
        {
            _locations = new Dictionary<string, LocationData>
            {
                ["camp"] = new LocationData { Id = "camp", Name = "Survivor's Camp", Type = "camp", HexQ = 2, HexR = 2, IsSafe = true },
                ["shrine"] = new LocationData { Id = "shrine", Name = "Wayfarer's Shrine", Type = "shrine", HexQ = 5, HexR = 3, IsSafe = true },
                ["town"] = new LocationData { Id = "town", Name = "Ashwick Ruins", Type = "town", HexQ = 8, HexR = 5, IsSafe = true, MapType = "village", InteriorSize = 64 },
                ["glade"] = new LocationData { Id = "glade", Name = "Blackwood Glade", Type = "glade", HexQ = 10, HexR = 7, IsSafe = true },
                ["dungeon"] = new LocationData { Id = "dungeon", Name = "The Hollow", Type = "dungeon", HexQ = 12, HexR = 10, IsSafe = false, MapType = "dungeon", InteriorSize = 64 },
                ["fortress"] = new LocationData { Id = "fortress", Name = "Ironhold", Type = "fortress", HexQ = 5, HexR = 15, IsSafe = true, MapType = "castle", InteriorSize = 64 },
                ["castle"] = new LocationData { Id = "castle", Name = "Throne of Rot", Type = "castle", HexQ = 17, HexR = 17, IsSafe = false, MapType = "castle", InteriorSize = 96 }
            };
        }
    }
}
