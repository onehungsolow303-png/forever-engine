# RPG Demo Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire the full RPG character system into the Shattered Kingdom demo -- character selection, D&D 5e combat, spell casting, death saves, conditions, and CR-based encounters.

**Architecture:** Modify 10 existing demo files to use RPG types. Create 1 new bridge file (RPGBridge.cs) for premade characters and CharacterSheet-to-PlayerData sync. Combat flows through AttackResolver->DamageResolver pipeline. Spell casting added as player action. Death saves replace instant death.

**Tech Stack:** Unity 6, existing ForeverEngine.RPG namespace, existing ForeverEngine.Demo namespace

---

## Task 1: RPGBridge + GameManager

**Files:**
- CREATE `Assets/Scripts/Demo/RPGBridge.cs`
- MODIFY `Assets/Scripts/Demo/GameManager.cs`

**Why:** Establish the bridge layer that creates premade CharacterSheets and syncs them to the legacy PlayerData format. Everything else depends on this.

### 1A. Create `Assets/Scripts/Demo/RPGBridge.cs`

This is a new static utility class. It creates the 4 premade characters using `CharacterBuilder.Create()` with explicitly assigned `AbilityScores`, and provides the sync helper that copies CharacterSheet state into PlayerData.

```csharp
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using UnityEngine;

namespace ForeverEngine.Demo
{
    /// <summary>
    /// Bridge between RPG CharacterSheet and Demo PlayerData.
    /// Creates premade characters and syncs CharacterSheet state to PlayerData.
    /// </summary>
    public static class RPGBridge
    {
        // Cached ScriptableObjects (loaded once from Resources)
        private static ClassData _warrior, _wizard, _cleric, _rogue;
        private static SpeciesData _human, _highElf, _hillDwarf, _lightfootHalfling;

        // Weapon/Armor templates
        private static WeaponData _longsword, _quarterstaff, _mace, _shortsword;
        private static ArmorData _chainMail, _scaleMail, _leather, _shield;

        /// <summary>
        /// Load all ScriptableObject assets from Resources folders.
        /// Call once before creating any premade characters.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_warrior != null) return;

            // Classes
            _warrior = Resources.Load<ClassData>("RPG/Classes/Warrior");
            _wizard  = Resources.Load<ClassData>("RPG/Classes/Wizard");
            _cleric  = Resources.Load<ClassData>("RPG/Classes/Cleric");
            _rogue   = Resources.Load<ClassData>("RPG/Classes/Rogue");

            // Species
            _human              = Resources.Load<SpeciesData>("RPG/Species/Human");
            _highElf            = Resources.Load<SpeciesData>("RPG/Species/HighElf");
            _hillDwarf          = Resources.Load<SpeciesData>("RPG/Species/HillDwarf");
            _lightfootHalfling  = Resources.Load<SpeciesData>("RPG/Species/LightfootHalfling");

            // Weapons
            _longsword    = Resources.Load<WeaponData>("RPG/Weapons/Longsword");
            _quarterstaff = Resources.Load<WeaponData>("RPG/Weapons/Quarterstaff");
            _mace         = Resources.Load<WeaponData>("RPG/Weapons/Mace");
            _shortsword   = Resources.Load<WeaponData>("RPG/Weapons/Shortsword");

            // Armor
            _chainMail = Resources.Load<ArmorData>("RPG/Armor/ChainMail");
            _scaleMail = Resources.Load<ArmorData>("RPG/Armor/ScaleMail");
            _leather   = Resources.Load<ArmorData>("RPG/Armor/Leather");
            _shield    = Resources.Load<ArmorData>("RPG/Armor/Shield");
        }

        /// <summary>
        /// Create the Human Warrior premade: STR 16, CON 15, DEX 14.
        /// Longsword (1d8 slashing), Chain Mail (AC 16), Shield (+2).
        /// </summary>
        public static CharacterSheet CreateHumanWarrior()
        {
            EnsureLoaded();
            // Human gets +1 to all scores (per SpeciesData.AbilityBonuses), so base 15 STR -> 16
            var abilities = new AbilityScores(15, 13, 14, 8, 10, 12);
            var sheet = CharacterBuilder.Create("Human Warrior", _human, _warrior, abilities);

            // Equip starting gear
            sheet.MainHand = _longsword;
            sheet.OffHand  = _shield;
            sheet.Armor    = _chainMail;
            sheet.RecalculateAC();
            return sheet;
        }

        /// <summary>
        /// Create the Elf Wizard premade: INT 16, DEX 16, CON 13.
        /// Quarterstaff (1d6 bludgeoning), no armor.
        /// Cantrips: Flame Dart, Arcane Bolt, Frost Ray + 6 L1 prepared spells.
        /// </summary>
        public static CharacterSheet CreateElfWizard()
        {
            EnsureLoaded();
            // High Elf gets +2 DEX, +1 INT via AbilityBonuses => base 14 DEX -> 16, base 15 INT -> 16
            var abilities = new AbilityScores(8, 14, 13, 15, 10, 12);
            var sheet = CharacterBuilder.Create("Elf Wizard", _highElf, _wizard, abilities);

            sheet.MainHand = _quarterstaff;
            sheet.Armor    = null; // No armor
            sheet.RecalculateAC();

            // Load spells from Resources
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Spells/FlameDart",
                "RPG/Spells/ArcaneBolt",
                "RPG/Spells/FrostRay"
            }, isCantrip: true);
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Spells/FlameBurst",
                "RPG/Spells/MagicMissile",
                "RPG/Spells/Shield",
                "RPG/Spells/Sleep",
                "RPG/Spells/MageArmor",
                "RPG/Spells/ThunderWave"
            }, isCantrip: false);

            return sheet;
        }

        /// <summary>
        /// Create the Dwarf Cleric premade: WIS 16, CON 16, STR 13.
        /// Mace (1d6 bludgeoning), Scale Mail (AC 14+DEX max 2), Shield (+2).
        /// Cantrips: Holy Spark, Glow + L1: Mending Touch, Sacred Shield, Guiding Light.
        /// </summary>
        public static CharacterSheet CreateDwarfCleric()
        {
            EnsureLoaded();
            // Hill Dwarf gets +2 CON, +1 WIS => base 14 CON -> 16, base 15 WIS -> 16
            var abilities = new AbilityScores(13, 8, 14, 10, 15, 12);
            var sheet = CharacterBuilder.Create("Dwarf Cleric", _hillDwarf, _cleric, abilities);

            sheet.MainHand = _mace;
            sheet.OffHand  = _shield;
            sheet.Armor    = _scaleMail;
            sheet.RecalculateAC();

            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Spells/HolySpark",
                "RPG/Spells/Glow"
            }, isCantrip: true);
            LoadSpellsFromResources(sheet, new[]
            {
                "RPG/Spells/MendingTouch",
                "RPG/Spells/SacredShield",
                "RPG/Spells/GuidingLight"
            }, isCantrip: false);

            return sheet;
        }

        /// <summary>
        /// Create the Halfling Rogue premade: DEX 17, CON 13, INT 14.
        /// Shortsword (1d6 piercing), Leather Armor (AC 11+DEX).
        /// No spells.
        /// </summary>
        public static CharacterSheet CreateHalflingRogue()
        {
            EnsureLoaded();
            // Lightfoot Halfling gets +2 DEX, +1 CHA => base 15 DEX -> 17
            var abilities = new AbilityScores(10, 15, 13, 14, 8, 12);
            var sheet = CharacterBuilder.Create("Halfling Rogue", _lightfootHalfling, _rogue, abilities);

            sheet.MainHand = _shortsword;
            sheet.Armor    = _leather;
            sheet.RecalculateAC();

            return sheet;
        }

        /// <summary>
        /// Helper: load spell ScriptableObjects from Resources and add to known/prepared lists.
        /// </summary>
        private static void LoadSpellsFromResources(CharacterSheet sheet, string[] paths, bool isCantrip)
        {
            foreach (var path in paths)
            {
                var spell = Resources.Load<ForeverEngine.RPG.Spells.SpellData>(path);
                if (spell == null)
                {
                    Debug.LogWarning($"[RPGBridge] Spell not found at Resources/{path}");
                    continue;
                }
                sheet.KnownSpells.Add(spell);
                sheet.PreparedSpells.Add(spell);
            }
        }

        /// <summary>
        /// Sync a CharacterSheet's current state into a PlayerData for backward compatibility.
        /// Copies HP, MaxHP, AC, ability scores, speed, attack dice, and level.
        /// </summary>
        public static void SyncPlayerFromCharacter(CharacterSheet sheet, PlayerData player)
        {
            if (sheet == null || player == null) return;

            var snap = sheet.ToStatsSnapshot();
            player.HP           = snap.HP;
            player.MaxHP        = snap.MaxHP;
            player.AC           = snap.AC;
            player.Strength     = snap.Strength;
            player.Dexterity    = snap.Dexterity;
            player.Constitution = snap.Constitution;
            player.Speed        = snap.Speed;
            player.Level        = sheet.TotalLevel;

            // Attack dice string from equipped weapon
            if (sheet.MainHand != null)
            {
                var dmg = sheet.MainHand.GetDamage();
                int bonus = dmg.Bonus + sheet.MainHand.MagicBonus;
                player.AttackDice = bonus != 0
                    ? $"{dmg.Count}d{(int)dmg.Die}{(bonus >= 0 ? "+" : "")}{bonus}"
                    : $"{dmg.Count}d{(int)dmg.Die}";
                player.WeaponName = sheet.MainHand.Name;
            }
            else
            {
                player.AttackDice = "1d1+" + snap.AtkDiceBonus;
                player.WeaponName = "Unarmed";
            }

            if (sheet.Armor != null)
                player.ArmorName = sheet.Armor.Name;
        }

        /// <summary>
        /// Get the primary class name for display (e.g., "Wizard").
        /// </summary>
        public static string GetClassName(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return "Adventurer";
            return sheet.ClassLevels[0].ClassRef != null ? sheet.ClassLevels[0].ClassRef.Name : "Adventurer";
        }

        /// <summary>
        /// Get the casting ability for the primary class.
        /// </summary>
        public static Ability GetCastingAbility(CharacterSheet sheet)
        {
            if (sheet == null || sheet.ClassLevels.Count == 0) return Ability.INT;
            return sheet.ClassLevels[0].ClassRef != null
                ? sheet.ClassLevels[0].ClassRef.SpellcastingAbility
                : Ability.INT;
        }

        /// <summary>
        /// Check if the primary class is proficient in CON saves.
        /// Needed for concentration checks.
        /// </summary>
        public static bool IsProficientConSave(CharacterSheet sheet)
        {
            return sheet != null && sheet.IsProficient("Save:CON");
        }
    }
}
```

