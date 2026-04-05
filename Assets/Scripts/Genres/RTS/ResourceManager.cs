using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.RTS
{
    public enum ResourceType { Gold, Wood, Food, Stone, Supply }

    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }
        private Dictionary<int, Dictionary<ResourceType, int>> _teamResources = new();

        private void Awake() => Instance = this;

        public void InitTeam(int teamId, int gold = 500, int wood = 200, int food = 100)
        {
            _teamResources[teamId] = new Dictionary<ResourceType, int>
            { [ResourceType.Gold] = gold, [ResourceType.Wood] = wood, [ResourceType.Food] = food, [ResourceType.Stone] = 0, [ResourceType.Supply] = 0 };
        }

        public int GetResource(int teamId, ResourceType type) => _teamResources.TryGetValue(teamId, out var r) && r.TryGetValue(type, out int v) ? v : 0;

        public void AddResource(int teamId, ResourceType type, int amount)
        {
            if (!_teamResources.ContainsKey(teamId)) InitTeam(teamId);
            _teamResources[teamId].TryGetValue(type, out int current);
            _teamResources[teamId][type] = current + amount;
        }

        public bool CanAfford(int teamId, ResourceType type, int amount) => GetResource(teamId, type) >= amount;

        public bool Spend(int teamId, ResourceType type, int amount)
        {
            if (!CanAfford(teamId, type, amount)) return false;
            _teamResources[teamId][type] -= amount;
            return true;
        }
    }
}
