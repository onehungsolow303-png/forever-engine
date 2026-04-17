using System.Collections.Generic;
using ForeverEngine.Core.Enums;
using ForeverEngine.Core.Messages;
using ForeverEngine.Core.Messages.DTOs;
using ForeverEngine.Network;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Display-only combat renderer driven by server messages.
    /// Replaces the authoritative BattleManager with a thin visual layer
    /// that spawns models, animates actions, and exposes read-only state
    /// for BattleHUD and BattleInputController.
    /// </summary>
    public class BattleRenderer : UnityEngine.MonoBehaviour
    {
        public static BattleRenderer Instance { get; private set; }

        // ── Public read-only state (consumed by BattleHUD / BattleInputController) ──

        public string BattleId { get; private set; } = "";
        public BattleCombatantDto[] Combatants { get; private set; } = new BattleCombatantDto[0];
        public string ActiveCombatantId { get; private set; } = "";
        public int RoundNumber { get; private set; }
        public bool IsPlayerTurn => ActiveCombatantId == _localPlayerId;
        public bool IsActive { get; private set; }
        public bool BattleOver { get; private set; }
        public bool PlayerWon { get; private set; }
        public int XpEarned { get; private set; }
        public int GoldEarned { get; private set; }
        public ItemDto[] LootItems { get; private set; } = new ItemDto[0];
        public List<string> BattleLog { get; private set; } = new();
        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }

        // Turn resource state from TurnStart
        public int MovementRemaining { get; private set; }
        public bool HasAction { get; private set; }
        public bool HasBonusAction { get; private set; }

        // ── Private state ────────────────────────────────────────────────────

        private readonly Dictionary<string, GameObject> _models = new();
        private readonly Dictionary<string, ModelAnimator> _animators = new();
        private BattleZoneManager _zoneManager;
        private Vector3 _gridOrigin;
        private string _localPlayerId = "";
        private string[] _turnOrder = new string[0];

        // ── MonoBehaviour lifecycle ──────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Handler registration ─────────────────────────────────────────────

        /// <summary>
        /// Register for server battle messages on the NetworkClient dispatcher.
        /// Call once after NetworkClient is connected (e.g. from ConnectionManager).
        /// </summary>
        public void RegisterHandlers()
        {
            var client = NetworkClient.Instance;
            if (client == null)
            {
                Debug.LogWarning("[BattleRenderer] NetworkClient.Instance is null — cannot register handlers.");
                return;
            }

            client.RegisterHandler<BattleStartMessage>(HandleBattleStart);
            client.RegisterHandler<BattleTurnStartMessage>(HandleBattleTurnStart);
            client.RegisterHandler<BattleActionResultMessage>(HandleBattleActionResult);
            client.RegisterHandler<BattleEndMessage>(HandleBattleEnd);
        }

        /// <summary>Unregister all battle message handlers from NetworkClient.</summary>
        public void UnregisterHandlers()
        {
            var client = NetworkClient.Instance;
            if (client == null) return;

            client.UnregisterHandler<BattleStartMessage>();
            client.UnregisterHandler<BattleTurnStartMessage>();
            client.UnregisterHandler<BattleActionResultMessage>();
            client.UnregisterHandler<BattleEndMessage>();
        }

        // ── Message handlers ─────────────────────────────────────────────────

        /// <summary>
        /// Handle battle start: store state, create zone manager, spawn models.
        /// PUBLIC so ConnectionManager can forward the first message directly.
        /// </summary>
        public void HandleBattleStart(BattleStartMessage msg)
        {
            // Read local player ID from server state cache
            if (ServerStateCache.Instance != null)
                _localPlayerId = ServerStateCache.Instance.LocalPlayerId ?? "";

            BattleId = msg.BattleId;
            Combatants = msg.Combatants ?? new BattleCombatantDto[0];
            GridWidth = msg.GridWidth;
            GridHeight = msg.GridHeight;
            _turnOrder = msg.TurnOrder ?? new string[0];
            RoundNumber = 1;
            ActiveCombatantId = "";
            IsActive = true;
            BattleOver = false;
            PlayerWon = false;
            XpEarned = 0;
            GoldEarned = 0;
            LootItems = new ItemDto[0];
            BattleLog.Clear();
            BattleLog.Add("Battle started!");

            // Set up grid origin — center roughly on camera or at world origin
            _gridOrigin = transform.position;

            // Create BattleZoneManager for coordinate conversion
            SetupZoneManager(msg.GridWidth, msg.GridHeight);

            // Spawn 3D models for each combatant
            foreach (var combatant in Combatants)
                SpawnModel(combatant);

            Debug.Log($"[BattleRenderer] Battle started: {BattleId}, {Combatants.Length} combatants, {GridWidth}x{GridHeight} grid");
        }

        private void HandleBattleTurnStart(BattleTurnStartMessage msg)
        {
            if (msg.BattleId != BattleId) return;

            ActiveCombatantId = msg.ActiveCombatantId;
            RoundNumber = msg.RoundNumber;
            MovementRemaining = msg.MovementRemaining;
            HasAction = msg.HasAction;
            HasBonusAction = msg.HasBonusAction;

            BattleLog.Add($"Round {RoundNumber}: {GetCombatantName(ActiveCombatantId)}'s turn");

            // Visual highlight: pulse the active combatant's model
            HighlightActiveCombatant();

            Debug.Log($"[BattleRenderer] Turn start: {GetCombatantName(ActiveCombatantId)} (round {RoundNumber}, player turn: {IsPlayerTurn})");
        }

        private void HandleBattleActionResult(BattleActionResultMessage msg)
        {
            if (msg.BattleId != BattleId) return;

            switch (msg.ActionType)
            {
                case BattleActionType.Move:
                    HandleMoveResult(msg);
                    break;

                case BattleActionType.MeleeAttack:
                case BattleActionType.RangedAttack:
                    HandleAttackResult(msg);
                    break;

                case BattleActionType.CastSpell:
                    HandleSpellResult(msg);
                    break;

                case BattleActionType.Dodge:
                case BattleActionType.Dash:
                case BattleActionType.Disengage:
                case BattleActionType.Help:
                case BattleActionType.Hide:
                case BattleActionType.EndTurn:
                    HandleMiscActionResult(msg);
                    break;

                default:
                    HandleMiscActionResult(msg);
                    break;
            }

            // Update conditions on target if provided
            if (!string.IsNullOrEmpty(msg.TargetId) && msg.Conditions != null && msg.Conditions.Length > 0)
            {
                var target = FindCombatant(msg.TargetId);
                if (target != null)
                    target.Conditions = msg.Conditions;
            }

            // Append narrative to log
            if (!string.IsNullOrEmpty(msg.Narrative))
                BattleLog.Add(msg.Narrative);
        }

        private void HandleBattleEnd(BattleEndMessage msg)
        {
            if (msg.BattleId != BattleId) return;

            BattleOver = true;
            PlayerWon = msg.Victory;
            XpEarned = msg.XpEarned;
            GoldEarned = msg.GoldEarned;
            LootItems = msg.LootItems ?? new ItemDto[0];
            IsActive = false;

            string result = msg.Victory ? "VICTORY" : "DEFEAT";
            BattleLog.Add($"Battle ended: {result}! XP: {XpEarned}, Gold: {GoldEarned}");

            Debug.Log($"[BattleRenderer] Battle ended: {result}, XP={XpEarned}, Gold={GoldEarned}, Loot={LootItems.Length} items");
        }

        // ── Action result sub-handlers ───────────────────────────────────────

        private void HandleMoveResult(BattleActionResultMessage msg)
        {
            var actor = FindCombatant(msg.ActorId);
            if (actor == null) return;

            // For move actions, update combatant position if target position is
            // encoded. The server sends updated positions; we update the DTO and
            // smoothly move the model. Position data comes from a follow-up
            // state update or is encoded in TargetId as "x,y".
            if (!string.IsNullOrEmpty(msg.TargetId) && msg.TargetId.Contains(","))
            {
                var parts = msg.TargetId.Split(',');
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0].Trim(), out int newX) &&
                    int.TryParse(parts[1].Trim(), out int newY))
                {
                    actor.X = newX;
                    actor.Y = newY;
                }
            }

            // Move the model to the new grid position
            if (_models.TryGetValue(msg.ActorId, out var model))
            {
                Vector3 targetPos = GridToWorld(actor.X, actor.Y);
                // Smooth move handled in Update via lerp
                if (_animators.TryGetValue(msg.ActorId, out var anim))
                    anim.SetBasePosition(targetPos);
                model.transform.position = targetPos;
            }

            BattleLog.Add($"{actor.Name} moves to ({actor.X}, {actor.Y})");
        }

        private void HandleAttackResult(BattleActionResultMessage msg)
        {
            var actor = FindCombatant(msg.ActorId);
            bool hit = msg.Hit ?? false;
            int damage = msg.Damage ?? 0;

            string attackType = msg.ActionType == BattleActionType.MeleeAttack ? "melee" : "ranged";

            if (!string.IsNullOrEmpty(msg.TargetId))
            {
                var target = FindCombatant(msg.TargetId);
                if (target != null)
                {
                    if (hit)
                    {
                        target.Hp = Mathf.Max(0, target.Hp - damage);
                        BattleLog.Add($"{actor?.Name ?? msg.ActorId} hits {target.Name} for {damage} {attackType} damage (HP: {target.Hp}/{target.MaxHp})");
                    }
                    else
                    {
                        BattleLog.Add($"{actor?.Name ?? msg.ActorId} misses {target.Name} ({attackType})");
                    }

                    // Check for death
                    bool died = target.Hp <= 0;

                    // Animate
                    AnimateAttack(msg.ActorId, msg.TargetId, hit, damage);

                    if (died)
                        AnimateDeath(msg.TargetId);
                }
            }
            else
            {
                BattleLog.Add($"{actor?.Name ?? msg.ActorId} attacks ({attackType})");
            }
        }

        private void HandleSpellResult(BattleActionResultMessage msg)
        {
            // Spells follow the same hit/damage pattern as attacks
            HandleAttackResult(msg);
        }

        private void HandleMiscActionResult(BattleActionResultMessage msg)
        {
            var actor = FindCombatant(msg.ActorId);
            string actorName = actor?.Name ?? msg.ActorId;
            string actionName = msg.ActionType.ToString();
            BattleLog.Add($"{actorName} uses {actionName}");
        }

        // ── Public query methods ─────────────────────────────────────────────

        /// <summary>Find a combatant DTO by ID.</summary>
        public BattleCombatantDto FindCombatant(string id)
        {
            if (string.IsNullOrEmpty(id) || Combatants == null) return null;
            foreach (var c in Combatants)
                if (c.Id == id) return c;
            return null;
        }

        /// <summary>Convert grid coordinates to world position.</summary>
        public Vector3 GridToWorld(int x, int y)
        {
            if (_zoneManager != null)
                return _zoneManager.GridToWorld(x, y);

            // Fallback: simple offset from grid origin
            return _gridOrigin + new Vector3(
                x * BattleZoneManager.CellSize + BattleZoneManager.CellSize * 0.5f,
                0f,
                y * BattleZoneManager.CellSize + BattleZoneManager.CellSize * 0.5f);
        }

        /// <summary>Convert world position to grid coordinates.</summary>
        public (int x, int y) WorldToGrid(Vector3 pos)
        {
            if (_zoneManager != null)
                return _zoneManager.WorldToGrid(pos);

            // Fallback
            Vector3 local = pos - _gridOrigin;
            int gx = Mathf.Clamp(Mathf.FloorToInt(local.x / BattleZoneManager.CellSize), 0, GridWidth - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(local.z / BattleZoneManager.CellSize), 0, GridHeight - 1);
            return (gx, gy);
        }

        /// <summary>Check if a grid cell is a valid move target (in bounds, not occupied).</summary>
        public bool IsValidMoveTarget(int x, int y)
        {
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
                return false;

            // Check no living combatant occupies the cell
            return GetCombatantAt(x, y) == null;
        }

        /// <summary>Get the combatant at a grid position, or null.</summary>
        public BattleCombatantDto GetCombatantAt(int x, int y)
        {
            if (Combatants == null) return null;
            foreach (var c in Combatants)
            {
                if (c.X == x && c.Y == y && c.Hp > 0)
                    return c;
            }
            return null;
        }

        /// <summary>Destroy all models, zone manager, and this component.</summary>
        public void Cleanup()
        {
            UnregisterHandlers();

            foreach (var kvp in _models)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _models.Clear();
            _animators.Clear();

            if (_zoneManager != null)
            {
                _zoneManager.Deactivate();
                _zoneManager = null;
            }

            IsActive = false;
            BattleId = "";
            Combatants = new BattleCombatantDto[0];
            BattleLog.Clear();

            Debug.Log("[BattleRenderer] Cleaned up.");
            Destroy(gameObject);
        }

        // ── Zone manager setup ───────────────────────────────────────────────

        private void SetupZoneManager(int gridWidth, int gridHeight)
        {
            // Create a BattleZoneManager for coordinate conversions
            var zoneGO = new GameObject("BattleZoneManager_ServerDriven");
            _zoneManager = zoneGO.AddComponent<BattleZoneManager>();

            // NOTE: InitializeFromServer(gridWidth, gridHeight, origin) is added
            // by Task 4. Until then, we set properties directly via the fallback
            // GridToWorld/WorldToGrid methods on this class.
            // When Task 4 is complete, uncomment the line below and remove the
            // manual property setup:
            // _zoneManager.InitializeFromServer(gridWidth, gridHeight, _gridOrigin);

            // For now the zone manager won't have a grid, so coordinate
            // conversion falls through to our local fallback implementation.
            // We null out _zoneManager to ensure the fallback path is used.
            Destroy(zoneGO);
            _zoneManager = null;

            GridWidth = gridWidth;
            GridHeight = gridHeight;
        }

        // ── Model management ─────────────────────────────────────────────────

        /// <summary>
        /// Spawn a 3D model for a combatant. Uses ModelRegistry to find a prefab
        /// from the ModelId, falling back to a colored capsule primitive.
        /// </summary>
        private void SpawnModel(BattleCombatantDto combatant)
        {
            GameObject model = null;

            // Try to load from ModelRegistry using the DTO's ModelId
            if (!string.IsNullOrEmpty(combatant.ModelId))
            {
                var (path, scale) = ModelRegistry.Resolve(combatant.ModelId);
                if (!string.IsNullOrEmpty(path))
                {
                    var prefab = Resources.Load<GameObject>($"Models/{path}");
                    if (prefab != null)
                    {
                        model = Object.Instantiate(prefab);
                        model.transform.localScale *= scale;
                    }
                }

                // Also try direct path as a fallback
                if (model == null)
                {
                    var prefab = Resources.Load<GameObject>($"Models/{combatant.ModelId}");
                    if (prefab != null)
                        model = Object.Instantiate(prefab);
                }
            }

            // Fallback: colored capsule
            if (model == null)
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
                var mr = model.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                Color capsuleColor = combatant.IsPlayer
                    ? new Color(0.2f, 0.6f, 1f)
                    : new Color(0.9f, 0.2f, 0.2f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", capsuleColor);
                else
                    mat.color = capsuleColor;
                mr.material = mat;
                var col = model.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }

            model.name = $"Model_{combatant.Name}";
            Vector3 worldPos = GridToWorld(combatant.X, combatant.Y);
            model.transform.position = worldPos;

            _models[combatant.Id] = model;

            // Attach procedural animator
            var anim = model.AddComponent<ModelAnimator>();
            anim.SetBasePosition(worldPos);
            _animators[combatant.Id] = anim;
        }

        // ── Animation helpers ────────────────────────────────────────────────

        /// <summary>
        /// Animate an attack: PlayAttack on actor toward target, PlayHit on
        /// target if the attack hit.
        /// </summary>
        private void AnimateAttack(string actorId, string targetId, bool hit, int damage)
        {
            Vector3 targetWorldPos = Vector3.zero;

            // Get target position for attack direction
            if (_models.TryGetValue(targetId, out var targetModel))
                targetWorldPos = targetModel.transform.position;

            // Actor attack animation
            if (_animators.TryGetValue(actorId, out var actorAnim))
            {
                actorAnim.PlayAttack(targetWorldPos, () =>
                {
                    // After attack animation completes, play hit on target
                    if (hit && _animators.TryGetValue(targetId, out var tgtAnim))
                        tgtAnim.PlayHit();
                });
            }
            else if (hit)
            {
                // No actor animator, but still play hit on target
                if (_animators.TryGetValue(targetId, out var tgtAnim))
                    tgtAnim.PlayHit();
            }
        }

        /// <summary>Play death animation on a combatant's model.</summary>
        private void AnimateDeath(string combatantId)
        {
            if (_animators.TryGetValue(combatantId, out var anim))
                anim.PlayDeath();
        }

        // ── Visual helpers ───────────────────────────────────────────────────

        private void HighlightActiveCombatant()
        {
            // Scale pulse is handled by the ModelAnimator idle bob.
            // We could add an emissive highlight here in the future.
            // For now, just ensure the active model is visible.
            foreach (var kvp in _models)
            {
                if (kvp.Value == null) continue;
                // Could add outline/glow shader here
            }
        }

        private string GetCombatantName(string id)
        {
            var c = FindCombatant(id);
            return c?.Name ?? id;
        }
    }
}
