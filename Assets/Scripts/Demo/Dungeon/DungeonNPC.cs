using System.Collections;
using UnityEngine;
using ForeverEngine.Demo.AI;
using ForeverEngine.Demo.UI;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Roles an NPC can have inside a dungeon room.
    /// </summary>
    public enum DungeonNPCRole
    {
        Merchant,
        Prisoner,
        QuestGiver,
        AmbientEnemy,
    }

    /// <summary>
    /// Per-NPC dungeon controller. Handles patrol (AmbientEnemy), proximity
    /// interaction prompts (Merchant / Prisoner / QuestGiver), and dispatches
    /// to the appropriate action on [E] press.
    ///
    /// Lives in Assets/Scripts/Demo/Dungeon/ alongside DungeonExplorer.
    /// Spawned by DungeonNPCSpawner; configured via DungeonNPCConfig SO.
    /// </summary>
    public class DungeonNPC : UnityEngine.MonoBehaviour
    {
        // ── Role-based fields ──────────────────────────────────────────────────
        [Header("Identity")]
        public DungeonNPCRole Role = DungeonNPCRole.Merchant;
        public string NPCName = "Unknown";
        public int RoomIndex = -1;

        [Header("Interaction")]
        public float InteractionRadius = 3f;
        public bool HasInteracted;

        // ── Patrol (AmbientEnemy only) ─────────────────────────────────────────
        [Header("Patrol (AmbientEnemy)")]
        public Transform WaypointA;
        public Transform WaypointB;
        public float PatrolSpeed = 2f;

        // ── Internal state ─────────────────────────────────────────────────────
        private Transform _playerTransform;
        private TextMesh _promptMesh;
        private bool _promptVisible;

        // Patrol
        private Transform _patrolTarget;
        private bool _patrolFlip;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Start()
        {
            // Find player by tag (DungeonExplorer tags the spawned player GO)
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;

            BuildPromptMesh();

            // Boot patrol state for ambient enemies
            if (Role == DungeonNPCRole.AmbientEnemy && WaypointA != null)
                _patrolTarget = WaypointA;
        }

        private void Update()
        {
            if (Role == DungeonNPCRole.AmbientEnemy)
                UpdatePatrol();
            else
                UpdateInteraction();
        }

        private void LateUpdate()
        {
            // Billboard: keep prompt facing the main camera
            if (_promptMesh != null && _promptVisible)
            {
                var cam = Camera.main;
                if (cam != null)
                    _promptMesh.transform.rotation = cam.transform.rotation;
            }
        }

        // ── Patrol ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ping-pong between WaypointA and WaypointB. Flips the target when
        /// the NPC arrives within 0.1 units; faces movement direction each frame.
        /// </summary>
        private void UpdatePatrol()
        {
            if (_patrolTarget == null) return;

            Vector3 target = _patrolTarget.position;
            Vector3 current = transform.position;
            Vector3 newPos = Vector3.MoveTowards(current, target, PatrolSpeed * Time.deltaTime);

            // Face movement direction
            Vector3 delta = newPos - current;
            if (delta.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(delta.normalized);

            transform.position = newPos;

            // Flip target when close enough
            if (Vector3.Distance(newPos, target) < 0.1f)
            {
                _patrolFlip = !_patrolFlip;
                _patrolTarget = _patrolFlip
                    ? (WaypointB != null ? WaypointB : WaypointA)
                    : WaypointA;
            }
        }

        // ── Interaction ────────────────────────────────────────────────────────

        /// <summary>
        /// Show/hide the "[E] …" prompt based on distance to player.
        /// Dispatch on E press when inside radius and dialogue is not open.
        /// </summary>
        private void UpdateInteraction()
        {
            if (HasInteracted && Role == DungeonNPCRole.Prisoner) return;
            if (_playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool inRange = dist <= InteractionRadius;

            SetPromptVisible(inRange);

            if (!inRange) return;
            if (DialoguePanel.Instance != null && DialoguePanel.Instance.IsOpen) return;
            if (!Input.GetKeyDown(KeyCode.E)) return;

            DispatchInteraction();
        }

        private void DispatchInteraction()
        {
            switch (Role)
            {
                case DungeonNPCRole.Merchant:
                    OnMerchantInteract();
                    break;
                case DungeonNPCRole.Prisoner:
                    OnPrisonerInteract();
                    break;
                case DungeonNPCRole.QuestGiver:
                    OnQuestGiverInteract();
                    break;
            }
        }

        // ── Role handlers ──────────────────────────────────────────────────────

        /// <summary>
        /// Merchant interaction — sells health potions for 25 gold each.
        /// </summary>
        private void OnMerchantInteract()
        {
            var gm = GameManager.Instance;
            var inventory = gm?.Player?.Inventory;
            if (inventory == null)
            {
                Debug.Log($"[DungeonNPC] Merchant '{NPCName}': No inventory available.");
                return;
            }

            int gold = gm.Player.Gold;
            // Simple shop: offer health potions for 25 gold each
            int potionCost = 25;
            if (gold >= potionCost)
            {
                gm.Player.Gold -= potionCost;
                inventory.Add(new ForeverEngine.ECS.Data.ItemInstance
                {
                    ItemId     = ForeverEngine.Demo.ItemIds.HealthPotion,
                    StackCount = 1,
                    MaxStack   = 5
                });
                Debug.Log($"[DungeonNPC] Bought health potion from '{NPCName}' for {potionCost}g. Gold remaining: {gm.Player.Gold}");
            }
            else
            {
                Debug.Log($"[DungeonNPC] Not enough gold for health potion ({gold}/{potionCost}g).");
            }

            SetPromptVisible(false);
            // Allow repeated purchases
        }

        /// <summary>
        /// Prisoner rescue: one-shot, scales NPC to zero via coroutine, grants +50 XP.
        /// </summary>
        private void OnPrisonerInteract()
        {
            if (HasInteracted) return;
            HasInteracted = true;
            SetPromptVisible(false);

            Debug.Log($"[DungeonNPC] Prisoner '{NPCName}' rescued!");

            // Grant XP via CharacterSheet (preferred) or log a warning
            var gm = GameManager.Instance;
            if (gm?.Character != null)
                gm.Character.GainXP(50);
            else
                Debug.LogWarning("[DungeonNPC] No CharacterSheet found — XP not awarded.");

            StartCoroutine(ScaleToZeroAndDestroy());
        }

        /// <summary>
        /// QuestGiver: routes to Director Hub via DirectorEvents.SendDialogue.
        /// </summary>
        private void OnQuestGiverInteract()
        {
            string locationId = DungeonExplorer.Instance != null
                ? $"dungeon_room_{RoomIndex}"
                : null;

            DirectorEvents.SendDialogue(
                $"Hello {NPCName}, what quest do you have for me?",
                npcId: NPCName,
                onResponse: response =>
                {
                    if (!string.IsNullOrEmpty(response))
                        Debug.Log($"[DungeonNPC] QuestGiver '{NPCName}': {response}");
                },
                locationId: locationId);
        }

        // ── Prompt mesh ────────────────────────────────────────────────────────

        private void BuildPromptMesh()
        {
            var promptGO = new GameObject("InteractionPrompt");
            promptGO.transform.SetParent(transform, worldPositionStays: false);
            // Float above NPC head
            promptGO.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            _promptMesh = promptGO.AddComponent<TextMesh>();
            _promptMesh.text = GetPromptText();
            _promptMesh.fontSize = 48;
            _promptMesh.characterSize = 0.05f;
            _promptMesh.anchor = TextAnchor.MiddleCenter;
            _promptMesh.alignment = TextAlignment.Center;
            _promptMesh.color = Color.white;

            promptGO.SetActive(false);
            _promptVisible = false;
        }

        private string GetPromptText() => Role switch
        {
            DungeonNPCRole.Merchant   => "[E] Trade",
            DungeonNPCRole.Prisoner   => "[E] Rescue",
            DungeonNPCRole.QuestGiver => "[E] Talk",
            _                         => "[E]",
        };

        private void SetPromptVisible(bool visible)
        {
            if (_promptVisible == visible) return;
            _promptVisible = visible;
            if (_promptMesh != null)
                _promptMesh.gameObject.SetActive(visible);
        }

        // ── Coroutines ─────────────────────────────────────────────────────────

        /// <summary>
        /// Smoothly scales the prisoner NPC to zero over 0.5 s, then destroys.
        /// </summary>
        private IEnumerator ScaleToZeroAndDestroy()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
