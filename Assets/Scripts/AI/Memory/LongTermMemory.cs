using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace ForeverEngine.AI.Memory
{
    [System.Serializable]
    public class LongTermData
    {
        public List<LTMEntry> entries = new();
        public List<RelationshipEntry> relationships = new();
        public List<Episode> persistentEvents = new();
    }

    [System.Serializable]
    public struct LTMEntry { public string key; public string value; }

    [System.Serializable]
    public struct RelationshipEntry { public string entityId; public string faction; public int value; }

    public class LongTermMemory
    {
        private Dictionary<string, string> _data = new();
        private Dictionary<string, int> _relationships = new();
        private List<Episode> _events = new();
        private int _maxEvents;

        public LongTermMemory(int maxEvents = 10000) => _maxEvents = maxEvents;

        public void Set(string key, object value) => _data[key] = value.ToString();
        public string Get(string key, string defaultValue = "") => _data.TryGetValue(key, out var v) ? v : defaultValue;
        public int GetInt(string key, int defaultValue = 0) => int.TryParse(Get(key), out int v) ? v : defaultValue;
        public float GetFloat(string key, float defaultValue = 0f) => float.TryParse(Get(key), out float v) ? v : defaultValue;
        public bool Has(string key) => _data.ContainsKey(key);

        public void AdjustRelationship(string entityId, int delta)
        {
            _relationships.TryGetValue(entityId, out int current);
            _relationships[entityId] = Mathf.Clamp(current + delta, -100, 100);
        }

        public int GetRelationship(string entityId) => _relationships.TryGetValue(entityId, out int v) ? v : 0;

        public void RecordPersistentEvent(Episode e)
        {
            _events.Add(e);
            if (_events.Count > _maxEvents)
                _events.RemoveRange(0, _events.Count - _maxEvents * 3 / 4);
        }

        public List<Episode> GetHistory(int limit = 50) => _events.GetRange(Mathf.Max(0, _events.Count - limit), Mathf.Min(limit, _events.Count));

        public void SaveToFile(string path)
        {
            var data = new LongTermData();
            foreach (var kv in _data) data.entries.Add(new LTMEntry { key = kv.Key, value = kv.Value });
            foreach (var kv in _relationships) data.relationships.Add(new RelationshipEntry { entityId = kv.Key, value = kv.Value });
            data.persistentEvents = new List<Episode>(_events);
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        public void LoadFromFile(string path)
        {
            if (!File.Exists(path)) return;
            var data = JsonUtility.FromJson<LongTermData>(File.ReadAllText(path));
            _data.Clear(); foreach (var e in data.entries) _data[e.key] = e.value;
            _relationships.Clear(); foreach (var r in data.relationships) _relationships[r.entityId] = r.value;
            _events = data.persistentEvents ?? new List<Episode>();
        }

        public void Clear() { _data.Clear(); _relationships.Clear(); _events.Clear(); }
    }
}
