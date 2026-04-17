using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;

namespace ForeverEngine.Network
{
    public class ServerStateCache
    {
        public static ServerStateCache Instance { get; private set; }

        // From PlayerUpdate
        public PlayerSnapshot[] AllPlayers { get; private set; } = new PlayerSnapshot[0];
        public string LocalPlayerId { get; set; } = "";

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
