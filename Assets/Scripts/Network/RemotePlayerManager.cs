using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Network
{
    /// <summary>
    /// Spawns/despawns RemotePlayerViews in response to snapshot arrivals.
    /// Ingest is driven by ConnectionManager.OnPlayerUpdate pushing into
    /// ServerStateCache.PlayerSnapshots; this class just materializes one
    /// capsule per remote id and removes it after StaleSeconds of silence.
    /// </summary>
    public class RemotePlayerManager : UnityEngine.MonoBehaviour
    {
        private const double StaleSeconds = 5.0;
        private readonly Dictionary<string, RemotePlayerView> _views = new Dictionary<string, RemotePlayerView>();
        private readonly List<string> _staleScratch = new List<string>();

        private void Update()
        {
            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            double now = Time.timeAsDouble;
            string localId = cache.LocalPlayerId;

            foreach (var kvp in cache.PlayerLastArrivalClientTime)
            {
                string id = kvp.Key;
                if (id == localId) continue;
                if (_views.ContainsKey(id)) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"RemotePlayer_{id}";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard"));
                    // Bright emissive green — no natural biome prop shares this
                    // color, so remote players are unmistakable across deserts,
                    // forests, tundra alike. Emission also catches the eye in
                    // overcast/ambient-lit scenes where base color alone would
                    // blend with terrain.
                    mat.color = new Color(0.1f, 1f, 0.3f);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", new Color(0.0f, 0.5f, 0.1f));
                    }
                    r.material = mat;
                }
                var view = go.AddComponent<RemotePlayerView>();
                view.PlayerId = id;
                _views[id] = view;
            }

            // Despawn stale views (and drop their cache entries).
            _staleScratch.Clear();
            foreach (var kvp in _views)
            {
                if (!cache.PlayerLastArrivalClientTime.TryGetValue(kvp.Key, out var last) ||
                    now - last > StaleSeconds)
                {
                    _staleScratch.Add(kvp.Key);
                }
            }
            for (int i = 0; i < _staleScratch.Count; i++)
            {
                var id = _staleScratch[i];
                if (_views[id] != null) Destroy(_views[id].gameObject);
                _views.Remove(id);
                cache.PlayerLastArrivalClientTime.Remove(id);
                cache.PlayerSnapshots.Remove(id);
            }
        }
    }
}
