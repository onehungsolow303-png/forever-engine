using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ForeverEngine.AI.Memory
{
    [System.Serializable]
    public struct Episode
    {
        public float Timestamp;
        public string Actor;
        public string Action;
        public string Target;
        public Vector3 Location;
        public string Outcome;
        public float Importance;
    }

    public class EpisodicMemory
    {
        private List<Episode> _episodes = new();
        private int _maxEpisodes;

        public EpisodicMemory(int maxEpisodes = 500) => _maxEpisodes = maxEpisodes;

        public void Record(Episode e)
        {
            if (e.Importance == 0) e.Importance = 0.5f;
            _episodes.Add(e);
            if (_episodes.Count > _maxEpisodes) Prune();
        }

        public IEnumerable<Episode> Query(string actor = null, string action = null, string target = null, float withinSeconds = -1)
        {
            float cutoff = withinSeconds > 0 ? Time.time - withinSeconds : 0;
            return _episodes.Where(e =>
                (actor == null || e.Actor == actor) &&
                (action == null || e.Action == action) &&
                (target == null || e.Target == target) &&
                (withinSeconds < 0 || e.Timestamp >= cutoff));
        }

        public List<Episode> GetMostImportant(int count) =>
            _episodes.OrderByDescending(e => e.Importance).Take(count).ToList();

        public int Count => _episodes.Count;
        public void Clear() => _episodes.Clear();

        private void Prune()
        {
            _episodes = _episodes.OrderByDescending(e => e.Importance).Take(_maxEpisodes * 3 / 4).ToList();
        }

        public List<Episode> GetAll() => new(_episodes);
    }
}
