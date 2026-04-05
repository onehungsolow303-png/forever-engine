using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.Adventure
{
    public class CollectibleTracker : MonoBehaviour
    {
        public static CollectibleTracker Instance { get; private set; }
        private Dictionary<string, HashSet<string>> _collected = new();
        private Dictionary<string, int> _totals = new();

        private void Awake() => Instance = this;

        public void RegisterCategory(string category, int total)
        {
            _totals[category] = total;
            if (!_collected.ContainsKey(category)) _collected[category] = new HashSet<string>();
        }

        public bool Collect(string category, string itemId)
        {
            if (!_collected.ContainsKey(category)) _collected[category] = new HashSet<string>();
            bool isNew = _collected[category].Add(itemId);
            if (isNew) OnCollected?.Invoke(category, itemId);
            return isNew;
        }

        public int GetCount(string category) => _collected.TryGetValue(category, out var s) ? s.Count : 0;
        public int GetTotal(string category) => _totals.TryGetValue(category, out int t) ? t : 0;
        public float GetPercent(string category) { int total = GetTotal(category); return total > 0 ? (float)GetCount(category) / total : 0f; }
        public bool IsComplete(string category) => GetCount(category) >= GetTotal(category);
        public bool HasCollected(string category, string itemId) => _collected.TryGetValue(category, out var s) && s.Contains(itemId);

        public event System.Action<string, string> OnCollected;
    }
}