### 1B. Modify `Assets/Scripts/Demo/GameManager.cs`

Add `CharacterSheet Character` property, `SyncPlayerFromCharacter()` method, and a new `StartGameWithSheet()` entry point used by the updated main menu.

**Old code (lines 1-4):**
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.CharacterCreation;

namespace ForeverEngine.Demo
```

**New code:**
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using ForeverEngine.MonoBehaviour.CharacterCreation;
using ForeverEngine.RPG.Character;

namespace ForeverEngine.Demo
```

**Old code (lines 11-12):**
```csharp
        public PlayerData Player { get; private set; }
        public CharacterData CharacterData { get; private set; }
```

**New code:**
```csharp
        public PlayerData Player { get; private set; }
        public CharacterData CharacterData { get; private set; }
        public CharacterSheet Character { get; set; }
```

**Add new method after `StartGameWithCharacter()` (after line 46):**

Insert the following new methods:

```csharp
        /// <summary>
        /// Called by the premade character selection buttons.
        /// Creates PlayerData from the CharacterSheet, then loads Overworld.
        /// </summary>
        public void StartGameWithSheet(CharacterSheet sheet, int seed = 0)
        {
            Character     = sheet;
            CurrentSeed   = seed > 0 ? seed : Random.Range(1, 99999);
            Player        = new PlayerData { HexQ = 2, HexR = 2 };
            Player.DiscoveredLocations.Add("camp");
            SyncPlayerFromCharacter();
            SceneManager.LoadScene("Overworld");
        }

        /// <summary>
        /// Copy CharacterSheet state into PlayerData for backward compatibility.
        /// Call after character creation, level up, equip changes, and rest.
        /// </summary>
        public void SyncPlayerFromCharacter()
        {
            if (Character == null || Player == null) return;
            RPGBridge.SyncPlayerFromCharacter(Character, Player);
        }
```

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): add RPGBridge and CharacterSheet field on GameManager`

---

## Task 2: DemoMainMenu Character Selection

**Files:**
- MODIFY `Assets/Scripts/Demo/UI/DemoMainMenu.cs`

**Why:** Replace the generic "New Game" flow with 4 premade character buttons so the player picks a class.

### Full replacement of `DemoMainMenu.cs`

**Old code (entire OnGUI method, lines 17-55):**
```csharp
        private void OnGUI()
        {
            // When character creation is active, it renders itself; don't draw the menu beneath it
            // (CharacterCreationUI.OnGUI draws its own full-screen overlay)

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 50), "SHATTERED KINGDOM", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.2f + 50, Screen.width, 30), "A Forever Engine Demo", subStyle);

            // Buttons
            float btnW = 200, btnH = 40;
            float x = Screen.width / 2 - btnW / 2;
            float y = Screen.height * 0.5f;

            if (GUI.Button(new Rect(x, y, btnW, btnH), "New Game"))
            {
                // Ensure GameManager exists before opening character creation
                if (GameManager.Instance == null)
                {
                    var go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                }
                _charCreation.Show();
            }

            if (GUI.Button(new Rect(x, y + 50, btnW, btnH), "Continue"))
            {
                var sm = ForeverEngine.MonoBehaviour.SaveLoad.SaveManager.Instance;
                if (sm != null) sm.Load("quicksave");
            }

            if (GUI.Button(new Rect(x, y + 100, btnW, btnH), "Quit"))
            {
                Application.Quit();
            }
        }
```

**New code:**
```csharp
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
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 50), "SHATTERED KINGDOM", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.2f + 50, Screen.width, 30), "A Forever Engine Demo", subStyle);

            if (_showCharSelect)
            {
                DrawCharacterSelect();
                return;
            }

            // Main menu buttons
            float btnW = 200, btnH = 40;
            float x = Screen.width / 2 - btnW / 2;
            float y = Screen.height * 0.5f;

            if (GUI.Button(new Rect(x, y, btnW, btnH), "New Game"))
            {
                // Ensure GameManager exists before opening character selection
                if (GameManager.Instance == null)
                {
                    var go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                }
                _showCharSelect = true;
            }

            if (GUI.Button(new Rect(x, y + 50, btnW, btnH), "Continue"))
            {
                var sm = ForeverEngine.MonoBehaviour.SaveLoad.SaveManager.Instance;
                if (sm != null) sm.Load("quicksave");
            }

            if (GUI.Button(new Rect(x, y + 100, btnW, btnH), "Quit"))
            {
                Application.Quit();
            }
        }

        private void DrawCharacterSelect()
        {
            float panelW = 500, panelH = 340;
            float px = Screen.width / 2 - panelW / 2;
            float py = Screen.height * 0.3f;
            GUI.Box(new Rect(px, py, panelW, panelH), "");

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(px, py + 10, panelW, 30), "Choose Your Character", headerStyle);

            float btnW = 440, btnH = 50;
            float bx = px + (panelW - btnW) / 2;
            float by = py + 50;

            for (int i = 0; i < 4; i++)
            {
                float yOff = by + i * (btnH + 10);
                if (GUI.Button(new Rect(bx, yOff, btnW, btnH), $"{_charNames[i]}\n{_charDescs[i]}"))
                {
                    SelectPremadeCharacter(i);
                }
            }

            // Back button
            if (GUI.Button(new Rect(bx, by + 4 * (btnH + 10) + 10, 100, 30), "Back"))
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
```

Note: The `_charCreation` field and `Awake()` method stay unchanged -- the old character creation UI remains available as a fallback but is not triggered by the new flow.

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): add premade character selection to main menu`

---

## Task 3: BattleCombatant Enhancement

**Files:**
- MODIFY `Assets/Scripts/Demo/Battle/BattleCombatant.cs`

**Why:** BattleCombatant needs CharacterSheet, ConditionManager, DeathSaveTracker, ConcentrationTracker, and damage type fields so the new combat pipeline can operate on RPG data.

### Full replacement of `BattleCombatant.cs`

**Old code (lines 1-2):**
```csharp
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.Demo.Battle
```

**New code:**
```csharp
using ForeverEngine.ECS.Utility;
using ForeverEngine.RPG.Character;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;

namespace ForeverEngine.Demo.Battle
```

**Old code (fields, lines 6-16):**
```csharp
    public class BattleCombatant
    {
        public string Name;
        public int X, Y;
        public int HP, MaxHP, AC;
        public int Strength, Dexterity, Speed;
        public int AtkCount, AtkSides, AtkBonus;
        public string Behavior;
        public bool IsPlayer;
        public bool IsAlive => HP > 0;
        public int MovementRemaining;
        public bool HasAction;
        public int InitiativeRoll;
```

