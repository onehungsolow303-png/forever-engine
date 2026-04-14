using UnityEngine;
using ForeverEngine.Demo;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Tab-toggled inventory screen using IMGUI with dark-fantasy UITheme styling.
    /// Shows equipment slots, a scrollable item list, and context-sensitive action
    /// buttons (equip/use/drop) for the selected item.
    ///
    /// Tab opens/closes. Escape closes. <see cref="IsOpen"/> is a static flag so
    /// other systems (DungeonExplorer, DungeonMinimap) can guard against conflicts.
    /// </summary>
    public class InventoryScreen : UnityEngine.MonoBehaviour
    {
        // ── Public interface ─────────────────────────────────────────────────

        /// <summary>True while the inventory panel is visible.</summary>
        public static bool IsOpen { get; private set; }

        // ── Layout constants ─────────────────────────────────────────────────

        private const float PanelW       = 600f;
        private const float PanelH       = 450f;
        private const float EquipColW    = 200f;
        private const float Pad          = 10f;
        private const float RowH         = 22f;
        private const float ActionPanelH = 90f;

        // ── State ────────────────────────────────────────────────────────────

        private Vector2 _scrollPos;
        private int     _selectedIndex = -1;   // index into GetAllSlots()

        // ── Unity lifecycle ──────────────────────────────────────────────────

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

            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            var player = gm.Player;

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
            DrawEquipmentColumn(px + Pad, contentY, EquipColW - Pad, contentH, player);

            // Vertical divider
            UITheme.DrawRect(new Rect(px + EquipColW, contentY, 1f, contentH),
                new Color(UITheme.PanelBorder.r, UITheme.PanelBorder.g, UITheme.PanelBorder.b, 0.4f));

            // ── Right column: Item list ──────────────────────────────────────
            float listX  = px + EquipColW + Pad;
            float listW  = PanelW - EquipColW - Pad * 2f;
            DrawItemList(listX, contentY, listW, contentH, player);

            // ── Bottom: Action panel ─────────────────────────────────────────
            float actionY = py + PanelH - ActionPanelH - Pad * 0.5f;
            UITheme.DrawSeparator(px + Pad, actionY - 4f, PanelW - Pad * 2f);
            DrawActionPanel(px + Pad, actionY, PanelW - Pad * 2f, ActionPanelH, player);

            // Close hint
            GUI.Label(new Rect(px + PanelW - 120f, py + Pad, 110f, 20f),
                "[Tab/Esc] Close", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary, TextAnchor.MiddleRight));
        }

        // ── Left column ──────────────────────────────────────────────────────

        private static void DrawEquipmentColumn(float x, float y, float w, float h, PlayerData player)
        {
            GUI.Label(new Rect(x, y, w, 20f),
                "EQUIPMENT", UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent));

            float ry = y + 22f;

            // Weapon slot
            UITheme.DrawPanelLight(new Rect(x, ry, w, RowH));
            GUI.Label(new Rect(x + 4f, ry, 50f, RowH),
                "Weapon", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
            GUI.Label(new Rect(x + 55f, ry, w - 55f, RowH),
                player.WeaponName, UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary));
            ry += RowH + 3f;

            // Armor slot
            UITheme.DrawPanelLight(new Rect(x, ry, w, RowH));
            GUI.Label(new Rect(x + 4f, ry, 50f, RowH),
                "Armor", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
            GUI.Label(new Rect(x + 55f, ry, w - 55f, RowH),
                player.ArmorName, UITheme.Label(UITheme.FontSmall, UITheme.TextPrimary));
            ry += RowH + 3f;

            // Separator
            UITheme.DrawSeparator(x, ry + 4f, w);
            ry += 12f;

            // Stats summary
            GUI.Label(new Rect(x, ry, w, 20f),
                "STATS", UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent));
            ry += 22f;

            DrawStatRow(x, ry, w, "HP",   $"{player.HP} / {player.MaxHP}"); ry += RowH;
            DrawStatRow(x, ry, w, "AC",   player.AC.ToString());            ry += RowH;
            DrawStatRow(x, ry, w, "ATK",  player.AttackDice);               ry += RowH;
            DrawStatRow(x, ry, w, "Gold", player.Gold.ToString());          ry += RowH;
        }

        private static void DrawStatRow(float x, float y, float w, string label, string value)
        {
            GUI.Label(new Rect(x, y, 50f, RowH),
                label, UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
            GUI.Label(new Rect(x + 52f, y, w - 52f, RowH),
                value, UITheme.Bold(UITheme.FontSmall, UITheme.TextPrimary));
        }

        // ── Right column: item list ──────────────────────────────────────────

        private void DrawItemList(float x, float y, float w, float h, PlayerData player)
        {
            var inv   = player.Inventory;
            var slots = inv?.GetAllSlots();

            GUI.Label(new Rect(x, y, w, 20f),
                $"ITEMS  ({(slots != null ? slots.Count : 0)}/{(inv != null ? inv.MaxSlots : 0)})",
                UITheme.Bold(UITheme.FontSmall, UITheme.TextAccent));

            float listY = y + 22f;
            float listH = h - 22f;

            if (slots == null || slots.Count == 0)
            {
                GUI.Label(new Rect(x, listY + 10f, w, RowH),
                    "No items.", UITheme.Label(UITheme.FontSmall, UITheme.DisabledGray));
                return;
            }

            float innerH = slots.Count * (RowH + 2f);
            _scrollPos = GUI.BeginScrollView(
                new Rect(x, listY, w, listH),
                _scrollPos,
                new Rect(0, 0, w - 16f, innerH));

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty) continue;

                float ry  = i * (RowH + 2f);
                var rowRect = new Rect(0, ry, w - 16f, RowH);

                bool selected = _selectedIndex == i;

                // Row background — highlight selected
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

                string name  = ItemRegistry.GetName(slot.ItemId);
                Color  col   = selected ? UITheme.TextHeader : UITheme.TextPrimary;
                GUI.Label(new Rect(4f, ry, w - 80f, RowH),
                    name, UITheme.Label(UITheme.FontSmall, col));

                if (slot.StackCount > 1)
                    GUI.Label(new Rect(w - 76f, ry, 60f, RowH),
                        $"x{slot.StackCount}", UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary, TextAnchor.MiddleRight));

                if (slot.Equipped)
                    GUI.Label(new Rect(w - 52f, ry, 36f, RowH),
                        "[E]", UITheme.Bold(UITheme.FontSmall, UITheme.XPGold, TextAnchor.MiddleRight));
            }

            GUI.EndScrollView();
        }

        // ── Bottom: action panel ─────────────────────────────────────────────

        private void DrawActionPanel(float x, float y, float w, float h, PlayerData player)
        {
            var inv   = player.Inventory;
            var slots = inv?.GetAllSlots();

            if (_selectedIndex < 0 || slots == null || _selectedIndex >= slots.Count)
            {
                GUI.Label(new Rect(x, y + 4f, w, RowH),
                    "Select an item to see actions.",
                    UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));
                return;
            }

            var slot = slots[_selectedIndex];
            if (slot.IsEmpty) return;

            string itemName = ItemRegistry.GetName(slot.ItemId);
            int    itemId   = slot.ItemId;

            // Item name + description row
            GUI.Label(new Rect(x, y + 2f, w * 0.6f, 20f),
                itemName, UITheme.Bold(UITheme.FontMedium, UITheme.TextHeader));

            // Context-sensitive detail line
            string detail = GetItemDetail(itemId);
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(x, y + 22f, w * 0.6f, 18f),
                    detail, UITheme.Label(UITheme.FontSmall, UITheme.TextSecondary));

            // Action buttons on the right side
            float btnW = 80f;
            float btnH = 26f;
            float btnX = x + w - btnW - 4f;
            float btnY = y + 6f;
            const float btnGap = 30f;

            // Drop is always available
            if (GUI.Button(new Rect(btnX, btnY + btnGap * 2f, btnW, btnH), "Drop", UITheme.Button()))
            {
                inv.Remove(itemId, 1);
                // If stack is now gone, deselect
                var updated = inv.GetAllSlots();
                bool stillPresent = updated.Exists(s => s.ItemId == itemId && !s.IsEmpty);
                if (!stillPresent) _selectedIndex = -1;
            }

            // Context-specific primary action
            if (IsWeapon(itemId))
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Equip", UITheme.Button()))
                {
                    var weapon = ItemRegistry.TryGetWeapon(itemId);
                    if (weapon != null)
                    {
                        player.WeaponName = weapon.Name;
                        player.AttackDice = weapon.GetDamage().ToString();
                    }
                    else
                    {
                        player.WeaponName = itemName;
                    }
                    inv.Remove(itemId, 1);
                    _selectedIndex = -1;
                }
            }
            else if (IsArmor(itemId))
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Equip", UITheme.Button()))
                {
                    var armor = ItemRegistry.TryGetArmor(itemId);
                    if (armor != null)
                    {
                        player.ArmorName = armor.Name;
                        player.AC        = armor.BaseAC;
                    }
                    else
                    {
                        player.ArmorName = itemName;
                    }
                    inv.Remove(itemId, 1);
                    _selectedIndex = -1;
                }
            }
            else if (itemId == ItemIds.Food)
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Eat", UITheme.Button()))
                {
                    player.Eat(25f);
                    inv.Remove(itemId, 1);
                    var updated = inv.GetAllSlots();
                    if (!updated.Exists(s => s.ItemId == itemId && !s.IsEmpty)) _selectedIndex = -1;
                }
            }
            else if (itemId == ItemIds.Water)
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Drink", UITheme.Button()))
                {
                    player.Drink(25f);
                    inv.Remove(itemId, 1);
                    var updated = inv.GetAllSlots();
                    if (!updated.Exists(s => s.ItemId == itemId && !s.IsEmpty)) _selectedIndex = -1;
                }
            }
            else if (itemId == ItemIds.HealthPotion)
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Use", UITheme.Button()))
                {
                    player.Heal(10);
                    inv.Remove(itemId, 1);
                    var updated = inv.GetAllSlots();
                    if (!updated.Exists(s => s.ItemId == itemId && !s.IsEmpty)) _selectedIndex = -1;
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetItemDetail(int itemId)
        {
            if (itemId == ItemIds.Food)        return "Restores 25 Hunger.";
            if (itemId == ItemIds.Water)       return "Restores 25 Thirst.";
            if (itemId == ItemIds.HealthPotion) return "Restores 10 HP.";

            var weapon = ItemRegistry.TryGetWeapon(itemId);
            if (weapon != null) return $"Damage: {weapon.GetDamage()}";

            var armor = ItemRegistry.TryGetArmor(itemId);
            if (armor != null) return $"AC: {armor.BaseAC}";

            return string.Empty;
        }

        private static bool IsWeapon(int itemId)
        {
            if (ItemRegistry.IsConsumable(itemId)) return false;
            return ItemRegistry.TryGetWeapon(itemId) != null;
        }

        private static bool IsArmor(int itemId)
        {
            if (ItemRegistry.IsConsumable(itemId)) return false;
            return ItemRegistry.TryGetArmor(itemId) != null;
        }
    }
}
