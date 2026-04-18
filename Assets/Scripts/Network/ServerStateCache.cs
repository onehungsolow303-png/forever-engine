using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Core.Network;

namespace ForeverEngine.Network
{
    public class ServerStateCache
    {
        public static ServerStateCache Instance { get; private set; }

        // From PlayerUpdate
        public PlayerSnapshot[] AllPlayers { get; private set; } = new PlayerSnapshot[0];
        public string LocalPlayerId { get; set; } = "";

        // --- Spec 7 Phase 1: continuous pose ---
        public UnityEngine.Vector3 LocalPlayerPosition;
        public float LocalPlayerYaw;

        /// <summary>
        /// Per-player snapshot buffer used for server-time interpolation.
        /// The wall-clock <c>receivedAt</c> stamp the old two-pose cache
        /// tracked is replaced by the server tick carried on each PlayerSnapshot.
        /// </summary>
        public readonly System.Collections.Generic.Dictionary<string, SnapshotBuffer>
            PlayerSnapshots = new System.Collections.Generic.Dictionary<string, SnapshotBuffer>();

        /// <summary>
        /// Shared client estimate of current server-loop time. Fed by every
        /// PlayerUpdate arrival; consumed by all per-player interpolators.
        /// </summary>
        public readonly ServerClock Clock = new ServerClock();

        /// <summary>
        /// Client time (<c>Time.timeAsDouble</c>) when each player last sent
        /// a snapshot. Used for stale-despawn independent of the buffer.
        /// </summary>
        public readonly System.Collections.Generic.Dictionary<string, double>
            PlayerLastArrivalClientTime = new System.Collections.Generic.Dictionary<string, double>();

        // From StatUpdate
        public int HP { get; private set; }
        public int MaxHP { get; private set; }
        public int AC { get; private set; }
        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public int Level { get; private set; }
        public int XP { get; private set; }
        public string[] Conditions { get; private set; } = new string[0];

        // From InventoryUpdate
        public ItemDto[] Inventory { get; private set; } = new ItemDto[0];
        public string EquippedWeapon { get; private set; } = "";
        public string EquippedArmor { get; private set; } = "";
        public int Gold { get; private set; }

        // From QuestUpdate
        public QuestDto[] ActiveQuests { get; private set; } = new QuestDto[0];

        // From CharacterSheetData
        public CharacterSheetDto CharacterSheet { get; private set; }

        // ── Spec 7 Phase 3 Task 6: party + dungeon state ──────────────────────

        /// <summary>Current party membership (never null after login — server auto-creates solo party).</summary>
        public PartyInfo CurrentParty;

        /// <summary>Pending invite from another player. Null when none.</summary>
        public (string fromPlayerId, string partyId)? PendingInvite;

        /// <summary>Active dungeon instance id if inside a dungeon; empty otherwise.</summary>
        public string CurrentDungeonInstanceId = "";

        // Derived
        public float HPPercent => MaxHP > 0 ? (float)HP / MaxHP : 0f;
        public float HungerPercent => Hunger / 100f;
        public float ThirstPercent => Thirst / 100f;

        public PlayerSnapshot GetLocalPlayer()
        {
            if (AllPlayers == null) return null;
            foreach (var p in AllPlayers)
                if (p.Id == LocalPlayerId) return p;
            return null;
        }

        public static void CreateInstance()
        {
            Instance = new ServerStateCache();
        }

        public void ApplyPlayerUpdate(PlayerUpdate msg)
        {
            AllPlayers = msg.Players ?? new PlayerSnapshot[0];
        }

        public void ApplyStatUpdate(StatUpdateMessage msg)
        {
            HP = msg.Hp;
            MaxHP = msg.MaxHp;
            AC = msg.Ac;
            Hunger = msg.Hunger;
            Thirst = msg.Thirst;
            Level = msg.Level;
            XP = msg.Xp;
            Conditions = msg.Conditions ?? new string[0];
        }

        public void ApplyInventoryUpdate(InventoryUpdateMessage msg)
        {
            Inventory = msg.Inventory ?? new ItemDto[0];
            EquippedWeapon = msg.EquippedWeapon ?? "";
            EquippedArmor = msg.EquippedArmor ?? "";
            Gold = msg.Gold;
        }

        public void ApplyQuestUpdate(QuestUpdateMessage msg)
        {
            ActiveQuests = msg.ActiveQuests ?? new QuestDto[0];
        }

        public void ApplyCharacterSheet(CharacterSheetDataMessage msg)
        {
            CharacterSheet = msg.Sheet;
        }
    }
}
