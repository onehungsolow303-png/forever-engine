using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ForeverEngine.Bridges;
using ForeverEngine.ECS.Utility;
using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;
using ForeverEngine.RPG.Spells;

namespace ForeverEngine.Demo.Battle
{
    public class BattleManager : UnityEngine.MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public BattleGrid Grid { get; private set; }
        public List<BattleCombatant> Combatants { get; private set; } = new();
        public BattleCombatant CurrentTurn { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public bool BattleOver { get; private set; }
        public bool PlayerWon { get; private set; }
        public List<string> Log { get; private set; } = new();

        private int _turnIndex;
        private uint _rngSeed;
        private Encounters.EncounterData _encounterData;
        private BattleRenderer _renderer;
        private BattleRenderer3D _renderer3D;
        private Dictionary<BattleCombatant, CombatBrain> _brains = new();
        private CombatIntelligence _neuralBrain;
        private GameConfig _gameConfig;

        // Spell casting UI state
        private bool _spellMenuOpen;
        private List<SpellData> _availableSpells = new();

        private void Awake() => Instance = this;

        private void Update()
        {
            if (_renderer3D != null)
                _renderer3D.UpdateVisuals(Combatants, CurrentTurn);
            else
            {
                if (_renderer == null) _renderer = FindAnyObjectByType<BattleRenderer>();
                if (_renderer != null) _renderer.UpdateVisuals(Combatants, CurrentTurn);
            }

            // Player input during their turn
            if (CurrentTurn != null && CurrentTurn.IsPlayer && !BattleOver)
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                    CameraRelativeMove(0, 1);
                else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                    CameraRelativeMove(0, -1);
                else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                    CameraRelativeMove(-1, 0);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    CameraRelativeMove(1, 0);
                // Attack nearest adjacent enemy with 1 or F
                else if (!_spellMenuOpen && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F)))
                    AttackNearestEnemy();
                // Toggle spell menu with Q
                else if (Input.GetKeyDown(KeyCode.Q))
                    ToggleSpellMenu();
                // Spell selection (1-9) when menu is open
                else if (_spellMenuOpen)
                    HandleSpellInput();
                // Quick heal: H consumes one health potion from inventory.
                // Costs an action so it can't be combined with an attack
                // on the same turn — using a potion in 5e takes an action.
                else if (Input.GetKeyDown(KeyCode.H))
                    UseHealthPotion();
                else if (Input.GetKeyDown(KeyCode.Space)) PlayerEndTurn();
            }
        }

        private void Start()
        {
            _gameConfig = Resources.Load<GameConfig>("GameConfig");
            var gm = GameManager.Instance;
            if (gm == null) return;

            string encId = gm.PendingEncounterId ?? gm.PendingLocationId ?? "default";
            _encounterData = Encounters.EncounterData.Get(encId);
            _rngSeed = (uint)(gm.CurrentSeed + encId.GetHashCode());

            // Build grid
            Grid = new BattleGrid(_encounterData.GridWidth, _encounterData.GridHeight, (int)_rngSeed);

            // Spawn player — use CharacterSheet if available, fall back to PlayerData
            BattleCombatant player;
            if (gm.Character != null)
                player = BattleCombatant.FromCharacterSheet(gm.Character);
            else
                player = BattleCombatant.FromPlayer(gm.Player);
            Combatants.Add(player);

            // Spawn enemies
            var rng = new System.Random((int)_rngSeed);
            foreach (var enemyDef in _encounterData.Enemies)
            {
                int ex, ey;
                do { ex = rng.Next(2, Grid.Width - 2); ey = rng.Next(2, Grid.Height - 2); }
                while (!Grid.IsWalkable(ex, ey) || Combatants.Any(c => c.X == ex && c.Y == ey));
                Combatants.Add(BattleCombatant.FromEnemy(enemyDef, ex, ey));
            }

            // Roll initiative, then promote the player to the front of the
            // round-1 turn order. The player's actual InitiativeRoll is left
            // intact for any consumer that displays it; only the turn ordering
            // is biased so the player always gets at least one action before
            // any enemy. Rounds 2+ go in raw initiative order via NextTurn's
            // index wrap. Addresses the "died with no turn" complaint without
            // changing combat balance.
            foreach (var c in Combatants) c.RollInitiative(ref _rngSeed);
            Combatants = Combatants.OrderByDescending(c => c.InitiativeRoll).ToList();
            int playerIdx = Combatants.FindIndex(c => c.IsPlayer);
            if (playerIdx > 0)
            {
                var p = Combatants[playerIdx];
                Combatants.RemoveAt(playerIdx);
                Combatants.Insert(0, p);
            }

            // Notify Director Hub that combat has started. Q-table persistence
            // is still pending the long-term memory wiring (spec §14 follow-up #3).
            Demo.AI.DirectorEvents.Send($"combat started: {encId}");
            float[] savedTable = null;
            foreach (var c in Combatants)
            {
                if (!c.IsPlayer && c.IsAlive)
                    _brains[c] = new CombatBrain(savedTable, seed: (int)_rngSeed + c.X * 100 + c.Y);
            }

            // Create neural intelligence only if InferenceEngine has a loaded model
            var inferEngine = ForeverEngine.AI.Inference.InferenceEngine.Instance;
            if (inferEngine != null && inferEngine.IsAvailable)
            {
                var go = new GameObject("CombatIntelligence");
                go.transform.SetParent(transform);
                _neuralBrain = go.AddComponent<CombatIntelligence>();
            }

            // Create visual renderer — 3D if a template is available, else 2D fallback
            var template = FindBattleTemplate(_encounterData);
            if (template != null)
            {
                var rendererGO = new GameObject("BattleRenderer3D");
                var renderer3D = rendererGO.AddComponent<BattleRenderer3D>();
                renderer3D.Initialize(template, Grid, Combatants, Camera.main);
                _renderer3D = renderer3D;

                var inputGO = new GameObject("BattleInput");
                var input = inputGO.AddComponent<BattleInputController>();
                input.Initialize(renderer3D, this, Camera.main);
            }
            else
            {
                var rendererGO = new GameObject("BattleRenderer");
                var renderer = rendererGO.AddComponent<BattleRenderer>();
                renderer.Initialize(Grid, Combatants, Camera.main);
            }

            // Ask Asset Manager for a creature_token sprite for each unique
            // enemy kind. The result is logged for now; visual swap (loading
            // the returned PNG into a SpriteRenderer on the existing token
            // GameObject) is a follow-up. The wire itself proves the engine
            // can consume Asset Manager assets at gameplay-driven moments,
            // closing the dead-code gap from the audit.
            StartCoroutine(RequestEnemySprites());

            StartTurn();
            Log.Add($"Battle begins! {_encounterData.Enemies.Count} enemies.");
        }

        // Cache so we don't re-request the same enemy across battles within
        // a single game session. Keyed on enemy Name (e.g. "Wolf", "Bandit").
        private static readonly Dictionary<string, string> _enemySpriteCache = new();

        private System.Collections.IEnumerator RequestEnemySprites()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Assets == null) yield break;

            // Extract a biome hint from the encounter id (e.g. "random_Forest_day"
            // → "forest"). Falls back to "default" for static encounters.
            string biome = "default";
            string encId = _encounterData.Id ?? "";
            if (encId.Contains("Forest")) biome = "forest";
            else if (encId.Contains("Road")) biome = "ruins";
            else if (encId.Contains("Plains")) biome = "plains";
            else if (encId.Contains("dungeon")) biome = "dungeon";
            else if (encId.Contains("castle")) biome = "castle";

            var seen = new HashSet<string>();
            foreach (var enemy in _encounterData.Enemies)
            {
                if (enemy == null || string.IsNullOrEmpty(enemy.Name)) continue;
                if (!seen.Add(enemy.Name)) continue;
                string cacheKey = $"{enemy.Name}|{biome}";
                if (_enemySpriteCache.TryGetValue(cacheKey, out var cached))
                {
                    Debug.Log($"[BattleManager] enemy sprite cache hit: {enemy.Name} -> {cached}");
                    continue;
                }

                var req = new AssetClient.AssetSelectionRequestDto
                {
                    Kind = "creature_token",
                    Biome = biome,
                    Tags = new[] { enemy.Name.ToLowerInvariant() },
                    AllowAiGeneration = false,
                };

                // Capture name into the closure so the callback can match
                // combatants by it (the loop variable changes per iteration).
                string enemyNameForCallback = enemy.Name;
                yield return gm.Assets.Select(
                    req,
                    resp =>
                    {
                        if (resp != null && resp.Found && !string.IsNullOrEmpty(resp.AssetId))
                        {
                            _enemySpriteCache[cacheKey] = resp.AssetId;
                            Debug.Log($"[BattleManager] asset hit for {enemyNameForCallback} ({biome}): {resp.AssetId} at {resp.Path}");
                            ApplySpriteToCombatants(enemyNameForCallback, resp.Path);
                        }
                        else
                        {
                            // Miss is expected today — the asset library is
                            // pre-pivot empty for these kinds. The /select call
                            // succeeded; just no asset matched. Log at info so
                            // we can verify the wire works.
                            Debug.Log($"[BattleManager] no asset for {enemyNameForCallback} ({biome}) — using procedural token");
                        }
                    },
                    err =>
                    {
                        // Asset Manager unavailable or errored — non-fatal.
                        Debug.LogWarning($"[BattleManager] asset request failed for {enemyNameForCallback}: {err}");
                    });
            }
        }

        /// <summary>
        /// Push a sprite path into the BattleRenderer for every combatant
        /// matching `enemyName`. Called from RequestEnemySprites' success
        /// callback. Renderer handles the actual file I/O + texture load.
        /// </summary>
        private void ApplySpriteToCombatants(string enemyName, string pngPath)
        {
            if (string.IsNullOrEmpty(pngPath)) return;
            // _renderer is populated by Update() the first frame after Start;
            // RequestEnemySprites runs as a coroutine spawned from Start so
            // _renderer may still be null on the first iteration. Find it
            // ourselves to be safe.
            var rendererInstance = _renderer != null ? _renderer : FindAnyObjectByType<BattleRenderer>();
            if (rendererInstance == null) return;
            foreach (var c in Combatants)
            {
                if (c == null || c.IsPlayer) continue;
                if (c.Name == enemyName)
                    rendererInstance.SwapTokenSprite(c, pngPath);
            }
        }

        private BattleSceneTemplate FindBattleTemplate(Encounters.EncounterData encounter)
        {
            string biome = encounter.Biome ?? "dungeon";
            var templates = Resources.LoadAll<BattleSceneTemplate>($"BattleTemplates/{biome}");
            if (templates == null || templates.Length == 0)
                templates = Resources.LoadAll<BattleSceneTemplate>("BattleTemplates");
            if (templates == null || templates.Length == 0) return null;
            var rng = new System.Random((int)_rngSeed);
            return templates[rng.Next(templates.Length)];
        }

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

        public void NextTurn()
        {
            _turnIndex = (_turnIndex + 1) % Combatants.Count;
            if (_turnIndex == 0) RoundNumber++;
            CheckBattleEnd();
            if (!BattleOver) StartTurn();
        }

        /// <summary>
        /// Convert camera-relative input to grid movement.
        /// inputX: -1=left, 1=right. inputZ: -1=back, 1=forward.
        /// </summary>
        private void CameraRelativeMove(float inputX, float inputZ)
        {
            var cam = Camera.main;
            if (cam == null) { PlayerMove((int)inputX, (int)inputZ); return; }

            Vector3 camFwd = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camFwd.y = 0f; camFwd.Normalize();
            camRight.y = 0f; camRight.Normalize();

            Vector3 desired = (camFwd * inputZ + camRight * inputX).normalized;

            // Snap to nearest cardinal grid direction
            // Grid directions: (1,0), (-1,0), (0,1), (0,-1)
            float dotRight = Vector3.Dot(desired, Vector3.right);   // +X
            float dotForward = Vector3.Dot(desired, Vector3.forward); // +Z

            int dx, dy;
            if (Mathf.Abs(dotRight) > Mathf.Abs(dotForward))
                { dx = dotRight > 0 ? 1 : -1; dy = 0; }
            else
                { dx = 0; dy = dotForward > 0 ? 1 : -1; }

            PlayerMove(dx, dy);
        }

        public void PlayerMove(int dx, int dy)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer) return;

            int nx = CurrentTurn.X + dx, ny = CurrentTurn.Y + dy;
            if (!Grid.IsWalkable(nx, ny)) return;

            // Bump attack: moving into an enemy attacks them instead
            var target = Combatants.FirstOrDefault(c => c.IsAlive && !c.IsPlayer && c.X == nx && c.Y == ny);
            if (target != null)
            {
                if (CurrentTurn.HasAction)
                {
                    ResolveAttack(CurrentTurn, target);
                    CurrentTurn.HasAction = false;
                    CheckBattleEnd();
                }
                return;
            }

            if (CurrentTurn.MovementRemaining <= 0) return;
            CurrentTurn.X = nx; CurrentTurn.Y = ny;
            CurrentTurn.MovementRemaining--;
        }

        /// <summary>Move player to a specific tile (called by mouse input).</summary>
        public void PlayerMoveTo(int x, int y)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || BattleOver) return;
            int dist = Mathf.Abs(CurrentTurn.X - x) + Mathf.Abs(CurrentTurn.Y - y);
            if (dist > CurrentTurn.MovementRemaining) return;
            if (!Grid.IsWalkable(x, y)) return;
            CurrentTurn.X = x;
            CurrentTurn.Y = y;
            CurrentTurn.MovementRemaining -= dist;
        }

        /// <summary>Attack a specific enemy (called by mouse input).</summary>
        public void PlayerAttack(BattleCombatant target)
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || BattleOver) return;
            if (!CurrentTurn.HasAction) return;
            int dist = Mathf.Abs(CurrentTurn.X - target.X) + Mathf.Abs(CurrentTurn.Y - target.Y);
            if (dist > 1) return;
            ResolveAttack(CurrentTurn, target);
            CurrentTurn.HasAction = false;
        }

        public void AttackNearestEnemy()
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || !CurrentTurn.HasAction) return;
            // Find closest adjacent alive enemy
            var target = Combatants
                .Where(c => c.IsAlive && !c.IsPlayer && IsAdjacent(CurrentTurn, c))
                .OrderBy(c => System.Math.Abs(c.X - CurrentTurn.X) + System.Math.Abs(c.Y - CurrentTurn.Y))
                .FirstOrDefault();
            if (target != null)
            {
                ResolveAttack(CurrentTurn, target);
                CurrentTurn.HasAction = false;
                CheckBattleEnd();
            }
            else
            {
                Log.Add("No adjacent enemy to attack! Move closer first.");
            }
        }

        public void PlayerEndTurn() { if (CurrentTurn != null && CurrentTurn.IsPlayer) NextTurn(); }

        // Heal amount per potion. Modest so combat still requires positioning
        // and attacks — potions are an emergency tool, not a free reset.
        private const int HEALTH_POTION_HEAL_AMOUNT = 12;

        /// <summary>
        /// Consume one health potion from the player's inventory and heal
        /// the active player combatant. Uses the player's action so it
        /// can't be paired with an attack on the same turn (5e rules).
        /// No-op when:
        ///   - It's not the player's turn
        ///   - The player has already used their action
        ///   - The player has no health potions in inventory
        ///   - The player is at full HP (so they don't waste a potion)
        /// </summary>
        public void UseHealthPotion()
        {
            if (CurrentTurn == null || !CurrentTurn.IsPlayer || !CurrentTurn.HasAction) return;
            var gm = GameManager.Instance;
            var inventory = gm?.Player?.Inventory;
            if (inventory == null) return;

            if (!inventory.HasItem(ItemIds.HealthPotion))
            {
                Log.Add("No health potions left!");
                return;
            }
            if (CurrentTurn.HP >= CurrentTurn.MaxHP)
            {
                Log.Add("Already at full HP — saving the potion.");
                return;
            }

            inventory.Remove(ItemIds.HealthPotion);
            int before = CurrentTurn.HP;
            CurrentTurn.Heal(HEALTH_POTION_HEAL_AMOUNT);
            int gained = CurrentTurn.HP - before;
            Log.Add($"Drank a health potion. +{gained} HP ({CurrentTurn.HP}/{CurrentTurn.MaxHP}).");
            // Sync the persistent PlayerData HP back from the combatant so
            // the heal carries over to the overworld after combat ends.
            if (gm?.Player != null) gm.Player.HP = CurrentTurn.HP;
            CurrentTurn.HasAction = false;
        }

        // === Spell Casting ===

        public void ToggleSpellMenu()
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

            // Compute target's save bonus for spell saves
            int targetSaveBonus = 0;
            if (target != null && spell.HasSave)
            {
                int saveStat = spell.SaveType switch
                {
                    Ability.STR => target.Strength,
                    Ability.DEX => target.Dexterity,
                    _ => 10 // CON/INT/WIS/CHA default to 10 for basic enemies
                };
                if (target.Sheet != null)
                    saveStat = target.Sheet.EffectiveAbilities.GetScore(spell.SaveType);
                targetSaveBonus = DiceRoller.AbilityModifier(saveStat);
            }

            // Build CastContext
            var ctx = new CastContext
            {
                Caster = sheet,
                Targets = target != null ? new object[] { target } : null,
                Spell = spell,
                SlotLevel = slotLevel > 0 ? slotLevel : spell.Level,
                Metamagic = MetamagicType.None,
                IsRitual = false,
                TargetSaveBonus = targetSaveBonus
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

                // Visual feedback for spell damage
                if (_renderer != null)
                    _renderer.ShowDamageNumber(new Vector3(target.X, target.Y, 0), dmgResult.AfterResistance, false);

                Demo.AI.DirectorEvents.Send(
                    $"spell hit {target.Name} for {dmgResult.AfterResistance}",
                    targetId: target.Name);
                if (!target.IsAlive)
                {
                    Log.Add($"{target.Name} is defeated!");
                    Demo.AI.DirectorEvents.Send($"defeated {target.Name}", targetId: target.Name);
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

        private void ProcessAITurn()
        {
            var ai = CurrentTurn;
            var player = Combatants.FirstOrDefault(c => c.IsPlayer && c.IsAlive);
            if (player == null) { NextTurn(); return; }

            if (!_brains.TryGetValue(ai, out var brain))
            {
                FallbackAI(ai, player);
                Invoke(nameof(NextTurn), 0.5f);
                return;
            }

            int aliveAllies = Combatants.Count(c => !c.IsPlayer && c.IsAlive && c != ai);
            CombatBrain.Action action;
            if (_neuralBrain != null && ForeverEngine.AI.Inference.InferenceEngine.Instance?.IsAvailable == true)
            {
                _neuralBrain.Configure(ai, brain);
                _neuralBrain.SetBattleContext(player, aliveAllies, ai.Behavior ?? "chase");
                action = _neuralBrain.DecideAction();
                // Keep Q-table updated even when using neural path
                if (_neuralBrain.UsingNeural)
                    brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");
            }
            else
            {
                action = brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");
            }

            switch (action)
            {
                case CombatBrain.Action.Advance:
                    MoveToward(ai, player.X, player.Y);
                    if (IsAdjacent(ai, player) && ai.HasAction)
                    {
                        ResolveAttack(ai, player);
                        ai.HasAction = false;
                        brain.AddReward(_gameConfig != null ? _gameConfig.RewardAdvanceHit : 0.1f);
                    }
                    break;

                case CombatBrain.Action.Retreat:
                    MoveAway(ai, player.X, player.Y);
                    if (ai.HP < ai.MaxHP * 0.3f)
                        brain.AddReward(_gameConfig != null ? _gameConfig.RewardRetreatLowHP : 0.2f);
                    break;

                case CombatBrain.Action.Flank:
                    MoveFlank(ai, player.X, player.Y);
                    break;

                case CombatBrain.Action.Attack:
                    if (IsAdjacent(ai, player) && ai.HasAction)
                    {
                        ResolveAttack(ai, player);
                        ai.HasAction = false;
                        brain.AddReward(_gameConfig != null ? _gameConfig.RewardAttackAdjacent : 0.3f);
                    }
                    else
                    {
                        MoveToward(ai, player.X, player.Y);
                    }
                    break;

                case CombatBrain.Action.Hold:
                    brain.AddReward(ai.Behavior == "guard"
                        ? (_gameConfig != null ? _gameConfig.RewardHoldGuard : 0.1f)
                        : (_gameConfig != null ? _gameConfig.PenaltyHoldChase : -0.05f));
                    break;
            }

            Invoke(nameof(NextTurn), 0.5f);
        }

        private void MoveToward(BattleCombatant ai, int targetX, int targetY)
        {
            while (ai.MovementRemaining > 0)
            {
                int dx = System.Math.Sign(targetX - ai.X);
                int dy = System.Math.Sign(targetY - ai.Y);
                if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
            }
        }

        private void MoveAway(BattleCombatant ai, int targetX, int targetY)
        {
            while (ai.MovementRemaining > 0)
            {
                int dx = -System.Math.Sign(targetX - ai.X);
                int dy = -System.Math.Sign(targetY - ai.Y);
                if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
            }
        }

        private void MoveFlank(BattleCombatant ai, int targetX, int targetY)
        {
            int dx = System.Math.Sign(targetX - ai.X);
            int dy = System.Math.Sign(targetY - ai.Y);
            int perpX = ai.X + dy, perpY = ai.Y - dx;
            if (!TryMove(ai, perpX, perpY))
            {
                perpX = ai.X - dy; perpY = ai.Y + dx;
                TryMove(ai, perpX, perpY);
            }
            MoveToward(ai, targetX, targetY);
        }

        private bool TryMove(BattleCombatant ai, int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= Grid.Width || ny >= Grid.Height) return false;
            if (!Grid.IsWalkable(nx, ny)) return false;
            if (Combatants.Any(c => c.IsAlive && c != ai && c.X == nx && c.Y == ny)) return false;
            ai.X = nx; ai.Y = ny; ai.MovementRemaining--;
            return true;
        }

        private void FallbackAI(BattleCombatant ai, BattleCombatant player)
        {
            int dist = System.Math.Abs(ai.X - player.X) + System.Math.Abs(ai.Y - player.Y);
            while (ai.MovementRemaining > 0 && dist > 1)
            {
                int dx = System.Math.Sign(player.X - ai.X);
                int dy = System.Math.Sign(player.Y - ai.Y);
                if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
                dist = System.Math.Abs(ai.X - player.X) + System.Math.Abs(ai.Y - player.Y);
            }
            if (IsAdjacent(ai, player) && ai.HasAction)
            {
                ResolveAttack(ai, player);
                ai.HasAction = false;
            }
        }

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
            // Phase 3 pivot: AI event hooks archived; ai variable removed.

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

                // Apply DDA damage multiplier for enemy attackers
                if (!attacker.IsPlayer)
                {
                    var dda = AI.Learning.DynamicDifficulty.Instance;
                    if (dda != null)
                        hpDamage = Mathf.RoundToInt(hpDamage * dda.EnemyDamageMult);
                }

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

                // Visual + audio feedback
                if (_renderer3D != null)
                {
                    _renderer3D.ShowDamage(target, dmgResult.AfterResistance, atkResult.Critical);
                    _renderer3D.ShowHitFlash(target);
                }
                else if (_renderer != null)
                    _renderer.ShowDamageNumber(new Vector3(target.X, target.Y, 0), dmgResult.AfterResistance, atkResult.Critical);
                if (atkResult.Critical)
                    Audio.SoundManager.Instance?.PlayCrit();
                else
                    Audio.SoundManager.Instance?.PlayHit();
                if (atkResult.Critical || target.HP <= 0)
                {
                    var cam = FindAnyObjectByType<MonoBehaviour.Camera.CameraController>();
                    cam?.Shake(atkResult.Critical ? 0.25f : 0.15f);
                }
                if (target.HP <= 0 && !target.IsPlayer)
                    Audio.SoundManager.Instance?.PlayDeath();

                if (attacker.IsPlayer)
                    Demo.AI.DirectorEvents.Send(
                        $"hit {target.Name} for {dmgResult.AfterResistance}",
                        targetId: target.Name);
                else if (target.IsPlayer)
                    Demo.AI.DirectorEvents.Send(
                        $"took {dmgResult.AfterResistance} damage from {attacker.Name}");

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
                        Demo.AI.DirectorEvents.Send($"killed {target.Name}", targetId: target.Name);
                        if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var killerBrain))
                            killerBrain.AddReward(_gameConfig != null ? _gameConfig.RewardKill : 0.5f);
                    }
                }

                // Q-learning: penalize target enemy for taking damage
                if (!target.IsPlayer && _brains.TryGetValue(target, out var tgtBrain))
                    tgtBrain.AddReward(_gameConfig != null ? _gameConfig.PenaltyDamageTaken : -0.1f);

                // All enemies rewarded for downing player (preserved)
                if (target.HP <= 0 && target.IsPlayer)
                    foreach (var b in _brains.Values) b.AddReward(1.0f);
            }
            else
            {
                Log.Add($"{attacker.Name} misses {target.Name}{advStr}. (d20={atkResult.NaturalRoll}, total={atkResult.Total} vs AC {target.AC})");
                _renderer?.ShowMiss(new Vector3(target.X, target.Y, 0));
                Audio.SoundManager.Instance?.PlayMiss();
                if (attacker.IsPlayer)
                    Demo.AI.DirectorEvents.Send($"missed {target.Name}", targetId: target.Name);
            }

            // Q-learning: reward/penalize enemy attacker for hit/miss
            if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var atkBrain))
                atkBrain.AddReward(atkResult.Hit
                    ? (_gameConfig != null ? _gameConfig.RewardHit : 0.5f)
                    : (_gameConfig != null ? _gameConfig.PenaltyMiss : -0.1f));
        }

        private bool IsAdjacent(BattleCombatant a, BattleCombatant b) =>
            System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) <= 1;

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
                Demo.AI.DirectorEvents.Send("player died");
            }
            else if (Combatants.All(c => c.IsPlayer || !c.IsAlive))
            {
                BattleOver = true; PlayerWon = true;
                Log.Add("Victory!");
                Audio.SoundManager.Instance?.PlayVictory();
                Demo.AI.DirectorEvents.Send(
                    $"victory: gold={_encounterData.GoldReward} xp={_encounterData.XPReward}");
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.LastBattleWon = true;
                    gm.LastBattleGoldEarned = _encounterData.GoldReward;
                    gm.LastBattleXPEarned = _encounterData.XPReward;

                    // Generate loot drops (use XP reward as CR proxy)
                    var loot = LootGenerator.GenerateLoot(_encounterData.XPReward, _encounterData.GoldReward);
                    UI.LootScreen.GoldEarned = _encounterData.GoldReward;
                    UI.LootScreen.XPEarned = _encounterData.XPReward;
                    UI.LootScreen.ItemsFound = loot;
                    UI.LootScreen.Show = true;
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

                // Phase 3 pivot: Q-table persistence to LongTermMemory archived
                // alongside MemoryManager. Will be reintroduced via Director Hub
                // memory tools in a follow-up. The brain still trains in-session.
            }
        }

        public void EndBattle()
        {
            if (PlayerWon)
                GameManager.Instance?.ReturnToOverworld();
            else
                GameManager.Instance?.PlayerDied();
        }
    }
}
