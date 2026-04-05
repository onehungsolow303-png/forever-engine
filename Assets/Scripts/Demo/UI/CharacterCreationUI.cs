using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.Data;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// OnGUI character creation screen.
    /// Activated by DemoMainMenu "New Game". Player selects species/class/background,
    /// previews derived stats, then presses "Begin Adventure" to start.
    /// </summary>
    public class CharacterCreationUI : UnityEngine.MonoBehaviour
    {
        // ── Visibility ────────────────────────────────────────────────────────
        private bool _visible = false;

        // ── Option lists ──────────────────────────────────────────────────────
        private static readonly string[] Species =
        {
            "Human", "Elf", "Dwarf", "Halfling", "Half-Orc", "Tiefling", "Dragonborn"
        };

        private static readonly string[] Classes =
        {
            "Warrior", "Rogue", "Wizard", "Cleric", "Ranger", "Barbarian", "Paladin", "Bard"
        };

        private static readonly string[] Backgrounds =
        {
            "Soldier", "Criminal", "Acolyte", "Sage", "Outlander", "Noble"
        };

        // Standard array: 15, 14, 13, 12, 10, 8
        private static readonly int[] StandardArray = { 15, 14, 13, 12, 10, 8 };

        // ── Selections ────────────────────────────────────────────────────────
        private int _speciesIdx    = 0;
        private int _classIdx      = 0;
        private int _backgroundIdx = 0;
        private string _characterName = "Adventurer";

        // ── Cached preview stats ──────────────────────────────────────────────
        private int _previewHP;
        private int _previewAC;
        private int _previewSpeed;
        private int _previewSTR, _previewDEX, _previewCON;
        private int _previewINT, _previewWIS, _previewCHA;
        private string _previewHitDie;

        // ── Error message ─────────────────────────────────────────────────────
        private string _errorMessage = "";

        // ── Scroll position for class list ────────────────────────────────────
        private Vector2 _scrollSpecies, _scrollClass, _scrollBg;

        // ── Internal CharacterCreator (created on demand) ─────────────────────
        private CharacterCreator _creator;

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Show()
        {
            _visible = true;
            _errorMessage = "";
            RefreshPreview();
        }

        public void Hide()
        {
            _visible = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unity
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _creator = gameObject.AddComponent<CharacterCreator>();
            _creator.OnValidationError += msg => _errorMessage = msg;
            RefreshPreview();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            DrawBackground();
            DrawTitle();
            DrawColumns();
            DrawStatsPreview();
            DrawNameField();
            DrawConfirmButton();
            DrawErrorMessage();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Layout helpers
        // ─────────────────────────────────────────────────────────────────────

        private void DrawBackground()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawTitle()
        {
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 28,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.9f, 0.8f, 0.4f);
            GUI.Label(new Rect(0, 12, Screen.width, 40), "CHARACTER CREATION", titleStyle);
        }

        private void DrawColumns()
        {
            float colW    = Screen.width / 3f - 20f;
            float colH    = Screen.height * 0.5f;
            float startY  = 60f;
            float col1X   = 10f;
            float col2X   = col1X + colW + 20f;
            float col3X   = col2X + colW + 20f;

            DrawSelectionList("SPECIES",    Species,    ref _speciesIdx,    ref _scrollSpecies,    col1X, startY, colW, colH);
            DrawSelectionList("CLASS",      Classes,    ref _classIdx,      ref _scrollClass,      col2X, startY, colW, colH);
            DrawSelectionList("BACKGROUND", Backgrounds, ref _backgroundIdx, ref _scrollBg,        col3X, startY, colW, colH);
        }

        private void DrawSelectionList(
            string label, string[] options, ref int selectedIdx,
            ref Vector2 scroll, float x, float y, float w, float h)
        {
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);

            GUI.Label(new Rect(x, y, w, 24), label, headerStyle);

            float listY  = y + 28f;
            float listH  = h - 28f;
            float btnH   = 32f;
            float contentH = options.Length * (btnH + 4f);

            scroll = GUI.BeginScrollView(
                new Rect(x, listY, w, listH),
                scroll,
                new Rect(0, 0, w - 16f, contentH));

            for (int i = 0; i < options.Length; i++)
            {
                bool isSelected = (i == selectedIdx);
                var style = new GUIStyle(GUI.skin.button);

                if (isSelected)
                {
                    style.normal.textColor  = Color.black;
                    style.fontStyle         = FontStyle.Bold;
                    GUI.backgroundColor     = new Color(0.9f, 0.8f, 0.3f);
                }
                else
                {
                    style.normal.textColor  = Color.white;
                    GUI.backgroundColor     = new Color(0.2f, 0.2f, 0.3f);
                }

                if (GUI.Button(new Rect(0, i * (btnH + 4f), w - 16f, btnH), options[i], style))
                {
                    if (selectedIdx != i)
                    {
                        selectedIdx = i;
                        RefreshPreview();
                        _errorMessage = "";
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            GUI.EndScrollView();
        }

        private void DrawStatsPreview()
        {
            float panelW = Screen.width - 20f;
            float panelY = Screen.height * 0.62f;
            float panelH = 90f;

            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = Color.white;
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            GUI.Box(new Rect(10f, panelY - 4f, panelW, panelH + 8f), "");
            GUI.backgroundColor = Color.white;

            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            var valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            valueStyle.normal.textColor = new Color(0.9f, 0.8f, 0.4f);

            // Row 1: Derived stats
            string[] statLabels = { "HP", "AC", "SPD", "STR", "DEX", "CON", "INT", "WIS", "CHA" };
            int[]    statValues = { _previewHP, _previewAC, _previewSpeed,
                                    _previewSTR, _previewDEX, _previewCON,
                                    _previewINT, _previewWIS, _previewCHA };

            float cellW = panelW / statValues.Length;
            for (int i = 0; i < statValues.Length; i++)
            {
                float cx = 10f + i * cellW;
                GUI.Label(new Rect(cx, panelY + 2f,  cellW, 20f), statLabels[i], labelStyle.Clone(TextAnchor.MiddleCenter));
                GUI.Label(new Rect(cx, panelY + 22f, cellW, 28f), statValues[i].ToString(), valueStyle);

                // Show modifier beneath ability scores
                if (i >= 3)
                {
                    int mod = AbilityModifier(statValues[i]);
                    string modStr = mod >= 0 ? $"+{mod}" : mod.ToString();
                    var modStyle = new GUIStyle(labelStyle) { fontSize = 11 };
                    modStyle.normal.textColor = mod >= 0 ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.9f, 0.4f, 0.4f);
                    GUI.Label(new Rect(cx, panelY + 50f, cellW, 18f), modStr, modStyle.Clone(TextAnchor.MiddleCenter));
                }
            }

            // Row 2: Hit die and class info
            var infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            infoStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
            string infoLine = $"Hit Die: {_previewHitDie}   |   Class: {Classes[_classIdx]}   |   Species: {Species[_speciesIdx]}   |   Background: {Backgrounds[_backgroundIdx]}";
            GUI.Label(new Rect(10f, panelY + 70f, panelW, 20f), infoLine, infoStyle.Clone(TextAnchor.MiddleCenter));
        }

        private void DrawNameField()
        {
            float fieldY = Screen.height * 0.79f;
            float fieldW = 300f;
            float fieldX = Screen.width / 2f - fieldW / 2f;

            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(fieldX - 110f, fieldY, 100f, 28f), "Name:", labelStyle);

            var fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 13 };
            _characterName = GUI.TextField(new Rect(fieldX, fieldY, fieldW, 28f), _characterName, 30, fieldStyle);
        }

        private void DrawConfirmButton()
        {
            float btnW = 220f;
            float btnH = 44f;
            float btnX = Screen.width / 2f - btnW / 2f;
            float btnY = Screen.height * 0.86f;

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.black;
            GUI.backgroundColor = new Color(0.9f, 0.75f, 0.2f);

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Begin Adventure", style))
            {
                ConfirmCharacter();
            }

            GUI.backgroundColor = Color.white;

            // Back button
            float backW = 100f;
            float backX = btnX - backW - 16f;
            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.4f);
            var backStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            backStyle.normal.textColor = Color.white;
            if (GUI.Button(new Rect(backX, btnY + 4f, backW, btnH - 8f), "Back", backStyle))
            {
                Hide();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawErrorMessage()
        {
            if (string.IsNullOrEmpty(_errorMessage)) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(1f, 0.4f, 0.4f);
            GUI.Label(new Rect(0, Screen.height * 0.93f, Screen.width, 28f), _errorMessage, style);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Character creation logic
        // ─────────────────────────────────────────────────────────────────────

        private void ConfirmCharacter()
        {
            _errorMessage = "";

            if (string.IsNullOrWhiteSpace(_characterName))
            {
                _errorMessage = "Please enter a character name.";
                return;
            }

            // Map UI class name to CharacterCreator class name
            string mappedClass = MapClassName(Classes[_classIdx]);

            _creator.SetName(_characterName.Trim());
            _creator.SetSpecies(Species[_speciesIdx]);
            _creator.SetClass(mappedClass);
            _creator.SetBackground(Backgrounds[_backgroundIdx]);

            // Assign standard array based on class priority
            var scores = AssignStandardArray(mappedClass);
            _creator.SetAbilityScores(scores);

            CharacterData cd = _creator.FinalizeCharacter();
            if (cd == null) return; // validation error was fired

            // Ensure GameManager exists
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }

            GameManager.Instance.StartGameWithCharacter(cd);
            Hide();
        }

        /// <summary>
        /// Maps UI-facing class names to the names CharacterCreator's switch expects.
        /// </summary>
        private static string MapClassName(string uiClassName) => uiClassName switch
        {
            "Warrior"  => "Fighter",
            "Rogue"    => "Rogue",
            "Wizard"   => "Wizard",
            "Cleric"   => "Cleric",
            "Ranger"   => "Ranger",
            "Barbarian"=> "Barbarian",
            "Paladin"  => "Paladin",
            "Bard"     => "Bard",
            _          => uiClassName
        };

        /// <summary>
        /// Assigns the standard array [15,14,13,12,10,8] to ability scores based
        /// on class priority: primary stat gets 15, secondary gets 14, etc.
        /// </summary>
        private static Dictionary<string, int> AssignStandardArray(string className)
        {
            // Priority order: [STR, DEX, CON, INT, WIS, CHA]
            string[] priority = GetClassStatPriority(className);

            string[] allStats = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
            var result = new Dictionary<string, int>();

            // Assign StandardArray values in priority order
            for (int i = 0; i < priority.Length && i < StandardArray.Length; i++)
                result[priority[i]] = StandardArray[i];

            // Fill in any remaining stats not explicitly ordered
            int fillIdx = priority.Length;
            foreach (var stat in allStats)
            {
                if (!result.ContainsKey(stat))
                {
                    result[stat] = fillIdx < StandardArray.Length ? StandardArray[fillIdx] : 8;
                    fillIdx++;
                }
            }

            return result;
        }

        private static string[] GetClassStatPriority(string className) => className.ToLowerInvariant() switch
        {
            "fighter"   => new[] { "STR", "CON", "DEX", "WIS", "CHA", "INT" },
            "barbarian" => new[] { "STR", "CON", "DEX", "WIS", "CHA", "INT" },
            "paladin"   => new[] { "STR", "CHA", "CON", "WIS", "DEX", "INT" },
            "ranger"    => new[] { "DEX", "WIS", "CON", "STR", "INT", "CHA" },
            "rogue"     => new[] { "DEX", "INT", "CON", "CHA", "WIS", "STR" },
            "wizard"    => new[] { "INT", "CON", "DEX", "WIS", "CHA", "STR" },
            "cleric"    => new[] { "WIS", "CON", "STR", "CHA", "DEX", "INT" },
            "bard"      => new[] { "CHA", "DEX", "CON", "WIS", "INT", "STR" },
            _           => new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" }
        };

        // ─────────────────────────────────────────────────────────────────────
        // Preview calculation (mirrors CharacterCreator logic without mutating state)
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshPreview()
        {
            string species    = Species[_speciesIdx];
            string mappedClass = MapClassName(Classes[_classIdx]);

            // Assign standard array
            var scores = AssignStandardArray(mappedClass);
            int str = scores["STR"], dex = scores["DEX"], con = scores["CON"];
            int intel = scores["INT"], wis = scores["WIS"], cha = scores["CHA"];

            // Apply species bonuses
            ApplySpeciesBonusToPreview(species, ref str, ref dex, ref con, ref intel, ref wis, ref cha);

            // Clamp
            str   = Clamp(str,   1, 20);
            dex   = Clamp(dex,   1, 20);
            con   = Clamp(con,   1, 20);
            intel = Clamp(intel, 1, 20);
            wis   = Clamp(wis,   1, 20);
            cha   = Clamp(cha,   1, 20);

            _previewSTR = str; _previewDEX = dex; _previewCON = con;
            _previewINT = intel; _previewWIS = wis; _previewCHA = cha;

            // Hit die by mapped class
            string hitDie = GetHitDie(mappedClass);
            _previewHitDie = hitDie;

            int conMod  = AbilityModifier(con);
            int dexMod  = AbilityModifier(dex);
            int hitSides = InfinityRPGData.HitDieValue(hitDie);

            _previewHP    = Mathf.Max(1, hitSides + conMod);
            _previewAC    = 10 + dexMod;
            _previewSpeed = GetSpeciesSpeed(species);
        }

        private static void ApplySpeciesBonusToPreview(
            string species,
            ref int str, ref int dex, ref int con,
            ref int intel, ref int wis, ref int cha)
        {
            switch (species.ToLowerInvariant())
            {
                case "human":
                    str++; dex++; con++; intel++; wis++; cha++; break;
                case "elf":
                    dex += 2; break;
                case "dwarf":
                    con += 2; break;
                case "halfling":
                    dex += 2; break;
                case "half-orc":
                    str += 2; con++; break;
                case "tiefling":
                    intel++; cha += 2; break;
                case "dragonborn":
                    str += 2; cha++; break;
            }
        }

        private static string GetHitDie(string className) => className.ToLowerInvariant() switch
        {
            "barbarian" => "d12",
            "fighter"   => "d10",
            "paladin"   => "d10",
            "ranger"    => "d10",
            "bard"      => "d8",
            "cleric"    => "d8",
            "rogue"     => "d8",
            "wizard"    => "d6",
            _           => "d8"
        };

        private static int GetSpeciesSpeed(string species) => species.ToLowerInvariant() switch
        {
            "dwarf"    => 25,
            "halfling" => 25,
            "elf"      => 35,
            _          => 30
        };

        // ─────────────────────────────────────────────────────────────────────
        // Utilities
        // ─────────────────────────────────────────────────────────────────────

        private static int AbilityModifier(int score)
        {
            int diff = score - 10;
            return diff >= 0 ? diff / 2 : (diff - 1) / 2;
        }

        private static int Clamp(int val, int min, int max) =>
            val < min ? min : val > max ? max : val;
    }

    // ── GUIStyle extension helper ─────────────────────────────────────────────
    internal static class GUIStyleExtensions
    {
        public static GUIStyle Clone(this GUIStyle src, TextAnchor alignment)
        {
            var s = new GUIStyle(src) { alignment = alignment };
            return s;
        }
    }
}
