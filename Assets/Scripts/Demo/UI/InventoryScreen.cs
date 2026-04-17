using UnityEngine;
using ForeverEngine.Core.Enums;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Tab-toggled inventory screen using IMGUI with dark-fantasy UITheme styling.
    /// Shows equipment slots, a scrollable item list, and context-sensitive action
    /// buttons (equip/use/drop) for the selected item.
    ///
    /// Reads from <see cref="ServerStateCache"/> and sends
    /// <see cref="InventoryActionMessage"/> to the server instead of mutating
    /// local state. The server resolves equip/use/drop logic and pushes back an
    /// <see cref="InventoryUpdateMessage"/> + <see cref="StatUpdateMessage"/>.
    ///
    /// Tab opens/closes. Escape closes. <see cref="IsOpen"/> gates other panels.
    /// </summary>
    public class InventoryScreen : UnityEngine.MonoBehaviour
    {
        // ── Public interface ─────────────────────────────────────────────────

        public static InventoryScreen Instance { get; private set; }

        /// <summary>True while the inventory panel is visible.</summary>
        public bool IsOpen { get; private set; }

        // ── Layout constants ─────────────────────────────────────────────────

        private const float PanelW       = 600f;
        private const float PanelH       = 450f;
        private const float EquipColW    = 200f;
        private const float Pad          = 10f;
        private const float RowH         = 22f;
        private const float ActionPanelH = 90f;

        // ── State ────────────────────────────────────────────────────────────

        private Vector2 _scrollPos;
        private int     _selectedIndex = -1;   // index into cache.Inventory

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Tab toggles open/close; Escape always closes
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                IsOpen = !IsOpen;
                if (!IsOpen) _selectedIndex = -1;
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && IsOpen)
            {
                IsOpen = false;
                _selectedIndex = -1;
            }
        }

        // ── IMGUI rendering ──────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;

            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            // Center the panel
            float px = (Screen.width  - PanelW) * 0.5f;
            float py = (Screen.height - PanelH) * 0.5f;

            // Dim background
            UITheme.DrawRect(new Rect(0, 0, Screen.width, Screen.height),
                new Color(0f, 0f, 0f, 0.55f));

            // Main panel
            UITheme.DrawPanel(new Rect(px, py, PanelW, PanelH));

            // Title
            GUI.Label(new Rect(px, py + Pad, PanelW, 24f),
                "INVENTORY", UITheme.Header(UITheme.FontLarge));

            // Separator under title
            UITheme.DrawSeparator(px + Pad, py + 36f, PanelW - Pad * 2f);

            float contentY = py + 42f;
            float contentH = PanelH - 42f - ActionPanelH - Pad;

            // ── Left column: Equipment + Stats ───────────────────────────────
            DrawEquipmentColumn(px + Pad, contentY, EquipColW - Pad, contentH, cache);

            // Vertical divider
            UITheme.DrawRect(new Rect(px + EquipColW, contentY, 1f, contentH),
                new Color(UITheme.PanelBorder.r, UITheme.PanelBorder.g, UITheme.PanelBorder.b, 0.4f));

            // ── Right column: Item list ──────────────────────────────────────
            float listX  = px + EquipColW + Pad;
            float listW  = PanelW - EquipColW - Pad * 2f;
            DrawItemList(listX, contentY, listW, contentH, cache);

            // ── Bottom: Action panel ─────────────────────────────────────────
            float actionY = py + PanelH - ActionPanelH - Pad * 0.5f;
            UITheme.DrawSeparator(px + Pad, actionY - 4f, PanelW - Pad * 2f);
            DrawActionPanel(px + Pad, actionY, PanelW - Pad * 2f, ActionPanelH, cache);

            // Close hint
            GUI.Label(new Rect(px + PanelW - 120f, py + Pad, 110f, 20f),
                "[Tab/Esc] Close", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary, TextAnchor.MiddleRight));
        }

        // ── Left column ──────────────────────────────────────────────────────

        private static void DrawEquipmentColumn(float x, float y, float w, float h, ServerStateCache cache)
        {
            GUI.Label(new Rect(x, y, w, 20f),
                "EQUIPMENT", UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent));

            float ry = y + 22f;

            // Weapon slot
            UITheme.DrawPanelLight(new Rect(x, ry, w, RowH));
            GUI.Label(new Rect(x + 4f, ry, 50f, RowH),
                "Weapon", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
            GUI.Label(new Rect(x + 55f, ry, w - 55f, RowH),
                cache.EquippedWeapon, UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary));
            ry += RowH + 3f;

            // Armor slot
            UITheme.DrawPanelLight(new Rect(x, ry, w, RowH));
            GUI.Label(new Rect(x + 4f, ry, 50f, RowH),
                "Armor", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
            GUI.Label(new Rect(x + 55f, ry, w - 55f, RowH),
                cache.EquippedArmor, UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary));
            ry += RowH + 3f;

            // Separator
            UITheme.DrawSeparator(x, ry + 4f, w);
            ry += 12f;

            // Stats summary
            GUI.Label(new Rect(x, ry, w, 20f),
                "STATS", UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent));
            ry += 22f;

            DrawStatRow(x, ry, w, "HP",   $"{cache.HP} / {cache.MaxHP}"); ry += RowH;
            DrawStatRow(x, ry, w, "AC",   cache.AC.ToString());            ry += RowH;
            DrawStatRow(x, ry, w, "Gold", cache.Gold.ToString());
        }

        private static void DrawStatRow(float x, float y, float w, string label, string value)
        {
            GUI.Label(new Rect(x, y, 50f, RowH),
                label, UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
            GUI.Label(new Rect(x + 52f, y, w - 52f, RowH),
                value, UITheme.Bold(UITheme.FontSmall, UITheme.TextPrimary));
        }

        // ── Right column: item list ──────────────────────────────────────────

        private void DrawItemList(float x, float y, float w, float h, ServerStateCache cache)
        {
            var items = cache.Inventory;
            int count = items != null ? items.Length : 0;

            GUI.Label(new Rect(x, y, w, 20f),
                $"ITEMS  ({count})",
                UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent));

            float listY = y + 22f;
            float listH = h - 22f;

            if (count == 0)
            {
                GUI.Label(new Rect(x, listY + 10f, w, RowH),
                    "No items.", UITheme.Label(UITheme.FontSmall, UITheme.DisabledGray));
                return;
            }

            float innerH = count * (RowH + 2f);
            _scrollPos = GUI.BeginScrollView(
                new Rect(x, listY, w, listH),
                _scrollPos,
                new Rect(0, 0, w - 16f, innerH));

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                float ry  = i * (RowH + 2f);
                var rowRect = new Rect(0, ry, w - 16f, RowH);

                bool selected = _selectedIndex == i;

                // Row background -- highlight selected
                if (selected)
                    UITheme.DrawRect(rowRect, new Color(UITheme.TextAccent.r, UITheme.TextAccent.g, UITheme.TextAccent.b, 0.25f));
                else if (i % 2 == 0)
                    UITheme.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.03f));

                // Click to select
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = selected ? -1 : i;
                    Event.current.Use();
                }

                Color col = selected ? UITheme.TextHeader : UITheme.TextPrimary;
                GUI.Label(new Rect(4f, ry, w - 80f, RowH),
                    item.Name, UITheme.Label(UITheme.FontSmall, col));

                if (item.StackCount > 1)
                    GUI.Label(new Rect(w - 76f, ry, 60f, RowH),
                        $"x{item.StackCount}", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary, TextAnchor.MiddleRight));

                if (item.IsEquipped)
                    GUI.Label(new Rect(w - 52f, ry, 36f, RowH),
                        "[E]", UITheme.Bold(UITheme.FontSmall, UITheme.XPGold, TextAnchor.MiddleRight));
            }

            GUI.EndScrollView();
        }

        // ── Bottom: action panel ─────────────────────────────────────────────

        private void DrawActionPanel(float x, float y, float w, float h, ServerStateCache cache)
        {
            var items = cache.Inventory;
            int count = items != null ? items.Length : 0;

            if (_selectedIndex < 0 || _selectedIndex >= count)
            {
                GUI.Label(new Rect(x, y + 4f, w, RowH),
                    "Select an item to see actions.",
                    UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
                return;
            }

            var item = items[_selectedIndex];

            // Item name row
            GUI.Label(new Rect(x, y + 2f, w * 0.6f, 20f),
                item.Name, UITheme.Bold(UITheme.FontMedium, UITheme.TextHeader));

            // Rarity detail line
            if (!string.IsNullOrEmpty(item.Rarity))
                GUI.Label(new Rect(x, y + 22f, w * 0.6f, 18f),
                    item.Rarity, UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));

            // Action buttons on the right side
            float btnW = 80f;
            float btnH = 26f;
            float btnX = x + w - btnW - 4f;
            float btnY = y + 6f;
            const float btnGap = 30f;

            // Drop is always available
            if (GUI.Button(new Rect(btnX, btnY + btnGap * 2f, btnW, btnH), "Drop", UITheme.Button()))
            {
                NetworkClient.Instance?.Send(new InventoryActionMessage
                {
                    Action = InvActionType.Drop,
                    SlotIndex = item.SlotIndex
                });
                _selectedIndex = -1;
            }

            // Equip / Unequip / Use
            if (item.IsEquipped)
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Unequip", UITheme.Button()))
                {
                    NetworkClient.Instance?.Send(new InventoryActionMessage
                    {
                        Action = InvActionType.Unequip,
                        SlotIndex = item.SlotIndex
                    });
                    _selectedIndex = -1;
                }
            }
            else
            {
                // Primary action: Equip or Use depending on item type.
                // The server knows whether an item is equippable or consumable;
                // we send Equip for gear and Use for consumables.
                string label = IsLikelyConsumable(item.Name) ? "Use" : "Equip";
                var actionType = IsLikelyConsumable(item.Name) ? InvActionType.Use : InvActionType.Equip;

                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), label, UITheme.Button()))
                {
                    NetworkClient.Instance?.Send(new InventoryActionMessage
                    {
                        Action = actionType,
                        SlotIndex = item.SlotIndex
                    });
                    _selectedIndex = -1;
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Heuristic to decide button label. The server is the authority on
        /// what the item actually does; this just picks between "Use" and
        /// "Equip" for the button text.
        /// </summary>
        private static bool IsLikelyConsumable(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("potion") || lower.Contains("food")
                || lower.Contains("water") || lower.Contains("scroll")
                || lower.Contains("elixir") || lower.Contains("ration");
        }
    }
}
