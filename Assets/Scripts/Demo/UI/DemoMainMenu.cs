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

            // Dev-only CLI flags:
            //   --skip-menu : offline auto-start as Human Warrior
            //   --connect   : online auto-start (spawns ConnectionManager, then Human Warrior)
            bool online = false, skip = false;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "--connect" || arg == "-connect") { online = true; skip = true; }
                else if (arg == "--skip-menu" || arg == "-skip-menu") { skip = true; }
            }
            if (skip)
                StartCoroutine(AutoStart(online));
        }

        private System.Collections.IEnumerator AutoStart(bool online)
        {
            yield return null;
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
                yield return null;
            }
            if (online && ForeverEngine.Network.ConnectionManager.Instance == null)
            {
                var cm = new GameObject("ConnectionManager");
                cm.AddComponent<ForeverEngine.Network.ConnectionManager>();
            }
            Debug.Log($"[DemoMainMenu] {(online ? "--connect" : "--skip-menu")} → auto-starting Human Warrior");
            GameManager.Instance.StartGameWithSheet(RPGBridge.CreateHumanWarrior());
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

            if (GUI.Button(new Rect(x, y, btnW, btnH), "New Game (Offline)", UITheme.Button()))
            {
                if (GameManager.Instance == null)
                {
                    var go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                }
                _showCharSelect = true;
            }

            if (GUI.Button(new Rect(x, y + 50, btnW, btnH), "Connect to Server", UITheme.Button()))
            {
                if (GameManager.Instance == null)
                {
                    var gm = new GameObject("GameManager");
                    gm.AddComponent<GameManager>();
                }
                if (ForeverEngine.Network.ConnectionManager.Instance == null)
                {
                    var cm = new GameObject("ConnectionManager");
                    cm.AddComponent<ForeverEngine.Network.ConnectionManager>();
                }
                _showCharSelect = true;
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
