using UnityEngine;
using ForeverEngine.Demo.UI;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// IMGUI-based dungeon minimap. Two rendering modes:
    ///   - Corner overlay: always visible, 200x200 at top-right.
    ///   - Full overlay: Tab toggle, 60% of screen centered with dim background.
    ///
    /// Initialize via <see cref="Initialize"/> after dungeon build completes.
    /// Wire Tab input in DungeonExplorer by calling <see cref="ToggleFullMap"/>.
    ///
    /// Uses <see cref="UITheme"/> for consistent dark-fantasy styling.
    /// </summary>
    public class DungeonMinimap : UnityEngine.MonoBehaviour
    {
        // ── Public interface ─────────────────────────────────────────────────

        /// <summary>True when the full-screen map is open. DungeonExplorer uses this to suppress movement.</summary>
        public bool IsFullOpen { get; private set; }

        // ── Constants / sizes ────────────────────────────────────────────────

        private const float CornerSize        = 200f;
        private const float CornerPadding     = 10f;
        private const float FullMapFraction   = 0.6f;
        private const float PlayerDotRadius   = 5f;
        private const float NPCDotRadius      = 4f;
        private const float UnexploredSize    = 6f;
        private const float EdgeThickness     = 1.2f;
        private const float OutlineThickness  = 2f;

        // ── Colors (themed) ──────────────────────────────────────────────────

        // Room tiers — richer dark-fantasy palette, distinct per tier
        private static readonly Color ColTier1         = new Color(0.18f, 0.55f, 0.35f, 0.75f);  // Deep emerald
        private static readonly Color ColTier2         = new Color(0.72f, 0.58f, 0.18f, 0.75f);  // Aged gold
        private static readonly Color ColTier3         = new Color(0.68f, 0.20f, 0.22f, 0.75f);  // Blood crimson
        private static readonly Color ColCorridor      = new Color(0.30f, 0.28f, 0.32f, 0.55f);  // Dusty stone
        private static readonly Color ColEntranceOutline = new Color(0.25f, 0.75f, 0.35f, 0.9f); // Verdant green
        private static readonly Color ColUnexplored    = new Color(0.25f, 0.23f, 0.28f, 0.45f);  // Shadowed stone
        private static readonly Color ColEdge          = new Color(0.45f, 0.42f, 0.48f, 0.4f);   // Faded stone

        // ── Runtime data ─────────────────────────────────────────────────────

        private DADungeonBuilder _builder;
        private Transform _playerTransform;

        // World-space AABB of all rooms (X/Z plane)
        private float _worldMinX, _worldMinZ, _worldMaxX, _worldMaxZ;

        // NPC cache
        private DungeonNPC[]  _npcCache;
        private int           _lastNpcRoom = -2;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Called once after dungeon build. Computes world AABB from all room bounds.
        /// </summary>
        public void Initialize(DADungeonBuilder builder, Transform playerTransform)
        {
            _builder         = builder;
            _playerTransform = playerTransform;

            if (_builder == null || _builder.Rooms == null || _builder.Rooms.Length == 0)
                return;

            // Compute world AABB (X/Z)
            const float Margin = 5f;
            _worldMinX = float.MaxValue;
            _worldMinZ = float.MaxValue;
            _worldMaxX = float.MinValue;
            _worldMaxZ = float.MinValue;

            foreach (var room in _builder.Rooms)
            {
                Bounds b = room.WorldBounds;
                if (b.min.x < _worldMinX) _worldMinX = b.min.x;
                if (b.min.z < _worldMinZ) _worldMinZ = b.min.z;
                if (b.max.x > _worldMaxX) _worldMaxX = b.max.x;
                if (b.max.z > _worldMaxZ) _worldMaxZ = b.max.z;
            }

            _worldMinX -= Margin;
            _worldMinZ -= Margin;
            _worldMaxX += Margin;
            _worldMaxZ += Margin;
        }

        /// <summary>Toggle the full-screen map open/closed.</summary>
        public void ToggleFullMap()
        {
            IsFullOpen = !IsFullOpen;
        }

        // ── IMGUI rendering ──────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_builder == null || _builder.Rooms == null) return;
            if (DialoguePanel.Instance != null && DialoguePanel.Instance.IsOpen) return;

            if (IsFullOpen)
                DrawFullOverlay();
            else
                DrawCornerOverlay();
        }

        private void DrawCornerOverlay()
        {
            float x = Screen.width - CornerSize - CornerPadding;
            float y = CornerPadding;
            var mapRect = new Rect(x, y, CornerSize, CornerSize);

            UITheme.DrawPanel(mapRect);
            DrawMapContent(mapRect, fullMode: false);
        }

        private void DrawFullOverlay()
        {
            // Dim the entire screen
            UITheme.DrawRect(new Rect(0, 0, Screen.width, Screen.height),
                new Color(0f, 0f, 0f, 0.6f));

            float mapSize = Mathf.Min(Screen.width, Screen.height) * FullMapFraction;
            float cx = (Screen.width  - mapSize) * 0.5f;
            float cy = (Screen.height - mapSize) * 0.5f;
            var mapRect = new Rect(cx, cy, mapSize, mapSize);

            // Dark panel with gold border
            UITheme.DrawPanel(mapRect);

            DrawMapContent(mapRect, fullMode: true);

            // Header label using themed gold header style
            GUI.Label(new Rect(cx, cy - 22f, mapSize, 20f),
                "DUNGEON MAP  (Tab to close)", UITheme.Header(UITheme.FontMedium));
        }

        private void DrawMapContent(Rect mapRect, bool fullMode)
        {
            if (_builder == null) return;

            var state    = GameManager.Instance?.PendingDungeonState;
            var graph    = _builder.RoomGraph;
            var rooms    = _builder.Rooms;

            // ── 1. Edges ─────────────────────────────────────────────────────
            if (graph != null)
            {
                foreach (var kvp in graph)
                {
                    int fromIdx = kvp.Key;
                    if (fromIdx < 0 || fromIdx >= rooms.Length) continue;
                    Vector2 fromCenter = WorldToMinimap(rooms[fromIdx].WorldBounds.center, mapRect);

                    foreach (int toIdx in kvp.Value)
                    {
                        // Only draw each edge once
                        if (toIdx <= fromIdx) continue;
                        if (toIdx >= rooms.Length) continue;

                        Vector2 toCenter = WorldToMinimap(rooms[toIdx].WorldBounds.center, mapRect);
                        DrawLine(fromCenter, toCenter, ColEdge, EdgeThickness);
                    }
                }
            }

            // ── 2. Rooms ─────────────────────────────────────────────────────
            for (int i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                bool visited = state != null && state.HasVisited(room.Index);

                // Check if adjacent to any visited room (for "fog of war reveal")
                bool adjacentToVisited = false;
                if (!visited && graph != null && graph.TryGetValue(room.Index, out var neighbors))
                {
                    foreach (int nb in neighbors)
                    {
                        if (state != null && state.HasVisited(nb))
                        {
                            adjacentToVisited = true;
                            break;
                        }
                    }
                }

                Rect roomRect = WorldBoundsToMinimap(room.WorldBounds, mapRect);

                if (visited)
                {
                    // Fill color by type / tier
                    Color fillColor = room.IsCorridor
                        ? ColCorridor
                        : room.Tier switch
                        {
                            1 => ColTier1,
                            2 => ColTier2,
                            3 => ColTier3,
                            _ => ColTier1,   // default non-corridor to Tier1 look
                        };

                    UITheme.DrawRect(roomRect, fillColor);

                    // Outlines
                    if (room.IsBoss)
                        UITheme.DrawBorder(roomRect,
                            new Color(UITheme.EnemyRed.r, UITheme.EnemyRed.g, UITheme.EnemyRed.b, 0.95f),
                            OutlineThickness);
                    else if (room.IsEntrance)
                        UITheme.DrawBorder(roomRect, ColEntranceOutline, OutlineThickness);

                    // Labels in full mode
                    if (fullMode && !room.IsCorridor)
                    {
                        string lbl = room.IsBoss ? "BOSS" : (room.Tier > 0 ? $"T{room.Tier}" : "");
                        if (!string.IsNullOrEmpty(lbl))
                            GUI.Label(roomRect, lbl,
                                UITheme.Label(UITheme.FontTiny, Color.white,
                                    TextAnchor.MiddleCenter, FontStyle.Bold));
                    }
                }
                else if (adjacentToVisited || room.IsBoss)
                {
                    // Small gray square with "?" — unexplored but revealed
                    Vector2 center = WorldToMinimap(room.WorldBounds.center, mapRect);
                    var smallRect = new Rect(center.x - UnexploredSize * 0.5f,
                                            center.y - UnexploredSize * 0.5f,
                                            UnexploredSize, UnexploredSize);
                    UITheme.DrawRect(smallRect, ColUnexplored);

                    if (room.IsBoss)
                        UITheme.DrawBorder(smallRect,
                            new Color(UITheme.EnemyRed.r, UITheme.EnemyRed.g, UITheme.EnemyRed.b, 0.95f),
                            OutlineThickness);

                    if (fullMode)
                        GUI.Label(smallRect, "?",
                            UITheme.Label(UITheme.FontTiny, Color.white,
                                TextAnchor.MiddleCenter, FontStyle.Bold));
                }
            }

            // ── 3. NPC markers ───────────────────────────────────────────────
            if (_playerTransform != null)
            {
                int currentRoom = _builder.GetRoomAtPosition(_playerTransform.position);

                // Refresh NPC cache when player changes room
                if (currentRoom != _lastNpcRoom)
                {
                    _npcCache    = FindObjectsByType<DungeonNPC>(FindObjectsSortMode.None);
                    _lastNpcRoom = currentRoom;
                }

                if (_npcCache != null)
                {
                    foreach (var npc in _npcCache)
                    {
                        if (npc == null) continue;
                        if (state == null || !state.HasVisited(npc.RoomIndex)) continue;

                        Vector2 npcPos = WorldToMinimap(npc.transform.position, mapRect);
                        bool isEnemy = npc.Role == DungeonNPCRole.AmbientEnemy;
                        Color npcColor = isEnemy ? UITheme.EnemyRed : UITheme.FriendlyBlue;

                        var dotRect = new Rect(npcPos.x - NPCDotRadius * 0.5f,
                                               npcPos.y - NPCDotRadius * 0.5f,
                                               NPCDotRadius, NPCDotRadius);
                        UITheme.DrawRect(dotRect, npcColor);
                    }
                }
            }

            // ── 4. Player dot ────────────────────────────────────────────────
            if (_playerTransform != null)
            {
                Vector2 playerPos = WorldToMinimap(_playerTransform.position, mapRect);
                var playerRect = new Rect(playerPos.x - PlayerDotRadius * 0.5f,
                                          playerPos.y - PlayerDotRadius * 0.5f,
                                          PlayerDotRadius, PlayerDotRadius);
                UITheme.DrawRect(playerRect, UITheme.XPGold);
            }
        }

        // ── Coordinate mapping ────────────────────────────────────────────────

        /// <summary>
        /// Linear remap from world AABB to a pixel position inside <paramref name="mapRect"/>.
        /// Flips Z → screen Y.
        /// </summary>
        private Vector2 WorldToMinimap(Vector3 worldPos, Rect mapRect)
        {
            float worldW = _worldMaxX - _worldMinX;
            float worldH = _worldMaxZ - _worldMinZ;

            if (worldW < 0.001f) worldW = 0.001f;
            if (worldH < 0.001f) worldH = 0.001f;

            float tx = (worldPos.x - _worldMinX) / worldW;
            float tz = (worldPos.z - _worldMinZ) / worldH;

            // Flip Z for screen Y (world +Z → screen top)
            float screenX = mapRect.x + tx * mapRect.width;
            float screenY = mapRect.y + (1f - tz) * mapRect.height;

            return new Vector2(screenX, screenY);
        }

        /// <summary>
        /// Converts 3D world <see cref="Bounds"/> to a 2D minimap <see cref="Rect"/>
        /// using the X/Z extent.
        /// </summary>
        private Rect WorldBoundsToMinimap(Bounds bounds, Rect mapRect)
        {
            Vector2 minPt = WorldToMinimap(new Vector3(bounds.min.x, 0, bounds.max.z), mapRect);
            Vector2 maxPt = WorldToMinimap(new Vector3(bounds.max.x, 0, bounds.min.z), mapRect);

            float rx = Mathf.Min(minPt.x, maxPt.x);
            float ry = Mathf.Min(minPt.y, maxPt.y);
            float rw = Mathf.Abs(maxPt.x - minPt.x);
            float rh = Mathf.Abs(maxPt.y - minPt.y);

            // Enforce a minimum pixel size so tiny corridors are still visible
            const float MinPx = 4f;
            if (rw < MinPx) { rx -= (MinPx - rw) * 0.5f; rw = MinPx; }
            if (rh < MinPx) { ry -= (MinPx - rh) * 0.5f; rh = MinPx; }

            return new Rect(rx, ry, rw, rh);
        }

        // ── IMGUI drawing helpers ─────────────────────────────────────────────

        /// <summary>
        /// Draws a line from <paramref name="a"/> to <paramref name="b"/> by rotating
        /// the GUI matrix around point <paramref name="a"/>, drawing a rectangle of the
        /// appropriate length, then restoring the matrix.
        /// </summary>
        private void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            float length = Vector2.Distance(a, b);
            if (length < 0.1f) return;

            float angle  = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var   pivot  = new Vector2(a.x, a.y + thickness * 0.5f);

            Matrix4x4 savedMatrix = GUI.matrix;

            // Rotate around the start point
            GUIUtility.RotateAroundPivot(angle, pivot);

            UITheme.DrawRect(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), color);

            GUI.matrix = savedMatrix;
        }
    }
}
