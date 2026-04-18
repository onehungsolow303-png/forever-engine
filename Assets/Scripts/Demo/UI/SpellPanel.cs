using UnityEngine;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Out-of-combat spell panel. Toggle with V key. Reads prepared spells and
    /// slot availability from <see cref="ServerStateCache.CharacterSheet"/> and
    /// sends <see cref="CastSpellRequest"/> to the server. The server resolves
    /// the cast, deducts the slot, and pushes updated stats/sheet back.
    /// </summary>
    public class SpellPanel : UnityEngine.MonoBehaviour
    {
        public static SpellPanel Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private const KeyCode ToggleKey = KeyCode.V;
        private Vector2 _scrollPos;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                if (GameManager.Instance?.IsInCombat == true) return;
                if (DialoguePanel.Instance?.IsOpen == true) return;
                if (InventoryScreen.Instance?.IsOpen == true) return;
                IsOpen = !IsOpen;
            }
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                IsOpen = false;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            var cache = ServerStateCache.Instance;
            if (cache?.CharacterSheet == null) return;

            // Centered panel
            float w = 300, h = 350;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            GUI.Box(new Rect(x, y, w, h), "");

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + 5, w, 30), "Spells", titleStyle);

            // Spell slots
            var sheet = cache.CharacterSheet;
            if (sheet.SpellSlots != null)
            {
                string slots = "Slots: ";
                for (int i = 0; i < sheet.SpellSlots.Length; i++)
                    if (sheet.SpellSlots[i] > 0) slots += $"L{i + 1}:{sheet.SpellSlots[i]} ";
                GUI.Label(new Rect(x + 10, y + 35, w - 20, 20), slots);
            }

            // Spell buttons
            float btnY = y + 60;
            DrawSpellButton(x + 10, ref btnY, w - 20, "Cure Wounds", "cure_wounds", 1, "Heal ~5 HP");
            DrawSpellButton(x + 10, ref btnY, w - 20, "Healing Word", "healing_word", 1, "Heal ~3 HP (bonus)");

            // Close
            if (GUI.Button(new Rect(x + w - 70, y + h - 35, 60, 25), "Close"))
                IsOpen = false;
        }

        private void DrawSpellButton(float x, ref float y, float w, string name, string spellId, int level, string desc)
        {
            GUI.Box(new Rect(x, y, w, 50), "");
            var ns = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            ns.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
            GUI.Label(new Rect(x + 5, y + 5, w - 65, 20), $"{name} (Lv{level})", ns);
            var ds = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            ds.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(x + 5, y + 22, w - 65, 20), desc, ds);
            if (GUI.Button(new Rect(x + w - 55, y + 10, 50, 30), "Cast"))
            {
                NetworkClient.Instance?.Send(new CastSpellRequest { SpellId = spellId, Level = level });
                IsOpen = false;
            }
            y += 55;
        }
    }
}
