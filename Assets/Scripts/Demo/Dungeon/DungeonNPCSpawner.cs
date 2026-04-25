using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ForeverEngine.Demo.Battle;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Static spawner that populates a freshly-built dungeon with friendly NPCs
    /// and ambient enemy patrols, driven by <see cref="DungeonNPCConfig"/> rules
    /// and a deterministic seed so layouts are reproducible.
    ///
    /// Called once after <see cref="DADungeonBuilder.OnPostDungeonBuild"/> completes.
    /// </summary>
    public static class DungeonNPCSpawner
    {
        // ── Name pools ────────────────────────────────────────────────────────

        private static readonly string[] MerchantNames =
            { "Grimweld", "Nessa", "Old Korbin", "Mira" };

        private static readonly string[] PrisonerNames =
            { "Captive Soldier", "Lost Explorer", "Chained Scholar" };

        private static readonly string[] QuestGiverNames =
            { "Whispering Shade", "Dying Knight", "Cursed Hermit" };

        // ── Role colours (URP / Standard fallback) ────────────────────────────

        private static readonly Color MerchantColor   = Color.green;
        private static readonly Color PrisonerColor   = Color.yellow;
        private static readonly Color QuestGiverColor = Color.blue;
        private static readonly Color EnemyColor      = Color.red;

        // ── Floor-snap raycast settings ───────────────────────────────────────

        private const float RaycastOriginOffset = 10f;
        private const float RaycastMaxDistance  = 20f;

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Spawns friendly NPCs and ambient enemies across <paramref name="builder"/>'s
        /// rooms according to <paramref name="config"/> rules, using <paramref name="seed"/>
        /// for reproducibility.
        /// </summary>
        /// <returns>
        /// Every <see cref="DungeonNPC"/> component attached to spawned GameObjects.
        /// </returns>
        public static List<DungeonNPC> SpawnNPCs(
            DADungeonBuilder builder,
            DungeonNPCConfig config,
            int seed)
        {
            var rng      = new System.Random(seed);
            var spawned  = new List<DungeonNPC>();

            // Per-rule spawn counters (friendly rules only)
            var friendlyCounts = new int[config.FriendlyRules.Length];

            if (builder.Rooms == null || builder.Rooms.Length == 0)
            {
                Debug.LogWarning("[DungeonNPCSpawner] Builder has no rooms — nothing spawned.");
                return spawned;
            }

            foreach (var room in builder.Rooms)
            {
                // Skip rooms that shouldn't host NPCs
                if (room.IsEntrance || room.IsBoss || room.IsCorridor) continue;

                SpawnFriendlyNPCs(room, config, rng, friendlyCounts, spawned);
                SpawnAmbientEnemies(room, config, rng, spawned);
            }

            // Notify Director Hub after all placements
            if (config.DirectorOverrides)
                FireDirectorManifest(spawned);

            Debug.Log($"[DungeonNPCSpawner] Spawned {spawned.Count} NPCs (seed {seed}).");
            return spawned;
        }

        // ── Friendly NPC placement ────────────────────────────────────────────

        private static void SpawnFriendlyNPCs(
            in DADungeonBuilder.RoomInfo room,
            DungeonNPCConfig config,
            System.Random rng,
            int[] friendlyCounts,
            List<DungeonNPC> spawned)
        {
            for (int ri = 0; ri < config.FriendlyRules.Length; ri++)
            {
                var rule = config.FriendlyRules[ri];

                if (!TierMatches(room.Tier, rule.TierFilter)) continue;
                if (friendlyCounts[ri] >= rule.MaxPerDungeon)  continue;
                if (rng.NextDouble() > rule.SpawnChance)        continue;

                // Pick a model key at random from the rule's list
                string modelKey = rule.ModelKeys.Length > 0
                    ? rule.ModelKeys[rng.Next(0, rule.ModelKeys.Length)]
                    : null;

                var go = InstantiateNPC(modelKey, FallbackColorForRole(rule.Role),
                                        room, rng);
                if (go == null) continue;

                var npc          = go.AddComponent<DungeonNPC>();
                npc.Role         = rule.Role;
                npc.NPCName      = PickName(rule.Role, rng);
                npc.RoomIndex    = room.Index;
                npc.PatrolSpeed  = 0f; // Friendly NPCs don't patrol

                go.name = $"NPC_{rule.Role}_{room.Index}_{friendlyCounts[ri]}";
                friendlyCounts[ri]++;
                spawned.Add(npc);
            }
        }

        // ── Ambient enemy placement ───────────────────────────────────────────

        private static void SpawnAmbientEnemies(
            in DADungeonBuilder.RoomInfo room,
            DungeonNPCConfig config,
            System.Random rng,
            List<DungeonNPC> spawned)
        {
            foreach (var rule in config.EnemyRules)
            {
                if (!TierMatches(room.Tier, rule.TierFilter))    continue;
                if (rule.EnemyNames == null || rule.EnemyNames.Length == 0) continue;

                int count = rng.Next(rule.CountRange.x, rule.CountRange.y + 1);

                for (int i = 0; i < count; i++)
                {
                    string enemyName = rule.EnemyNames[rng.Next(0, rule.EnemyNames.Length)];
                    var go = InstantiateNPC(enemyName, EnemyColor, room, rng);
                    if (go == null) continue;

                    var npc         = go.AddComponent<DungeonNPC>();
                    npc.Role        = DungeonNPCRole.AmbientEnemy;
                    npc.NPCName     = enemyName;
                    npc.RoomIndex   = room.Index;
                    npc.PatrolSpeed = 2f;

                    // Patrol waypoints: room centre ± patrolRadius on a random horizontal axis
                    SetPatrolWaypoints(npc, room, rule.PatrolRadius, rng);

                    go.name = $"Enemy_{enemyName.Replace(' ', '_')}_{room.Index}_{i}";
                    spawned.Add(npc);
                }
            }
        }

        // ── Instantiation helpers ─────────────────────────────────────────────

        /// <summary>
        /// Tries to load a model via <see cref="ModelRegistry"/>.
        /// Falls back to a coloured capsule primitive when no model exists.
        /// Returns null only on a hard failure (no room parent etc.).
        /// </summary>
        private static GameObject InstantiateNPC(
            string modelKey,
            Color fallbackColor,
            in DADungeonBuilder.RoomInfo room,
            System.Random rng)
        {
            GameObject go = null;

            // Attempt model registry lookup
            if (!string.IsNullOrEmpty(modelKey))
            {
                var (path, scale) = ModelRegistry.Resolve(modelKey);
                if (!string.IsNullOrEmpty(path))
                {
                    var prefab = Resources.Load<GameObject>(path);
                    if (prefab != null)
                    {
                        go = Object.Instantiate(prefab);
                        go.transform.localScale = Vector3.one * scale;
                    }
                }
            }

            // Capsule fallback
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);

                // Remove the collider — spawned primitives must not block physics
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                ApplyFallbackMaterial(go, fallbackColor);
            }

            // Parent to room object so Unity can cull inactive rooms cheaply
            if (room.RoomObject != null)
                go.transform.SetParent(room.RoomObject.transform, worldPositionStays: true);

            // Place at a random point in the inner 60 % of the room bounds
            go.transform.position = RandomPointInRoom(room, rng);

            return go;
        }

        /// <summary>
        /// Applies a URP Lit material with <paramref name="color"/> as the base
        /// colour, falling back to Standard if the URP shader isn't present.
        /// </summary>
        private static void ApplyFallbackMaterial(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            if (mat == null) return;

            // URP uses _BaseColor; Standard uses _Color
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            renderer.sharedMaterial = mat;
        }

        // ── Patrol waypoint setup ─────────────────────────────────────────────

        private static void SetPatrolWaypoints(
            DungeonNPC npc,
            in DADungeonBuilder.RoomInfo room,
            float patrolRadius,
            System.Random rng)
        {
            Vector3 center = room.WorldBounds.center;

            // Choose a random horizontal axis (X or Z)
            bool useX  = rng.NextDouble() >= 0.5;
            Vector3 axis = useX ? Vector3.right : Vector3.forward;

            Vector3 posA = center + axis * patrolRadius;
            Vector3 posB = center - axis * patrolRadius;

            posA.y = SnapToFloor(posA, room);
            posB.y = SnapToFloor(posB, room);

            var waypointA = new GameObject($"{npc.gameObject.name}_WP_A");
            var waypointB = new GameObject($"{npc.gameObject.name}_WP_B");

            if (room.RoomObject != null)
            {
                waypointA.transform.SetParent(room.RoomObject.transform, worldPositionStays: true);
                waypointB.transform.SetParent(room.RoomObject.transform, worldPositionStays: true);
            }

            waypointA.transform.position = posA;
            waypointB.transform.position = posB;

            npc.WaypointA = waypointA.transform;
            npc.WaypointB = waypointB.transform;
        }

        // ── Positioning helpers ───────────────────────────────────────────────

        /// <summary>
        /// Returns a random point within the inner 60 % of <paramref name="room"/>'s
        /// bounds, snapped to the floor via a downward raycast.
        /// </summary>
        private static Vector3 RandomPointInRoom(
            in DADungeonBuilder.RoomInfo room,
            System.Random rng)
        {
            Bounds b = room.WorldBounds;

            // Shrink to inner 60 %
            Vector3 innerSize = b.size * 0.6f;
            float x = (float)(b.center.x + (rng.NextDouble() - 0.5) * innerSize.x);
            float z = (float)(b.center.z + (rng.NextDouble() - 0.5) * innerSize.z);
            float y = SnapToFloor(new Vector3(x, b.center.y, z), room);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Casts a ray downward from <paramref name="worldPos"/> and returns the
        /// floor Y. Falls back to the room bounds min-Y if nothing is hit.
        /// </summary>
        private static float SnapToFloor(
            Vector3 worldPos,
            in DADungeonBuilder.RoomInfo room)
        {
            Vector3 origin = new Vector3(worldPos.x,
                                         worldPos.y + RaycastOriginOffset,
                                         worldPos.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RaycastMaxDistance))
                return hit.point.y;

            // Fallback: bottom face of room bounds
            return room.WorldBounds.min.y;
        }

        // ── Director Hub notification ─────────────────────────────────────────

        /// <summary>
        /// Fires a fire-and-forget event summarising the placement manifest.
        /// </summary>
        private static void FireDirectorManifest(List<DungeonNPC> npcs)
        {
            if (npcs.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append('[');

            for (int i = 0; i < npcs.Count; i++)
            {
                var n = npcs[i];
                if (i > 0) sb.Append(',');
                sb.Append('{')
                  .Append($"\"name\":\"{n.NPCName}\"")
                  .Append($",\"role\":\"{n.Role}\"")
                  .Append($",\"room\":{n.RoomIndex}")
                  .Append('}');
            }

            sb.Append(']');

            // TODO: notify Director Hub of NPC placement via server-side bridge.
            // Was DirectorEvents.Send("dungeon_npcs_placed: " + sb) — no-op since Spec 3B.
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private static bool TierMatches(int roomTier, int[] filter)
        {
            if (filter == null || filter.Length == 0) return true;
            foreach (int t in filter)
                if (t == roomTier) return true;
            return false;
        }

        private static Color FallbackColorForRole(DungeonNPCRole role) => role switch
        {
            DungeonNPCRole.Merchant   => MerchantColor,
            DungeonNPCRole.Prisoner   => PrisonerColor,
            DungeonNPCRole.QuestGiver => QuestGiverColor,
            _                         => Color.white,
        };

        private static string PickName(DungeonNPCRole role, System.Random rng) => role switch
        {
            DungeonNPCRole.Merchant   => MerchantNames[rng.Next(0, MerchantNames.Length)],
            DungeonNPCRole.Prisoner   => PrisonerNames[rng.Next(0, PrisonerNames.Length)],
            DungeonNPCRole.QuestGiver => QuestGiverNames[rng.Next(0, QuestGiverNames.Length)],
            _                         => "Unknown",
        };
    }
}
