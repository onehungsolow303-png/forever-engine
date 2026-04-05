using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.AI.Memory
{
    public struct MemoryEvent
    {
        public float Timestamp;
        public string EventType;
        public Vector3 Position;
        public string ActorId;
        public string Data;
    }

    public class ShortTermMemory
    {
        private List<MemoryEvent> _events = new();
        private float _decaySeconds;

        public ShortTermMemory(float decaySeconds = 10f) => _decaySeconds = decaySeconds;

        public void RecordEvent(string eventType, Vector3 position, string actorId = null, string data = null)
        {
            _events.Add(new MemoryEvent { Timestamp = Time.time, EventType = eventType, Position = position, ActorId = actorId, Data = data });
        }

        public List<MemoryEvent> GetEventsWithin(float seconds)
        {
            float cutoff = Time.time - seconds;
            return _events.FindAll(e => e.Timestamp >= cutoff);
        }

        public List<MemoryEvent> GetEventsByType(string eventType)
        {
            return _events.FindAll(e => e.EventType == eventType);
        }

        public void Decay()
        {
            float cutoff = Time.time - _decaySeconds;
            _events.RemoveAll(e => e.Timestamp < cutoff);
        }

        public void Clear() => _events.Clear();
        public int Count => _events.Count;
    }
}