**New code:**
```csharp
    public class BattleCombatant
    {
        // === Core fields (unchanged) ===
        public string Name;
        public int X, Y;
        public int HP, MaxHP, AC;
        public int Strength, Dexterity, Speed;
        public int AtkCount, AtkSides, AtkBonus;
        public string Behavior;
        public bool IsPlayer;
        public bool IsAlive => HP > 0 || (IsPlayer && DeathSaves != null && DeathSaves.IsActive);
        public int MovementRemaining;
        public bool HasAction;
        public int InitiativeRoll;

        // === RPG Integration fields ===
        public CharacterSheet Sheet;
        public ConditionManager Conditions = new ConditionManager();
        public DeathSaveTracker DeathSaves;
        public ConcentrationTracker Concentration;

        // Damage type info for the resolver pipeline
        public DamageType Resistances;
        public DamageType Vulnerabilities;
        public DamageType Immunities;
        public DamageType AttackDamageType = DamageType.Slashing;

        // Temp HP (tracked here for non-sheet combatants; sheet combatants use Sheet.TempHP)
        public int TempHP;
```

**Old method `TakeDamage` (line 27):**
```csharp
        public void TakeDamage(int amount) { HP = System.Math.Max(0, HP - amount); }
```

**New method:**
```csharp
        /// <summary>
        /// Apply raw HP damage (after resistance/tempHP already resolved by DamageResolver).
        /// </summary>
        public void TakeDamage(int amount)
        {
            HP = System.Math.Max(0, HP - amount);
            if (Sheet != null) Sheet.HP = HP;
        }

        /// <summary>
        /// Apply healing. Resets death saves if revived from 0 HP.
        /// </summary>
        public void Heal(int amount)
        {
            bool wasAtZero = HP <= 0;
            HP = System.Math.Min(MaxHP, HP + amount);
            if (Sheet != null) Sheet.HP = HP;
            if (wasAtZero && HP > 0 && DeathSaves != null)
                DeathSaves.Reset();
        }
```

**Old `StartTurn` (line 28):**
```csharp
        public void StartTurn() { MovementRemaining = Speed; HasAction = true; }
```

**New `StartTurn`:**
```csharp
        public void StartTurn()
        {
            MovementRemaining = Speed;
            HasAction = true;
            // Tick condition durations at start of this combatant's turn
            if (Conditions != null)
                Conditions.TickDurations();
        }
```

**After existing `FromEnemy` factory (line 52), add `FromCharacterSheet` factory:**

```csharp
        /// <summary>
        /// Create a BattleCombatant from a full CharacterSheet (player).
        /// </summary>
        public static BattleCombatant FromCharacterSheet(CharacterSheet sheet)
        {
            var snap = sheet.ToStatsSnapshot();
            return new BattleCombatant
            {
                Name = sheet.Name,
                X = 1, Y = 1,
                IsPlayer = true,
                Sheet = sheet,
                HP = snap.HP,
                MaxHP = snap.MaxHP,
                AC = snap.AC,
                Strength = snap.Strength,
                Dexterity = snap.Dexterity,
                Speed = snap.Speed,
                AtkCount = snap.AtkDiceCount,
                AtkSides = snap.AtkDiceSides,
                AtkBonus = snap.AtkDiceBonus,
                Behavior = "player",
                Conditions = sheet.Conditions,
                DeathSaves = sheet.DeathSaves,
                Concentration = sheet.Concentration,
                TempHP = sheet.TempHP,
                AttackDamageType = sheet.MainHand != null ? sheet.MainHand.Type : DamageType.Bludgeoning
            };
        }
```

**Modify existing `FromEnemy` to initialize ConditionManager (line 42-52):**

**Old:**
```csharp
        public static BattleCombatant FromEnemy(Encounters.EnemyDef def, int x, int y)
        {
            DiceRoller.Parse(def.AtkDice, out int c, out int s, out int b);
            return new BattleCombatant
            {
                Name = def.Name, X = x, Y = y, IsPlayer = false,
                HP = def.HP, MaxHP = def.HP, AC = def.AC,
                Strength = def.Str, Dexterity = def.Dex, Speed = def.Spd,
                AtkCount = c, AtkSides = s, AtkBonus = b, Behavior = def.Behavior
            };
        }
```

**New:**
```csharp
        public static BattleCombatant FromEnemy(Encounters.EnemyDef def, int x, int y)
        {
            DiceRoller.Parse(def.AtkDice, out int c, out int s, out int b);
            return new BattleCombatant
            {
                Name = def.Name, X = x, Y = y, IsPlayer = false,
                HP = def.HP, MaxHP = def.HP, AC = def.AC,
                Strength = def.Str, Dexterity = def.Dex, Speed = def.Spd,
                AtkCount = c, AtkSides = s, AtkBonus = b, Behavior = def.Behavior,
                Conditions = new ConditionManager(),
                Resistances = def.Resistances,
                Vulnerabilities = def.Vulnerabilities,
                Immunities = def.Immunities,
                AttackDamageType = def.AttackDamageType
            };
        }
```

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): add RPG fields and FromCharacterSheet factory to BattleCombatant`

---

## Task 4: BattleManager Combat Overhaul

**Files:**
- MODIFY `Assets/Scripts/Demo/Battle/BattleManager.cs`

**Why:** Replace the simple hit/miss ResolveAttack with the full AttackResolver->DamageResolver pipeline. Add death save handling and condition awareness. This is the largest single task.

### 4A. Add using directives

**Old (lines 1-4):**
```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ForeverEngine.ECS.Utility;
```

**New:**
```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ForeverEngine.ECS.Utility;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Spells;
```

### 4B. Add spell menu state fields

**After `private CombatIntelligence _neuralBrain;` (line 26), add:**

```csharp
        // Spell casting UI state
        private bool _spellMenuOpen;
        private List<SpellData> _availableSpells = new();
        private int _selectedSpellSlotLevel = 1;
```

### 4C. Modify Start() to use FromCharacterSheet when available

**Old (lines 60-62):**
```csharp
            // Spawn player
            var player = BattleCombatant.FromPlayer(gm.Player);
            Combatants.Add(player);
```

**New:**
```csharp
            // Spawn player — use CharacterSheet if available, fall back to PlayerData
            BattleCombatant player;
            if (gm.Character != null)
                player = BattleCombatant.FromCharacterSheet(gm.Character);
            else
                player = BattleCombatant.FromPlayer(gm.Player);
            Combatants.Add(player);
```

### 4D. Add spell casting input to Update()

**Old (lines 42-44):**
```csharp
                // Attack nearest adjacent enemy with 1, or any key 1-9
                else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F))
                    AttackNearestEnemy();
                else if (Input.GetKeyDown(KeyCode.Space)) PlayerEndTurn();
```

**New:**
```csharp
                // Attack nearest adjacent enemy with 1 or F
                else if (!_spellMenuOpen && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F)))
                    AttackNearestEnemy();
                // Toggle spell menu with Q
                else if (Input.GetKeyDown(KeyCode.Q))
                    ToggleSpellMenu();
                // Spell selection (1-9) when menu is open
                else if (_spellMenuOpen)
                    HandleSpellInput();
                else if (Input.GetKeyDown(KeyCode.Space)) PlayerEndTurn();
