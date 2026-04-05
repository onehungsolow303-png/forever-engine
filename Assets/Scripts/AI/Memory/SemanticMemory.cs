using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.AI.Memory
{
    [System.Serializable]
    public struct Fact { public string subject; public string predicate; public string obj; }

    [System.Serializable]
    public struct FactionRelation { public string factionA; public string factionB; public int relation; }

    [CreateAssetMenu(menuName = "Forever Engine/Knowledge Graph")]
    public class SemanticMemory : ScriptableObject
    {
        public List<Fact> facts = new();
        public List<FactionRelation> factionRelations = new();

        private Dictionary<string, List<Fact>> _index;

        public bool AreFactionsHostile(string a, string b)
        {
            foreach (var r in factionRelations)
                if ((r.factionA == a && r.factionB == b) || (r.factionA == b && r.factionB == a))
                    return r.relation < -50;
            return false;
        }

        public List<string> GetWeaknesses(string entityType)
        {
            var result = new List<string>();
            foreach (var f in facts)
                if (f.subject == entityType && f.predicate == "weak_to") result.Add(f.obj);
            return result;
        }

        public List<string> QueryFacts(string subject, string predicate = null)
        {
            var result = new List<string>();
            foreach (var f in facts)
                if (f.subject == subject && (predicate == null || f.predicate == predicate)) result.Add(f.obj);
            return result;
        }
    }
}
