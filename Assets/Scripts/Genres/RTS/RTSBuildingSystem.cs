using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.RTS
{
    [CreateAssetMenu(menuName = "Forever Engine/RTS/Building Data")]
    public class BuildingData : ScriptableObject
    {
        public string BuildingName; public float MaxHP = 500; public int GoldCost = 200;
        public float BuildTime = 10f; public Vector2Int Size = new(2, 2);
        public List<UnitData> CanProduce = new(); public List<string> TechUnlocks = new();
    }

    public class RTSBuildingSystem : MonoBehaviour
    {
        private List<RTSBuilding> _buildings = new();

        public RTSBuilding PlaceBuilding(BuildingData data, Vector3 position, int teamId)
        {
            var go = new GameObject(data.BuildingName);
            go.transform.position = position;
            var building = go.AddComponent<RTSBuilding>();
            building.Initialize(data, teamId);
            _buildings.Add(building);
            return building;
        }

        public List<RTSBuilding> GetBuildings(int teamId) => _buildings.FindAll(b => b.TeamId == teamId && b.IsAlive);
    }

    public class RTSBuilding : MonoBehaviour
    {
        public BuildingData Data { get; private set; }
        public int TeamId { get; private set; }
        public float HP { get; private set; }
        public bool IsAlive => HP > 0;
        public float BuildProgress { get; private set; }
        public bool IsBuilt => BuildProgress >= 1f;

        private Queue<UnitData> _productionQueue = new();
        private float _productionTimer;

        public void Initialize(BuildingData data, int teamId) { Data = data; TeamId = teamId; HP = data.MaxHP; BuildProgress = 0f; }

        private void Update()
        {
            if (!IsBuilt) { BuildProgress = Mathf.Min(1f, BuildProgress + Time.deltaTime / (Data?.BuildTime ?? 10f)); return; }
            if (_productionQueue.Count > 0)
            {
                _productionTimer += Time.deltaTime;
                if (_productionTimer >= _productionQueue.Peek().BuildTime)
                { _productionQueue.Dequeue(); _productionTimer = 0f; /* Spawn unit */ }
            }
        }

        public void QueueUnit(UnitData unit) { if (IsBuilt && Data.CanProduce.Contains(unit)) _productionQueue.Enqueue(unit); }
        public void TakeDamage(float amount) { HP = Mathf.Max(0, HP - amount); if (HP <= 0) gameObject.SetActive(false); }
        public int QueueLength => _productionQueue.Count;
    }
}