```

### 4E. Add spell menu methods

Add after `PlayerEndTurn()` (line 178):

```csharp
        // === Spell Casting ===

        private void ToggleSpellMenu()
        {
            _spellMenuOpen = !_spellMenuOpen;
            if (_spellMenuOpen)
            {
                var playerCombatant = Combatants.FirstOrDefault(c => c.IsPlayer);
                if (playerCombatant?.Sheet == null || playerCombatant.Sheet.PreparedSpells.Count == 0)
                {
                    Log.Add("No spells available.");
                    _spellMenuOpen = false;
                    return;
                }
                _availableSpells = playerCombatant.Sheet.PreparedSpells;
                Log.Add("Spell menu open. Press 1-9 to cast, Q to close.");
            }
        }

        private void HandleSpellInput()
        {
            // Escape or Q closes menu
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q))
            {
                _spellMenuOpen = false;
                Log.Add("Spell menu closed.");
                return;
            }

            // Number keys 1-9 select a spell
            for (int i = 0; i < 9 && i < _availableSpells.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    CastSpell(_availableSpells[i]);
                    return;
                }
            }
        }

        private void CastSpell(SpellData spell)
        {
            var playerCombatant = Combatants.FirstOrDefault(c => c.IsPlayer);
            if (playerCombatant?.Sheet == null || !playerCombatant.HasAction)
            {
                Log.Add("Cannot cast right now.");
                return;
            }

            var sheet = playerCombatant.Sheet;
            int slotLevel = spell.IsCantrip ? 0 : spell.Level;

            // Find a target — closest enemy for damage spells, self for healing
            BattleCombatant target = null;
            if (spell.HealingDiceCount > 0)
            {
                target = playerCombatant; // Self-heal
            }
            else
            {
                // Target closest alive enemy
                target = Combatants
                    .Where(c => c.IsAlive && !c.IsPlayer)
                    .OrderBy(c => System.Math.Abs(c.X - playerCombatant.X) + System.Math.Abs(c.Y - playerCombatant.Y))
                    .FirstOrDefault();
            }

            if (target == null && spell.HealingDiceCount <= 0)
            {
                Log.Add("No valid target.");
                return;
            }

            var castingAbility = RPGBridge.GetCastingAbility(sheet);

            // Build CastContext
            var ctx = new CastContext
            {
                Caster = sheet,
                Targets = target != null ? new object[] { target } : null,
                Spell = spell,
                SlotLevel = slotLevel > 0 ? slotLevel : spell.Level,
                Metamagic = MetamagicType.None,
                IsRitual = false
            };

            var result = SpellCastingPipeline.Cast(
                ctx,
                sheet.EffectiveAbilities,
                sheet.ProficiencyBonus,
                castingAbility,
                sheet.SpellSlots,
                sheet.Concentration,
                null, // No sorcery points for premades
                ref _rngSeed);

            if (!result.Success)
            {
                Log.Add($"Cannot cast {spell.Name}: {result.FailureReason}");
                return;
            }

            _spellMenuOpen = false;
            playerCombatant.HasAction = false;

            // Apply damage to target
            if (result.DamageDealt > 0 && target != null && target != playerCombatant)
            {
                // Apply through DamageResolver with target's resistances
                var dmgCtx = new DamageContext
                {
                    BaseDamage = spell.GetDamage(),
                    Type = spell.DamageType,
                    Critical = false,
                    BonusDamage = 0,
                    Resistances = target.Resistances,
                    Vulnerabilities = target.Vulnerabilities,
                    Immunities = target.Immunities,
                    TargetTempHP = target.TempHP,
                    TargetHP = target.HP
                };
                var dmgResult = DamageResolver.Apply(dmgCtx, ref _rngSeed);

                // Apply temp HP absorption
                if (dmgResult.AbsorbedByTempHP > 0)
                    target.TempHP -= dmgResult.AbsorbedByTempHP;

                target.TakeDamage(dmgResult.HPDamage);

                string resistMsg = "";
                if ((target.Resistances & spell.DamageType) != 0)
                    resistMsg = " (resisted)";
                if ((target.Vulnerabilities & spell.DamageType) != 0)
                    resistMsg = " (vulnerable!)";

                Log.Add($"{playerCombatant.Name} casts {spell.Name} on {target.Name} for {dmgResult.AfterResistance} {spell.DamageType} damage{resistMsg}!");

                // AI events
                var ai = Demo.AI.DemoAIIntegration.Instance;
                ai?.OnPlayerAttacked(true, dmgResult.AfterResistance, target.Name);

                if (!target.IsAlive)
                {
                    Log.Add($"{target.Name} is defeated!");
                    ai?.OnEnemyKilled(target.Name);
                }

                // Q-learning penalty for target
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(-0.3f);
            }

            // Apply healing
            if (result.HealingDone > 0 && target != null)
            {
                target.Heal(result.HealingDone);
                Log.Add($"{playerCombatant.Name} casts {spell.Name} — heals {target.Name} for {result.HealingDone} HP!");
            }

            // Apply conditions
            if (result.ConditionsApplied != Condition.None && target != null && target.Conditions != null)
            {
                target.Conditions.Apply(result.ConditionsApplied, spell.ConditionDuration, spell.Name);
                Log.Add($"{target.Name} is now {result.ConditionsApplied}!");
            }

            // Concentration tracking
            if (result.ConcentrationStarted)
            {
                Log.Add($"{playerCombatant.Name} is concentrating on {spell.Name}.");
            }

            // Slot info
            if (result.SlotExpended > 0)
            {
                int remaining = sheet.SpellSlots.AvailableSlots[result.SlotExpended - 1];
                Log.Add($"(Level {result.SlotExpended} slot expended. {remaining} remaining)");
            }

            CheckBattleEnd();
        }

        // Public accessor for HUD to read spell menu state
        public bool IsSpellMenuOpen => _spellMenuOpen;
        public List<SpellData> AvailableSpells => _availableSpells;
```

### 4F. Replace `ResolveAttack` with full pipeline

**Old `ResolveAttack` method (lines 310-350):**
```csharp
        private void ResolveAttack(BattleCombatant attacker, BattleCombatant target)
        {
            int roll = attacker.RollAttack(ref _rngSeed);
            bool hit = roll == 20 || (roll != 1 && roll + DiceRoller.AbilityModifier(attacker.Strength) >= target.AC);

            var ai = Demo.AI.DemoAIIntegration.Instance;
            if (hit)
            {
                int damage = attacker.RollDamage(ref _rngSeed);
                if (damage < 1) damage = 1;
                target.TakeDamage(damage);
                Log.Add($"{attacker.Name} hits {target.Name} for {damage}! (d20={roll})");

                // AI events
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(true, damage, target.Name);
                if (target.IsPlayer) ai?.OnPlayerDamaged(damage);

                if (!target.IsAlive)
                {
                    Log.Add($"{target.Name} is defeated!");
                    if (!target.IsPlayer) ai?.OnEnemyKilled(target.Name);
                }

                // Q-learning: penalize target enemy for taking damage
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(-0.3f);

                // All enemies rewarded for killing player
                if (!target.IsAlive && target.IsPlayer)
                    foreach (var b in _brains.Values) b.AddReward(1.0f);
            }
            else
            {
                Log.Add($"{attacker.Name} misses {target.Name}. (d20={roll})");
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(false, 0, target.Name);
            }

            // Q-learning: reward/penalize enemy attacker for hit/miss
            if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var atkBrain))
                atkBrain.AddReward(hit ? 0.5f : -0.1f);
        }
