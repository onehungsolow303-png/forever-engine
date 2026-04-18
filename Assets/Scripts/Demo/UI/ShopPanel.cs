using UnityEngine;
using ForeverEngine.Core.Enums;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// IMGUI shop panel with buy/sell via server messages.
    /// Left column shows merchant inventory with Buy buttons;
    /// right column shows player inventory with Sell buttons.
    /// Opens when server sends ShopOpenMessage, closes on Escape or Close button.
    /// </summary>
    public class ShopPanel : UnityEngine.MonoBehaviour
    {
        public static ShopPanel Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private string _npcId = "";
        private string _shopName = "";
        private ShopItemDto[] _shopItems = new ShopItemDto[0];
        private int _playerGold;
        private Vector2 _shopScroll;
        private Vector2 _invScroll;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Open(string npcId, string shopName, ShopItemDto[] items, int playerGold)
        {
            _npcId = npcId;
            _shopName = shopName;
            _shopItems = items;
            _playerGold = playerGold;
            IsOpen = true;
        }

        public void UpdateShop(ShopItemDto[] items, int playerGold)
        {
            _shopItems = items;
            _playerGold = playerGold;
        }

        public void Close() => IsOpen = false;

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            float w = 600, h = 400;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            GUI.Box(new Rect(x, y, w, h), "");

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + 5, w, 30), _shopName, titleStyle);

            // Gold display
            GUI.Label(new Rect(x + 10, y + 35, 200, 20), $"Your Gold: {_playerGold}");

            // Left: shop inventory
            GUI.Label(new Rect(x + 10, y + 55, 280, 20), "--- Merchant ---");
            _shopScroll = GUI.BeginScrollView(
                new Rect(x + 5, y + 75, 290, h - 120),
                _shopScroll,
                new Rect(0, 0, 270, _shopItems.Length * 30 + 10));
            for (int i = 0; i < _shopItems.Length; i++)
            {
                var item = _shopItems[i];
                float iy = i * 30;
                string stock = item.Stock < 0 ? "" : $" ({item.Stock})";
                GUI.Label(new Rect(5, iy, 150, 25), $"{item.Name}{stock}");
                GUI.Label(new Rect(155, iy, 50, 25), $"{item.Price}g");
                if (GUI.Button(new Rect(210, iy, 50, 25), "Buy"))
                {
                    NetworkClient.Instance?.Send(new TradeActionMessage
                    {
                        NpcId = _npcId,
                        Action = TradeActionType.Buy,
                        ItemId = item.ItemId,
                        Quantity = 1,
                    });
                }
            }
            GUI.EndScrollView();

            // Right: player inventory (sell)
            GUI.Label(new Rect(x + 310, y + 55, 280, 20), "--- Your Items ---");
            var cache = ServerStateCache.Instance;
            var playerItems = cache?.Inventory ?? new ItemDto[0];
            _invScroll = GUI.BeginScrollView(
                new Rect(x + 305, y + 75, 290, h - 120),
                _invScroll,
                new Rect(0, 0, 270, playerItems.Length * 30 + 10));
            for (int i = 0; i < playerItems.Length; i++)
            {
                var item = playerItems[i];
                float iy = i * 30;
                GUI.Label(new Rect(5, iy, 150, 25), $"{item.Name} x{item.StackCount}");
                if (GUI.Button(new Rect(210, iy, 50, 25), "Sell"))
                {
                    NetworkClient.Instance?.Send(new TradeActionMessage
                    {
                        NpcId = _npcId,
                        Action = TradeActionType.Sell,
                        ItemId = item.ItemId,
                        Quantity = 1,
                    });
                }
            }
            GUI.EndScrollView();

            // Close button
            if (GUI.Button(new Rect(x + w - 70, y + h - 35, 60, 25), "Close"))
                Close();
        }
    }
}
