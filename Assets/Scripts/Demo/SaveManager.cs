using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace ForeverEngine.Demo
{
    /// <summary>
    /// Simple JSON save/load for player state. Single save slot.
    /// Auto-saves after battle victory, location discovery, and rest.
    /// </summary>
    public static class SaveManager
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static bool HasSave => File.Exists(SavePath);

        /// <summary>
        /// Load the raw save JSON as a dictionary for sending to Director Hub boot.
        /// Returns null if no save exists or on parse failure.
        /// </summary>
        public static object LoadRaw()
        {
            if (!File.Exists(SavePath)) return null;
            try
            {
                string json = File.ReadAllText(SavePath);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static void Save()
        {
            var gm = GameManager.Instance;
            if (gm?.Player == null) return;

            var data = new SaveData();
            data.FromPlayer(gm.Player);
            data.CurrentSeed = gm.CurrentSeed;

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveManager] Saved to {SavePath}");
        }

        public static PlayerData Load()
        {
            if (!HasSave) return null;

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                var player = data.ToPlayer();
                player.EnsureHPScaled();

                // Restore seed to GameManager
                if (GameManager.Instance != null)
                    GameManager.Instance.CurrentSeed = data.CurrentSeed;

                Debug.Log($"[SaveManager] Loaded from {SavePath} (seed={data.CurrentSeed})");
                return player;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to load: {e.Message}");
                return null;
            }
        }

        public static void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log("[SaveManager] Save deleted");
            }
        }

        /// <summary>
        /// Serializable save data wrapper. Converts HashSets and Inventory
        /// to arrays for JsonUtility compatibility.
        /// </summary>
        [System.Serializable]
        public class SaveData
        {
            public int HexQ, HexR;
            public int HP, MaxHP, AC;
            public int Strength, Dexterity, Constitution;
            public int Speed;
            public string AttackDice;
            public int Level;
            public float Hunger, Thirst;
            public int Gold;
            public int DayCount;
            public string LastSafeLocation;
            public string WeaponName, ArmorName;
            public string[] ExploredHexes;
            public string[] DiscoveredLocations;
            public int CurrentSeed;
            public int SaveVersion = 1;
            public string ModelId;
            public float MaxHunger, MaxThirst;
            public int[] InvItemIds;
            public int[] InvStackCounts;
            public int[] InvMaxStacks;

            // Dungeon state (null/empty if not in dungeon)
            public bool HasDungeonState;
            public string DungeonLocationId;
            public float DungeonPlayerPosX, DungeonPlayerPosY, DungeonPlayerPosZ;
            public float DungeonPlayerRotY;
            public float DungeonCameraOrbit, DungeonCameraDist;
            public int[] DungeonVisitedRooms;
            public int[] DungeonTriggeredEncounters;
            public int DungeonRoomCount;
            public int DungeonBossRoomIndex;
            public bool DungeonBossDefeated;

            public void FromPlayer(PlayerData p)
            {
                HexQ = p.HexQ; HexR = p.HexR;
                HP = p.HP; MaxHP = p.MaxHP; AC = p.AC;
                Strength = p.Strength; Dexterity = p.Dexterity; Constitution = p.Constitution;
                Speed = p.Speed;
                AttackDice = p.AttackDice;
                Level = p.Level;
                Hunger = p.Hunger; Thirst = p.Thirst;
                Gold = p.Gold;
                DayCount = p.DayCount;
                LastSafeLocation = p.LastSafeLocation;
                WeaponName = p.WeaponName; ArmorName = p.ArmorName;

                var hexes = new List<string>(p.ExploredHexes);
                ExploredHexes = hexes.ToArray();
                var locs = new List<string>(p.DiscoveredLocations);
                DiscoveredLocations = locs.ToArray();

                ModelId = p.ModelId;
                MaxHunger = p.MaxHunger;
                MaxThirst = p.MaxThirst;

                // Serialize inventory items as parallel arrays (JsonUtility can't handle struct arrays directly)
                var items = new System.Collections.Generic.List<ForeverEngine.ECS.Data.ItemInstance>();
                for (int i = 0; i < p.Inventory.Count; i++)
                {
                    var item = p.Inventory.GetSlot(i);
                    if (!item.IsEmpty) items.Add(item);
                }
                InvItemIds = new int[items.Count];
                InvStackCounts = new int[items.Count];
                InvMaxStacks = new int[items.Count];
                for (int i = 0; i < items.Count; i++)
                {
                    InvItemIds[i] = items[i].ItemId;
                    InvStackCounts[i] = items[i].StackCount;
                    InvMaxStacks[i] = items[i].MaxStack;
                }

                // Dungeon state
                var dungeon = GameManager.Instance?.PendingDungeonState;
                if (dungeon != null)
                {
                    HasDungeonState = true;
                    DungeonLocationId = dungeon.LocationId;
                    DungeonPlayerPosX = dungeon.PlayerPosition.x;
                    DungeonPlayerPosY = dungeon.PlayerPosition.y;
                    DungeonPlayerPosZ = dungeon.PlayerPosition.z;
                    DungeonPlayerRotY = dungeon.PlayerRotationY;
                    DungeonCameraOrbit = dungeon.CameraOrbitAngle;
                    DungeonCameraDist = dungeon.CameraDistance;
                    DungeonVisitedRooms = new List<int>(dungeon.VisitedRooms).ToArray();
                    DungeonTriggeredEncounters = new List<int>(dungeon.TriggeredEncounters).ToArray();
                    DungeonRoomCount = dungeon.RoomCount;
                    DungeonBossRoomIndex = dungeon.BossRoomIndex;
                    DungeonBossDefeated = dungeon.BossDefeated;
                }
            }

            public PlayerData ToPlayer()
            {
                var p = new PlayerData
                {
                    HexQ = HexQ, HexR = HexR,
                    HP = HP, MaxHP = MaxHP, AC = AC,
                    Strength = Strength, Dexterity = Dexterity, Constitution = Constitution,
                    Speed = Speed,
                    AttackDice = AttackDice,
                    Level = Level,
                    Hunger = Hunger, Thirst = Thirst,
                    Gold = Gold,
                    DayCount = DayCount,
                    LastSafeLocation = LastSafeLocation,
                    WeaponName = WeaponName, ArmorName = ArmorName
                };

                p.ExploredHexes = new HashSet<string>();
                if (ExploredHexes != null)
                    foreach (var h in ExploredHexes) p.ExploredHexes.Add(h);

                p.DiscoveredLocations = new HashSet<string>();
                if (DiscoveredLocations != null)
                    foreach (var l in DiscoveredLocations) p.DiscoveredLocations.Add(l);

                if (!string.IsNullOrEmpty(ModelId)) p.ModelId = ModelId;
                if (MaxHunger > 0) p.MaxHunger = MaxHunger;
                if (MaxThirst > 0) p.MaxThirst = MaxThirst;

                // Restore inventory from parallel arrays
                if (InvItemIds != null && InvItemIds.Length > 0)
                {
                    p.Inventory = new ForeverEngine.ECS.Data.Inventory(20);
                    for (int i = 0; i < InvItemIds.Length; i++)
                    {
                        p.Inventory.Add(new ForeverEngine.ECS.Data.ItemInstance
                        {
                            ItemId = InvItemIds[i],
                            StackCount = InvStackCounts[i],
                            MaxStack = InvMaxStacks[i]
                        });
                    }
                }

                // Restore dungeon state if saved mid-dungeon
                if (HasDungeonState && !string.IsNullOrEmpty(DungeonLocationId))
                {
                    var dungeon = new ForeverEngine.Demo.Dungeon.DungeonState
                    {
                        LocationId = DungeonLocationId,
                        PlayerPosition = new UnityEngine.Vector3(DungeonPlayerPosX, DungeonPlayerPosY, DungeonPlayerPosZ),
                        PlayerRotationY = DungeonPlayerRotY,
                        CameraOrbitAngle = DungeonCameraOrbit,
                        CameraDistance = DungeonCameraDist,
                        RoomCount = DungeonRoomCount,
                        BossRoomIndex = DungeonBossRoomIndex,
                        BossDefeated = DungeonBossDefeated
                    };
                    if (DungeonVisitedRooms != null)
                        foreach (int r in DungeonVisitedRooms) dungeon.VisitedRooms.Add(r);
                    if (DungeonTriggeredEncounters != null)
                        foreach (int e in DungeonTriggeredEncounters) dungeon.TriggeredEncounters.Add(e);

                    if (GameManager.Instance != null)
                        GameManager.Instance.PendingDungeonState = dungeon;
                }

                return p;
            }
        }
    }
}