```

**New `ResolveAttack`:**
```csharp
        private void ResolveAttack(BattleCombatant attacker, BattleCombatant target)
        {
            // Build AttackContext
            var atkAbilities = new AbilityScores(
                attacker.Strength, attacker.Dexterity, 10, 10, 10, 10);
            int profBonus = 2; // Default proficiency for demo enemies

            WeaponData weapon = null;
            bool isMelee = true;
            bool isRanged = false;
            int magicBonus = 0;

            if (attacker.Sheet != null)
            {
                atkAbilities = attacker.Sheet.EffectiveAbilities;
                profBonus = attacker.Sheet.ProficiencyBonus;
                weapon = attacker.Sheet.MainHand;
                if (weapon != null)
                {
                    isMelee = !weapon.IsRanged;
                    isRanged = weapon.IsRanged;
                    magicBonus = weapon.MagicBonus;
                }
            }

            var atkCtx = new AttackContext
            {
                AttackerAbilities = atkAbilities,
                AttackerProficiency = profBonus,
                Weapon = weapon,
                TargetAC = target.AC,
                AttackerConditions = attacker.Conditions?.ActiveFlags ?? Condition.None,
                TargetConditions = target.Conditions?.ActiveFlags ?? Condition.None,
                IsMelee = isMelee,
                IsRanged = isRanged,
                CritRange = 20,
                MagicBonus = magicBonus
            };

            var atkResult = AttackResolver.Resolve(atkCtx, ref _rngSeed);

            var ai = Demo.AI.DemoAIIntegration.Instance;

            // Format advantage/disadvantage in log
            string advStr = atkResult.State switch
            {
                AdvantageState.Advantage => " with advantage",
                AdvantageState.Disadvantage => " with disadvantage",
                _ => ""
            };

            if (atkResult.Hit)
            {
                // Build DamageContext
                var baseDmg = new DiceExpression(attacker.AtkCount, (DieType)attacker.AtkSides, attacker.AtkBonus);
                if (weapon != null)
                    baseDmg = weapon.GetDamage();

                int abilityDmgBonus = 0;
                if (attacker.Sheet != null)
                {
                    // STR mod for melee, DEX for ranged/finesse
                    if (weapon != null && weapon.IsFinesse)
                    {
                        int strMod = atkAbilities.GetModifier(Ability.STR);
                        int dexMod = atkAbilities.GetModifier(Ability.DEX);
                        abilityDmgBonus = strMod > dexMod ? strMod : dexMod;
                    }
                    else if (isRanged)
                        abilityDmgBonus = atkAbilities.GetModifier(Ability.DEX);
                    else
                        abilityDmgBonus = atkAbilities.GetModifier(Ability.STR);
                }

                var dmgType = attacker.AttackDamageType;
                if (weapon != null) dmgType = weapon.Type;

                var dmgCtx = new DamageContext
                {
                    BaseDamage = baseDmg,
                    Type = dmgType,
                    Critical = atkResult.Critical,
                    BonusDamage = abilityDmgBonus + magicBonus,
                    Resistances = target.Resistances,
                    Vulnerabilities = target.Vulnerabilities,
                    Immunities = target.Immunities,
                    TargetTempHP = target.TempHP,
                    TargetHP = target.HP
                };

                var dmgResult = DamageResolver.Apply(dmgCtx, ref _rngSeed);

                // Apply temp HP absorption
                if (dmgResult.AbsorbedByTempHP > 0)
                    target.TempHP -= dmgResult.AbsorbedByTempHP;

                int hpDamage = dmgResult.HPDamage;
                if (hpDamage < 1 && dmgResult.AfterResistance > 0) hpDamage = 1;

                // Handle damage at 0 HP (death save failures)
                if (target.HP <= 0 && target.IsPlayer && target.DeathSaves != null && target.DeathSaves.IsActive)
                {
                    var dsResult = target.DeathSaves.TakeDamageAtZero(atkResult.Critical);
                    Log.Add($"{target.Name} takes damage at 0 HP! Death save failure{(atkResult.Critical ? " (x2 from crit)" : "")}.");
                    if (dsResult == DeathSaveResult.Dead)
                        Log.Add($"{target.Name} has died!");
                }
                else
                {
                    target.TakeDamage(hpDamage);
                }

                // Format log message
                string critStr = atkResult.Critical ? "CRITICAL HIT! " : "";
                string resistStr = "";
                if ((target.Resistances & dmgType) != 0)
                    resistStr = $" (resisted, halved to {dmgResult.AfterResistance})";
                if ((target.Vulnerabilities & dmgType) != 0)
                    resistStr = $" (vulnerable! doubled to {dmgResult.AfterResistance})";

                Log.Add($"{critStr}{attacker.Name} hits {target.Name}{advStr} for {dmgResult.AfterResistance} {dmgType} damage! (d20={atkResult.NaturalRoll}, total={atkResult.Total} vs AC {target.AC}){resistStr}");

                // AI events (preserved)
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(true, dmgResult.AfterResistance, target.Name);
                if (target.IsPlayer) ai?.OnPlayerDamaged(dmgResult.AfterResistance);

                // Check concentration on damaged caster
                if (target.Concentration != null && target.Concentration.IsConcentrating && target.Sheet != null)
                {
                    bool maintained = target.Concentration.CheckConcentration(
                        dmgResult.AfterResistance,
                        target.Sheet.EffectiveAbilities,
                        target.Sheet.ProficiencyBonus,
                        RPGBridge.IsProficientConSave(target.Sheet),
                        false, // No War Caster for demo
                        ref _rngSeed);
                    if (!maintained)
                        Log.Add($"{target.Name} lost concentration on {target.Concentration.ActiveSpell?.Name ?? "spell"}!");
                }

                // Death & defeat
                if (target.HP <= 0)
                {
                    if (target.IsPlayer && target.DeathSaves != null && !target.DeathSaves.IsActive && !target.DeathSaves.IsDead)
                    {
                        // Enter death save mode
                        target.DeathSaves.Begin();
                        Log.Add($"{target.Name} falls to 0 HP! Death saves begin...");
                    }
                    else if (!target.IsPlayer)
                    {
                        Log.Add($"{target.Name} is defeated!");
                        ai?.OnEnemyKilled(target.Name);
                    }
                }

                // Q-learning: penalize target enemy for taking damage (preserved)
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(-0.3f);

                // All enemies rewarded for downing player (preserved)
                if (target.HP <= 0 && target.IsPlayer)
                    foreach (var b in _brains.Values) b.AddReward(1.0f);
            }
            else
            {
                Log.Add($"{attacker.Name} misses {target.Name}{advStr}. (d20={atkResult.NaturalRoll}, total={atkResult.Total} vs AC {target.AC})");
                if (attacker.IsPlayer) ai?.OnPlayerAttacked(false, 0, target.Name);
            }

            // Q-learning: reward/penalize enemy attacker for hit/miss (preserved)
            if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var atkBrain))
                atkBrain.AddReward(atkResult.Hit ? 0.5f : -0.1f);
        }
```

### 4G. Update `StartTurn` for death save handling

**Old `StartTurn` (lines 106-113):**
```csharp
        public void StartTurn()
        {
            if (BattleOver) return;
            CurrentTurn = Combatants[_turnIndex];
            if (!CurrentTurn.IsAlive) { NextTurn(); return; }
            CurrentTurn.StartTurn();

            if (!CurrentTurn.IsPlayer) ProcessAITurn();
        }
```

**New `StartTurn`:**
```csharp
        public void StartTurn()
        {
            if (BattleOver) return;
            CurrentTurn = Combatants[_turnIndex];

            // Skip truly dead combatants (enemies at 0 HP, or player who failed death saves)
            if (!CurrentTurn.IsAlive) { NextTurn(); return; }

            CurrentTurn.StartTurn();

            // Handle death save mode: player at 0 HP rolls a death save instead of acting
            if (CurrentTurn.IsPlayer && CurrentTurn.HP <= 0 && CurrentTurn.DeathSaves != null && CurrentTurn.DeathSaves.IsActive)
            {
                RollPlayerDeathSave();
                return; // Turn ends after death save
            }

            // Check if conditions prevent acting
            if (CurrentTurn.Conditions != null && !CurrentTurn.Conditions.CanAct)
            {
                Log.Add($"{CurrentTurn.Name} is incapacitated and cannot act!");
                Invoke(nameof(NextTurn), 0.5f);
                return;
            }

            if (!CurrentTurn.IsPlayer) ProcessAITurn();
        }

        private void RollPlayerDeathSave()
        {
            int roll = DiceRoller.Roll(1, 20, 0, ref _rngSeed);
            var result = CurrentTurn.DeathSaves.RollDeathSave(roll);

            switch (result)
            {
                case DeathSaveResult.Revived:
                    CurrentTurn.HP = 1;
                    if (CurrentTurn.Sheet != null) CurrentTurn.Sheet.HP = 1;
                    Log.Add($"Death Save: d20={roll} -- NATURAL 20! {CurrentTurn.Name} revives with 1 HP!");
                    break;
                case DeathSaveResult.Stabilized:
                    Log.Add($"Death Save: d20={roll} -- Success ({CurrentTurn.DeathSaves.Successes}/3). {CurrentTurn.Name} is stabilized!");
                    break;
                case DeathSaveResult.Dead:
                    Log.Add($"Death Save: d20={roll} -- {(roll <= 1 ? "NATURAL 1! Two failures!" : "Failure")} ({CurrentTurn.DeathSaves.Failures}/3). {CurrentTurn.Name} has died!");
                    break;
                case DeathSaveResult.Success:
                    Log.Add($"Death Save: d20={roll} -- Success ({CurrentTurn.DeathSaves.Successes}/3)");
                    break;
                case DeathSaveResult.Failure:
                    Log.Add($"Death Save: d20={roll} -- {(roll <= 1 ? "NATURAL 1! Two failures!" : "Failure")} ({CurrentTurn.DeathSaves.Failures}/3)");
                    break;
            }

            Invoke(nameof(NextTurn), 1.0f);
        }
```

### 4H. Update `CheckBattleEnd` for death saves

**Old `CheckBattleEnd` (lines 355-389):**
```csharp
        private void CheckBattleEnd()
        {
            var player = Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player == null || !player.IsAlive)
            {
                BattleOver = true; PlayerWon = false;
                Log.Add("You have fallen...");
                Demo.AI.DemoAIIntegration.Instance?.OnPlayerDied();
            }
            else if (Combatants.All(c => c.IsPlayer || !c.IsAlive))
            {
                BattleOver = true; PlayerWon = true;
                Log.Add("Victory!");
                Demo.AI.DemoAIIntegration.Instance?.OnCombatVictory(_encounterData.GoldReward, _encounterData.XPReward);
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.LastBattleWon = true;
                    gm.LastBattleGoldEarned = _encounterData.GoldReward;
                    gm.LastBattleXPEarned = _encounterData.XPReward;
                    gm.Player.HP = player.HP; // Persist damage taken
                }
            }

            if (BattleOver)
            {
                float endReward = PlayerWon ? -0.5f : 0.5f;
                foreach (var b in _brains.Values) b.OnEpisodeEnd(endReward);

                // Save Q-table to LTM
                var firstBrain = _brains.Values.FirstOrDefault();
                if (firstBrain != null)
                    Demo.AI.DemoAIIntegration.Instance?.SaveCombatQTable(firstBrain.SaveQTable());
            }
        }
