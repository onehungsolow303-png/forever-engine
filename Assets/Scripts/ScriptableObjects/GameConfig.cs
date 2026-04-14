using UnityEngine;

namespace ForeverEngine
{
    /// <summary>
    /// Game configuration — rewritten from pygame config.py.
    /// ScriptableObject so designers can tweak values in the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Forever Engine/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Display")]
        public int TileSize = 32;
        public int TargetFPS = 60;

        [Header("Gameplay")]
        public int FogSightRadius = 16;       // FOW_SIGHT_RADIUS
        public int CombatDetectRange = 12;    // COMBAT_DETECT_RANGE
        public int DefaultMovementPerTurn = 6;
        public float ParallaxStrength = 0.05f;

        [Header("Colors — Tokens")]
        public Color PlayerColor = new Color(0.2f, 0.4f, 1f);
        public Color EnemyColor = new Color(1f, 0.2f, 0.2f);
        public Color NPCColor = new Color(1f, 0.84f, 0f);
        public Color NeutralColor = new Color(0.6f, 0.6f, 0.6f);

        [Header("Colors — Fog")]
        public Color FogUnexplored = new Color(0f, 0f, 0f, 1f);
        public Color FogExplored = new Color(0f, 0f, 0f, 0.6f);

        [Header("Colors — UI")]
        public Color UIBackground = new Color(0.12f, 0.12f, 0.15f, 0.86f);
        public Color UIText = new Color(0.85f, 0.85f, 0.85f);
        public Color HPGreen = new Color(0.2f, 0.8f, 0.2f);
        public Color HPRed = new Color(0.8f, 0.2f, 0.2f);

        [Header("Sprites")]
        public int TokenRadius = 10;
        public int TokenBorder = 2;

        [Header("AI")]
        public float AITurnDelay = 0.33f;  // Seconds before AI acts (visual feedback)

        [Header("AI — Q-Learning (State Space B)")]
        [Tooltip("Lower = more stable with 1296-state table. 0.12 recommended for convergence.")]
        public float QLearningRate = 0.12f;
        [Tooltip("Higher = longer-horizon planning. 0.9 balances immediate + future reward.")]
        public float QDiscountFactor = 0.9f;
        [Tooltip("Lower = less random exploration. 0.15 once state space is well-covered.")]
        public float QExplorationRate = 0.15f;

        [Header("AI — Rewards (offensive)")]
        [Tooltip("Advance then hit in same turn — small bonus for aggression")]
        public float RewardAdvanceHit = 0.15f;
        [Tooltip("Attack when already adjacent — bread-and-butter melee")]
        public float RewardAttackAdjacent = 0.35f;
        [Tooltip("Successful melee hit (from AttackResolver)")]
        public float RewardHit = 0.4f;
        [Tooltip("Melee miss penalty")]
        public float PenaltyMiss = -0.15f;
        [Tooltip("Successful ranged hit — slightly less than melee to prefer closing distance")]
        public float RewardRangedHit = 0.35f;
        [Tooltip("Ranged miss — slightly harsher to discourage sniping when out of range")]
        public float PenaltyRangedMiss = -0.2f;
        [Tooltip("Kill any combatant — strong positive signal")]
        public float RewardKill = 0.6f;

        [Header("AI — Rewards (defensive)")]
        [Tooltip("Retreat when below 30% HP — survival instinct")]
        public float RewardRetreatLowHP = 0.25f;
        [Tooltip("Hold position when guarding — tactical patience")]
        public float RewardHoldGuard = 0.15f;
        [Tooltip("Hold when should be chasing — mild discouragement")]
        public float PenaltyHoldChase = -0.08f;
        [Tooltip("Taking damage — universal penalty")]
        public float PenaltyDamageTaken = -0.12f;
        [Tooltip("Move to protect a critically wounded ally")]
        public float RewardProtectAlly = 0.3f;

        [Header("Encounters")]
        public int DayXPBudgetPerLevel = 40;
        public int NightXPBudgetPerLevel = 60;
        public int MaxEnemiesPerEncounter = 4;
        [Range(0f, 1f)]
        public float EncounterTemplateChance = 0.6f;

        [Header("Shared Schemas")]
        [Tooltip("Path to C:\\Dev\\.shared\\schemas\\ for cross-project validation")]
        public string SharedSchemaPath = "C:\\Dev\\.shared\\schemas";
    }
}
