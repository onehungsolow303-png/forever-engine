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

        [Header("Shared Schemas")]
        [Tooltip("Path to C:\\Dev\\.shared\\schemas\\ for cross-project validation")]
        public string SharedSchemaPath = "C:\\Dev\\.shared\\schemas";
    }
}