```

**New `CheckBattleEnd`:**
```csharp
        private void CheckBattleEnd()
        {
            var player = Combatants.FirstOrDefault(c => c.IsPlayer);

            // Player is truly dead only if DeathSaveTracker says so (or no tracker and HP <= 0)
            bool playerDead = false;
            if (player == null)
            {
                playerDead = true;
            }
            else if (player.DeathSaves != null)
            {
                playerDead = player.DeathSaves.IsDead;
            }
            else
            {
                playerDead = !player.IsAlive;
            }

            if (playerDead)
            {
                BattleOver = true; PlayerWon = false;
                Log.Add("You have fallen...");
                Demo.AI.DemoAIIntegration.Instance?.OnPlayerDied();
            }
            else if (Combatants.All(c => c.IsPlayer || !c.IsAlive))
            {
                BattleOver = true; PlayerWon = true;
                Log.Add("Victory!");
                Demo.AI.DemoAIIntegration.Instance?.OnCombatVictory(_encounterData.GoldReward, _encounterData.XPReward);
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.LastBattleWon = true;
                    gm.LastBattleGoldEarned = _encounterData.GoldReward;
                    gm.LastBattleXPEarned = _encounterData.XPReward;
                    // Persist damage taken back to CharacterSheet and PlayerData
                    if (gm.Character != null)
                    {
                        gm.Character.HP = player.HP;
                        gm.SyncPlayerFromCharacter();
                    }
                    else
                    {
                        gm.Player.HP = player.HP;
                    }
                }
            }

            if (BattleOver)
            {
                float endReward = PlayerWon ? -0.5f : 0.5f;
                foreach (var b in _brains.Values) b.OnEpisodeEnd(endReward);

                // Save Q-table to LTM (preserved)
                var firstBrain = _brains.Values.FirstOrDefault();
                if (firstBrain != null)
                    Demo.AI.DemoAIIntegration.Instance?.SaveCombatQTable(firstBrain.SaveQTable());
            }
        }
```

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): overhaul BattleManager with AttackResolver/DamageResolver pipeline, death saves, spell casting`

---

## Task 5: EncounterData + EncounterManager CR Upgrade

**Files:**
- MODIFY `Assets/Scripts/Demo/Encounters/EncounterData.cs`
- MODIFY `Assets/Scripts/Demo/Encounters/EncounterManager.cs`

**Why:** Enemies need damage type fields for the new resolver pipeline, and encounter generation should scale by CR/XP budget.

### 5A. Modify EnemyDef in `EncounterData.cs`

**Old EnemyDef (line 6):**
```csharp
    [System.Serializable]
    public class EnemyDef { public string Name; public int HP; public int AC; public int Str; public int Dex; public int Spd; public string AtkDice; public string Behavior; }
```

**New EnemyDef:**
```csharp
    [System.Serializable]
    public class EnemyDef
    {
        public string Name;
        public int HP, AC, Str, Dex, Spd;
        public string AtkDice;
        public string Behavior;
        public int CR; // Challenge Rating (0 = CR 0, 25 = CR 1/4 scaled by 100 for int, or just use 1-30)
        public ForeverEngine.RPG.Enums.DamageType AttackDamageType = ForeverEngine.RPG.Enums.DamageType.Slashing;
        public ForeverEngine.RPG.Enums.DamageType Resistances;
        public ForeverEngine.RPG.Enums.DamageType Vulnerabilities;
        public ForeverEngine.RPG.Enums.DamageType Immunities;
    }
```

### 5B. Replace `GenerateRandom` with CR-based generation

**Old `GenerateRandom` (lines 28-56):**
```csharp
        private static EncounterData GenerateRandom(string id)
        {
            bool night = id.Contains("night");
            var enc = new EncounterData { Id = id, GridWidth = 8, GridHeight = 8 };

            if (id.Contains("Forest"))
            {
                int count = night ? 3 : 2;
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(new EnemyDef { Name = "Wolf", HP = 8, AC = 11, Str = 12, Dex = 14, Spd = 6, AtkDice = "1d6+1", Behavior = "chase" });
                enc.GoldReward = 5 * count; enc.XPReward = 25 * count;
            }
            else if (id.Contains("Road")) // Ruins
            {
                int count = night ? 4 : 2;
                string type = night ? "Mutant" : "Skeleton";
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(new EnemyDef { Name = type, HP = night ? 15 : 10, AC = night ? 13 : 10, Str = night ? 14 : 10, Dex = 10, Spd = 5, AtkDice = night ? "1d8+2" : "1d6", Behavior = night ? "chase" : "guard" });
                enc.GoldReward = 10 * count; enc.XPReward = 50 * count;
            }
            else // Plains
            {
                enc.Enemies.Add(new EnemyDef { Name = "Bandit", HP = 12, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d6+2", Behavior = "chase" });
                if (night) enc.Enemies.Add(new EnemyDef { Name = "Bandit", HP = 12, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d6+2", Behavior = "chase" });
                enc.GoldReward = 15; enc.XPReward = 50;
            }

            return enc;
        }
```

**New `GenerateRandom`:**
```csharp
        private static EncounterData GenerateRandom(string id)
        {
            bool night = id.Contains("night");
            var enc = new EncounterData { Id = id, GridWidth = 8, GridHeight = 8 };

            // Get player level for XP budget calculation
            int playerLevel = GameManager.Instance?.Character?.TotalLevel
                ?? GameManager.Instance?.Player?.Level ?? 1;

            // AI Director pacing multiplier
            float pacingMult = 1.0f;
            var director = ForeverEngine.AI.Director.AIDirector.Instance;
            if (director != null)
                pacingMult = UnityEngine.Mathf.Clamp(1.0f + (director.Pacing.CurrentIntensity - 0.5f), 0.5f, 1.5f);

            // XP budget: Medium difficulty baseline (50 x level), night = Hard (75 x level)
            int xpBudget = (int)((night ? 75 : 50) * playerLevel * pacingMult);

            if (id.Contains("Forest"))
            {
                // Wolves: CR 1/4 (25 XP each), ~10 HP, AC 11
                int count = System.Math.Max(1, xpBudget / 25);
                count = System.Math.Min(count, 5); // Cap at 5
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(MakeCREnemyDef("Wolf", 25, "chase", "Forest",
                        ForeverEngine.RPG.Enums.DamageType.Piercing));
                enc.GoldReward = 5 * count; enc.XPReward = 25 * count;
            }
            else if (id.Contains("Road")) // Ruins
            {
                if (night)
                {
                    // Mutants: CR 1 (100 XP), ~25 HP, AC 13
                    int count = System.Math.Max(1, xpBudget / 100);
                    count = System.Math.Min(count, 4);
                    for (int i = 0; i < count; i++)
                        enc.Enemies.Add(MakeCREnemyDef("Mutant", 100, "chase", "Ruins",
                            ForeverEngine.RPG.Enums.DamageType.Bludgeoning));
                    enc.GoldReward = 15 * count; enc.XPReward = 100 * count;
                }
                else
                {
                    // Skeletons: CR 1/4 (25 XP), vulnerable to bludgeoning, resistant to piercing
                    int count = System.Math.Max(1, xpBudget / 25);
                    count = System.Math.Min(count, 4);
                    for (int i = 0; i < count; i++)
                    {
                        var skel = MakeCREnemyDef("Skeleton", 25, "guard", "Ruins",
                            ForeverEngine.RPG.Enums.DamageType.Slashing);
                        skel.Vulnerabilities = ForeverEngine.RPG.Enums.DamageType.Bludgeoning;
                        skel.Resistances = ForeverEngine.RPG.Enums.DamageType.Piercing;
                        enc.Enemies.Add(skel);
                    }
                    enc.GoldReward = 10 * count; enc.XPReward = 25 * count;
                }
            }
            else // Plains
            {
                // Bandits: CR 1/2 (50 XP), ~15 HP, AC 12
                int count = System.Math.Max(1, xpBudget / 50);
                count = System.Math.Min(count, 4);
                for (int i = 0; i < count; i++)
                    enc.Enemies.Add(MakeCREnemyDef("Bandit", 50, "chase", "Plains",
                        ForeverEngine.RPG.Enums.DamageType.Slashing));
                enc.GoldReward = 15 * count; enc.XPReward = 50 * count;
            }

            return enc;
        }

        /// <summary>
        /// Create an EnemyDef from a CR-based stat block.
        /// CR lookup table:
        ///   XP 25  (CR 1/4): ~10 HP, AC 11, STR 12, DEX 14, Spd 6, 1d6+1
        ///   XP 50  (CR 1/2): ~15 HP, AC 12, STR 12, DEX 12, Spd 6, 1d8+1
        ///   XP 100 (CR 1):   ~25 HP, AC 13, STR 14, DEX 10, Spd 5, 1d10+2
        ///   XP 200 (CR 2):   ~40 HP, AC 14, STR 15, DEX 10, Spd 5, 2d6+3
        ///   XP 450 (CR 3):   ~55 HP, AC 15, STR 16, DEX 10, Spd 5, 2d8+3
        ///   XP 900 (CR 5):   ~80 HP, AC 16, STR 18, DEX 12, Spd 5, 2d10+4
        /// </summary>
        private static EnemyDef MakeCREnemyDef(string name, int xp, string behavior, string biome,
            ForeverEngine.RPG.Enums.DamageType atkDmgType)
        {
            // Stat block by XP tier
            return xp switch
            {
                <= 25  => new EnemyDef { Name = name, HP = 10, AC = 11, Str = 12, Dex = 14, Spd = 6, AtkDice = "1d6+1",  Behavior = behavior, CR = 0, AttackDamageType = atkDmgType },
                <= 50  => new EnemyDef { Name = name, HP = 15, AC = 12, Str = 12, Dex = 12, Spd = 6, AtkDice = "1d8+1",  Behavior = behavior, CR = 1, AttackDamageType = atkDmgType },
                <= 100 => new EnemyDef { Name = name, HP = 25, AC = 13, Str = 14, Dex = 10, Spd = 5, AtkDice = "1d10+2", Behavior = behavior, CR = 1, AttackDamageType = atkDmgType },
                <= 200 => new EnemyDef { Name = name, HP = 40, AC = 14, Str = 15, Dex = 10, Spd = 5, AtkDice = "2d6+3",  Behavior = behavior, CR = 2, AttackDamageType = atkDmgType },
                <= 450 => new EnemyDef { Name = name, HP = 55, AC = 15, Str = 16, Dex = 10, Spd = 5, AtkDice = "2d8+3",  Behavior = behavior, CR = 3, AttackDamageType = atkDmgType },
                _      => new EnemyDef { Name = name, HP = 80, AC = 16, Str = 18, Dex = 12, Spd = 5, AtkDice = "2d10+4", Behavior = behavior, CR = 5, AttackDamageType = atkDmgType },
            };
        }
```

