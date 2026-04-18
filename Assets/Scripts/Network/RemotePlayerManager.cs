using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Network
{
    /// <summary>
    /// Spawns/despawns RemotePlayerViews as remote poses arrive and go stale.
    /// Polls ServerStateCache.RemotePlayerPoses each frame.
    /// </summary>
    public class RemotePlayerManager : MonoBehaviour
    {
        private const double StaleSeconds = 5.0;
        private readonly Dictionary<string, RemotePlayerView> _views = new Dictionary<string, RemotePlayerView>();

        private void Update()
        {
            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            double now = Time.timeAsDouble;

            // Ingest fresh poses
            foreach (var kvp in cache.RemotePlayerPoses)
            {
                string id = kvp.Key;
                var (pos, yaw, receivedAt) = kvp.Value;
                if (!_views.TryGetValue(id, out var view))
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = $"RemotePlayer_{id}";
                    // Remove collider — remote players are visual-only
                    var col = go.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    // Tint so local/remote are distinguishable
                    var r = go.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard"));
                        mat.color = new Color(0.9f, 0.4f, 0.3f);
                        r.material = mat;
                    }
                    view = go.AddComponent<RemotePlayerView>();
                    view.PlayerId = id;
                    _views[id] = view;
                }
                view.PushPose(pos, yaw, receivedAt);
            }

            // Despawn stale entries — both from the view-set and the cache dict.
            var toRemove = new List<string>();
            foreach (var kvp in _views)
            {
                if (!cache.RemotePlayerPoses.TryGetValue(kvp.Key, out var p) ||
                    now - p.receivedAt > StaleSeconds)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
            {
                if (_views[id] != null) Destroy(_views[id].gameObject);
                _views.Remove(id);
                cache.RemotePlayerPoses.Remove(id);
            }
        }
    }
}
