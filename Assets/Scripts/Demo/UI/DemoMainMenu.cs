using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class DemoMainMenu : UnityEngine.MonoBehaviour
    {
        // Character creation UI — lazily created so it doesn't block the main menu
        private CharacterCreationUI _charCreation;

        private void Awake()
        {
            // Attach CharacterCreationUI to the same GameObject
            _charCreation = gameObject.AddComponent<CharacterCreationUI>();
        }

        // Premade character descriptions for tooltip
        private static readonly string[] _charNames = {
            "Human Warrior", "Elf Wizard", "Dwarf Cleric", "Halfling Rogue"
        };
        private static readonly string[] _charDescs = {
            "STR 16 | Chain Mail + Shield | Longsword 1d8",
            "INT 16 | No Armor | Spells: Flame Dart, Magic Missile...",
            "WIS 16 | Scale Mail + Shield | Spells: Mending Touch...",
            "DEX 17 | Leather Armor | Shortsword 1d6 + Sneak Attack"
        };

        private bool _showCharSelect;

        private void OnGUI()
        {
            // When character creation is active, it renders itself; don't draw the menu beneath it
            // (CharacterCreationUI.OnGUI draws its own full-screen overlay)

            // Title
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 50), "SHATTERED KINGDOM",
                UITheme.Bold(UITheme.FontHuge, UITheme.TextHeader, TextAnchor.MiddleCenter));

            // Subtitle
            GUI.Label(new Rect(0, Screen.height * 0.2f + 50, Screen.width, 30), "A Forever Engine Demo",
                UITheme.Label(UITheme.FontMedium, UITheme.TextSecondary, TextAnchor.MiddleCenter));

            if (_showCharSelect)
            {
                DrawCharacterSelect();
                return;
            }

            // Main menu buttons
            float btnW = 200, btnH = 40;
            float x = Screen.width / 2 - btnW / 2;
            float y = Screen.height * 0.5f;

            if (GUI.Button(new Rect(x, y, btnW, btnH), "New Game", UITheme.Button()))
            {
                // Ensure GameManager exists before opening character selection
                if (GameManager.Instance == null)
                {
                    var go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                }
                _showCharSelect = true;
            }

            if (SaveManager.HasSave && GUI.Button(new Rect(x, y + 50, btnW, btnH), "Continue", UITheme.Button()))
            {
                if (GameManager.Instance == null)
                {
                    var go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                }
                var player = SaveManager.Load();
                if (player != null)
                {
                    GameManager.Instance.Player = player;
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Overworld");
                }
            }

            if (GUI.Button(new Rect(x, y + 100, btnW, btnH), "Quit", UITheme.Button()))
            {
                Application.Quit();
            }
        }

        private void DrawCharacterSelect()
        {
            float panelW = 500, panelH = 340;
            float px = Screen.width / 2 - panelW / 2;
            float py = Screen.height * 0.3f;

            UITheme.DrawPanel(new Rect(px, py, panelW, panelH));

            // Header
            GUI.Label(new Rect(px, py + 10, panelW, 30), "Choose Your Character",
                UITheme.Header(UITheme.FontLarge));

            float btnW = 440, btnH = 50;
            float bx = px + (panelW - btnW) / 2;
            float by = py + 50;

            for (int i = 0; i < 4; i++)
            {
                float yOff = by + i * (btnH + 10);
                if (GUI.Button(new Rect(bx, yOff, btnW, btnH),
                        $"{_charNames[i]}\n{_charDescs[i]}", UITheme.Button()))
                {
                    SelectPremadeCharacter(i);
                }
            }

            // Back button
            if (GUI.Button(new Rect(bx, by + 4 * (btnH + 10) + 10, 100, 30), "Back", UITheme.Button()))
            {
                _showCharSelect = false;
            }
        }

        private void SelectPremadeCharacter(int index)
        {
            ForeverEngine.RPG.Character.CharacterSheet sheet = index switch
            {
                0 => RPGBridge.CreateHumanWarrior(),
                1 => RPGBridge.CreateElfWizard(),
                2 => RPGBridge.CreateDwarfCleric(),
                3 => RPGBridge.CreateHalflingRogue(),
                _ => RPGBridge.CreateHumanWarrior()
            };

            GameManager.Instance.StartGameWithSheet(sheet);
        }
    }
}