### 5C. Update boss templates to include new fields

In the `Init()` method, add damage type fields to existing boss EnemyDefs. For each `new EnemyDef { ... }` in the boss templates, append:

For `"Hollow Guardian"`, add: `AttackDamageType = ForeverEngine.RPG.Enums.DamageType.Bludgeoning, CR = 3`
For `"Skeleton"` enemies in dungeon_boss: `AttackDamageType = ForeverEngine.RPG.Enums.DamageType.Slashing, CR = 0, Vulnerabilities = ForeverEngine.RPG.Enums.DamageType.Bludgeoning, Resistances = ForeverEngine.RPG.Enums.DamageType.Piercing`
For `"The Rot King"`: `AttackDamageType = ForeverEngine.RPG.Enums.DamageType.Necrotic, CR = 5, Resistances = ForeverEngine.RPG.Enums.DamageType.Necrotic`
For `"Rot Knight"`: `AttackDamageType = ForeverEngine.RPG.Enums.DamageType.Slashing, CR = 2`
For `"Plague Rat"`: `AttackDamageType = ForeverEngine.RPG.Enums.DamageType.Piercing, CR = 0`

### 5D. EncounterManager: add CR scaling to `ScaleEncounter`

The existing `ScaleEncounter` in EncounterManager already applies DDA scaling. No further changes needed -- the CR data is embedded in the EnemyDef and flows through to BattleCombatant.FromEnemy() which now reads the new fields.

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): CR-based encounter generation with damage type resistances`

---

## Task 6: OverworldHUD Updates

**Files:**
- MODIFY `Assets/Scripts/Demo/UI/OverworldHUD.cs`

**Why:** Show the player's class, species, level, and spell slot information from CharacterSheet.

### Modify the stats panel in `OnGUI`

**Old (lines 14-21):**
```csharp
            // Top-left: Stats
            GUI.Box(new Rect(10, 10, 200, 130), "");
            GUI.Label(new Rect(20, 15, 180, 20), $"<b>The Wanderer</b> (Lv.{p.Level})");
            DrawBar(new Rect(20, 40, 170, 16), p.HPPercent, Color.red, $"HP: {p.HP}/{p.MaxHP}");
            DrawBar(new Rect(20, 60, 170, 16), p.HungerPercent, new Color(0.8f, 0.5f, 0.2f), $"Hunger: {p.Hunger:F0}/{p.MaxHunger}");
            DrawBar(new Rect(20, 80, 170, 16), p.ThirstPercent, Color.cyan, $"Thirst: {p.Thirst:F0}/{p.MaxThirst}");
            GUI.Label(new Rect(20, 100, 180, 20), $"Gold: {p.Gold}  |  AC: {p.AC}");
            GUI.Label(new Rect(20, 118, 180, 20), $"Day {p.DayCount}  |  {(ow != null && ow.IsNight ? "Night" : "Day")}");
```

**New:**
```csharp
            // Top-left: Stats
            var sheet = gm.Character;
            string charTitle;
            if (sheet != null)
            {
                string species = sheet.Species != null ? sheet.Species.Name : "";
                string cls = RPGBridge.GetClassName(sheet);
                charTitle = $"<b>{species} {cls}</b> Lv{sheet.TotalLevel}";
            }
            else
            {
                charTitle = $"<b>The Wanderer</b> (Lv.{p.Level})";
            }

            // Calculate spell slot display
            string spellSlotStr = "";
            if (sheet != null)
            {
                var slots = sheet.SpellSlots;
                for (int i = 0; i < 9; i++)
                {
                    if (slots.MaxSlots[i] > 0)
                    {
                        if (spellSlotStr.Length > 0) spellSlotStr += " | ";
                        spellSlotStr += $"L{i+1}: {slots.AvailableSlots[i]}/{slots.MaxSlots[i]}";
                    }
                }
            }

            int boxHeight = string.IsNullOrEmpty(spellSlotStr) ? 130 : 150;
            GUI.Box(new Rect(10, 10, 240, boxHeight), "");
            GUI.Label(new Rect(20, 15, 220, 20), charTitle);
            DrawBar(new Rect(20, 40, 210, 16), p.HPPercent, Color.red, $"HP: {p.HP}/{p.MaxHP}");
            DrawBar(new Rect(20, 60, 210, 16), p.HungerPercent, new Color(0.8f, 0.5f, 0.2f), $"Hunger: {p.Hunger:F0}/{p.MaxHunger}");
            DrawBar(new Rect(20, 80, 210, 16), p.ThirstPercent, Color.cyan, $"Thirst: {p.Thirst:F0}/{p.MaxThirst}");
            GUI.Label(new Rect(20, 100, 220, 20), $"Gold: {p.Gold}  |  AC: {p.AC}");
            GUI.Label(new Rect(20, 118, 220, 20), $"Day {p.DayCount}  |  {(ow != null && ow.IsNight ? "Night" : "Day")}");
            if (!string.IsNullOrEmpty(spellSlotStr))
            {
                var slotStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.6f, 0.7f, 1f) } };
                GUI.Label(new Rect(20, 136, 220, 18), $"Slots: {spellSlotStr}", slotStyle);
            }
```

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): show class/species/spell slots on OverworldHUD`

---

## Task 7: BattleHUD Updates

**Files:**
- MODIFY `Assets/Scripts/Demo/UI/BattleHUD.cs`

**Why:** Show conditions, spell list, death save pips, and enhanced combat controls.

### Add using directives

**Old (lines 1-2):**
```csharp
using UnityEngine;
using ForeverEngine.Demo.Battle;
```

**New:**
```csharp
using UnityEngine;
using System.Linq;
using ForeverEngine.Demo.Battle;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Enums;
```

### Modify `OnGUI` method

**Old player controls section (lines 42-46):**
```csharp
            // Player controls
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer && !bm.BattleOver)
            {
                GUI.Label(new Rect(Screen.width/2 - 200, Screen.height - 30, 400, 20), "WASD: Move | Click enemy: Attack | Space: End Turn");
            }
```

**New:**
```csharp
            // Player controls
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer && !bm.BattleOver)
            {
                var pc = bm.CurrentTurn;

                // Death save display (overrides normal controls)
                if (pc.HP <= 0 && pc.DeathSaves != null && pc.DeathSaves.IsActive)
                {
                    DrawDeathSavePips(pc.DeathSaves);
                }
                else
                {
                    // Controls hint
                    bool hasSpells = pc.Sheet != null && pc.Sheet.PreparedSpells.Count > 0;
                    string controls = hasSpells
                        ? "WASD: Move | F: Attack | Q: Spells | Space: End Turn"
                        : "WASD: Move | F: Attack | Space: End Turn";
                    GUI.Label(new Rect(Screen.width/2 - 220, Screen.height - 30, 440, 20), controls);

                    // Spell menu overlay
                    if (bm.IsSpellMenuOpen && bm.AvailableSpells != null)
                    {
                        DrawSpellMenu(bm, pc);
                    }
                }
            }

            // Active conditions on player
            DrawConditions(bm);
```

### Add helper methods at the end of the class

Add before the closing `}` of the class:

```csharp
        private void DrawDeathSavePips(DeathSaveTracker ds)
        {
            float cx = Screen.width / 2;
            float cy = Screen.height / 2 + 60;
            GUI.Box(new Rect(cx - 120, cy, 240, 60), "");

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(cx - 110, cy + 5, 220, 20), "DEATH SAVES", headerStyle);

            // Success pips
            string successes = "";
            for (int i = 0; i < 3; i++)
                successes += i < ds.Successes ? "\u25CF " : "\u25CB "; // filled / empty circles
            var successStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.green }, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(cx - 110, cy + 25, 110, 20), $"Pass: {successes}", successStyle);

            // Failure pips
            string failures = "";
            for (int i = 0; i < 3; i++)
                failures += i < ds.Failures ? "\u25CF " : "\u25CB ";
            var failStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.red }, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(cx, cy + 25, 110, 20), $"Fail: {failures}", failStyle);
        }

        private void DrawSpellMenu(BattleManager bm, BattleCombatant pc)
        {
            var spells = bm.AvailableSpells;
            float menuW = 320, menuH = 30 + spells.Count * 22;
            float mx = Screen.width / 2 - menuW / 2;
            float my = Screen.height - 60 - menuH;
            GUI.Box(new Rect(mx, my, menuW, menuH), "");

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(mx + 10, my + 5, 300, 20), "Prepared Spells (press 1-9 to cast, Q to close)", headerStyle);

            var slots = pc.Sheet?.SpellSlots;
            for (int i = 0; i < spells.Count && i < 9; i++)
            {
                var spell = spells[i];
                string lvlStr = spell.IsCantrip ? "Cantrip" : $"L{spell.Level}";
                string dmgStr = spell.DamageDiceCount > 0 ? $" {spell.GetDamage()} {spell.DamageType}" : "";
                string healStr = spell.HealingDiceCount > 0 ? $" Heal {spell.GetHealing()}" : "";
                string concStr = spell.Concentration ? " [C]" : "";

                bool canCast = spell.IsCantrip || (slots != null && slots.CanCast(spell, spell.Level));
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = canCast ? Color.white : Color.gray }
                };

                GUI.Label(new Rect(mx + 10, my + 25 + i * 22, 300, 20),
                    $"{i + 1}. {spell.Name} ({lvlStr}){dmgStr}{healStr}{concStr}", style);
            }

            // Spell slot summary
            if (slots != null)
            {
                string slotInfo = "";
                for (int i = 0; i < 9; i++)
                {
                    if (slots.MaxSlots[i] > 0)
                    {
                        if (slotInfo.Length > 0) slotInfo += " | ";
                        slotInfo += $"L{i+1}: {slots.AvailableSlots[i]}/{slots.MaxSlots[i]}";
                    }
                }
                if (slotInfo.Length > 0)
                {
                    var slotStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.5f, 0.7f, 1f) } };
                    GUI.Label(new Rect(mx + 10, my + menuH - 18, 300, 16), slotInfo, slotStyle);
                }
            }
        }

        private void DrawConditions(BattleManager bm)
        {
            // Show active conditions on player and current target
            var player = bm.Combatants.FirstOrDefault(c => c.IsPlayer);
            if (player != null && player.Conditions != null && player.Conditions.ActiveFlags != Condition.None)
            {
                var condStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.yellow } };
                GUI.Label(new Rect(220, 15, 200, 18), $"Conditions: {player.Conditions.ActiveFlags}", condStyle);
            }

            // Show conditions on currently targeted enemy (current turn if enemy)
            if (bm.CurrentTurn != null && !bm.CurrentTurn.IsPlayer && bm.CurrentTurn.IsAlive)
            {
                var enemy = bm.CurrentTurn;
                if (enemy.Conditions != null && enemy.Conditions.ActiveFlags != Condition.None)
                {
                    var condStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(1f, 0.6f, 0.3f) } };
                    GUI.Label(new Rect(220, 35, 200, 18), $"{enemy.Name}: {enemy.Conditions.ActiveFlags}", condStyle);
                }
            }

            // Show player class/level in combatant list area
            if (player?.Sheet != null)
            {
                var clsStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.7f, 0.9f, 1f) } };
                string cls = RPGBridge.GetClassName(player.Sheet);
                GUI.Label(new Rect(220, 55, 200, 18), $"{player.Sheet.Species?.Name} {cls} Lv{player.Sheet.TotalLevel}", clsStyle);
            }
        }
```

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `feat(demo): add spell menu, death save pips, and condition display to BattleHUD`

---

## Task 8: Scene Rebuild + Verification

**Files:**
- MODIFY `Assets/Editor/DemoSceneBuilder.cs` (minor -- no structural changes needed)

**Why:** Rebuild all 3 demo scenes to pick up the new component layouts, then run playtest capture.

### 8A. DemoSceneBuilder changes

No structural changes needed to `DemoSceneBuilder.cs` -- the scene builder creates GameObjects with `AddComponent<>` calls, and the existing components (GameManager, DemoMainMenu, BattleManager, BattleHUD, OverworldHUD) are already wired. The RPG types are created at runtime by RPGBridge, not as scene components.

The only thing to verify: `DemoSceneBuilder` does not need to add RPGBridge (it is static) or any new MonoBehaviours.

### 8B. Rebuild scenes

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -executeMethod ForeverEngine.Editor.DemoSceneBuilder.BuildAll -quit 2>/dev/null; echo "Exit: $?"
```

### 8C. Run playtest capture

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration-playtest.log -executeMethod ForeverEngine.Editor.DemoSceneBuilder.PlaytestCapture -quit 2>/dev/null; echo "Exit: $?"
```

### 8D. Final compilation check

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Check the log for errors:
```bash
grep -i "error\|fail" "C:/Dev/Forever engin/tests/rpg-integration.log" | head -20
```

### Compile & Commit

```bash
cd "C:/Dev/Forever engin" && "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/rpg-integration.log -quit 2>/dev/null; echo "Exit: $?"
```

Commit message: `chore(demo): rebuild scenes and verify RPG integration compiles`

---

## Verification Checklist

After all 8 tasks, verify:

- [ ] **Compilation**: Unity batch mode exits with code 0
- [ ] **Character creation**: Each of the 4 premades creates valid CharacterSheet with correct stats (check via log output)
- [ ] **Combat pipeline**: AttackResolver produces hits/misses/crits with correct advantage/disadvantage from conditions
- [ ] **Damage pipeline**: DamageResolver applies resistances/vulnerabilities correctly (Skeleton takes half piercing, double bludgeoning)
- [ ] **Spell casting**: Wizard can cast Flame Dart (cantrip, no slot), Magic Missile (L1, expends slot)
- [ ] **Healing**: Cleric can cast Mending Touch, HP increases
- [ ] **Death saves**: Player at 0 HP rolls saves; nat 20 revives with 1 HP; 3 failures = game over
- [ ] **Conditions**: Spell that applies Paralyzed gives melee attacks advantage + auto-crit
- [ ] **Concentration**: Casting a concentration spell while already concentrating ends the previous one
- [ ] **CR encounters**: `GenerateRandom` produces CR-appropriate enemies based on player level and XP budget
- [ ] **AI integration**: All existing hooks fire (OnCombatStarted, OnPlayerAttacked, OnEnemyKilled, OnPlayerDied, OnCombatVictory)
- [ ] **Q-learning**: CombatBrain still functions -- enemy attacks resolve through AttackResolver, rewards still apply
- [ ] **HUD**: OverworldHUD shows class/species/level/spell slots; BattleHUD shows conditions/spells/death saves
- [ ] **PlayerData sync**: `SyncPlayerFromCharacter()` correctly maps HP, AC, abilities, attack dice, level

## Files Summary

| Action | File | Description |
|--------|------|-------------|
| CREATE | `Assets/Scripts/Demo/RPGBridge.cs` | Premade character factory, sync helper, casting ability lookup |
| MODIFY | `Assets/Scripts/Demo/GameManager.cs` | Add `Character` field, `StartGameWithSheet()`, `SyncPlayerFromCharacter()` |
| MODIFY | `Assets/Scripts/Demo/UI/DemoMainMenu.cs` | 4 premade character selection buttons |
| MODIFY | `Assets/Scripts/Demo/Battle/BattleCombatant.cs` | Add Sheet, Conditions, DeathSaves, Concentration, resistances, `FromCharacterSheet()` |
| MODIFY | `Assets/Scripts/Demo/Battle/BattleManager.cs` | Full AttackResolver+DamageResolver pipeline, spell casting, death saves |
| MODIFY | `Assets/Scripts/Demo/Encounters/EncounterData.cs` | EnemyDef CR/resistance fields, CR-based `GenerateRandom()` |
| MODIFY | `Assets/Scripts/Demo/Encounters/EncounterManager.cs` | No changes needed (CR data flows through EnemyDef) |
| MODIFY | `Assets/Scripts/Demo/UI/OverworldHUD.cs` | Show class/species/level/spell slots |
| MODIFY | `Assets/Scripts/Demo/UI/BattleHUD.cs` | Spell menu, death save pips, condition display |
| MODIFY | `Assets/Editor/DemoSceneBuilder.cs` | No changes needed (RPGBridge is static, no new components) |
