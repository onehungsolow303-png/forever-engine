using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.Sandbox
{
    public class BuildingSystem : UnityEngine.MonoBehaviour
    {
        public static BuildingSystem Instance { get; private set; }
        [SerializeField] private bool _gridSnap = true;
        [SerializeField] private float _gridSize = 1f;

        private List<PlacedStructure> _structures = new();

        private void Awake() => Instance = this;

        public bool Place(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (_gridSnap) position = SnapToGrid(position);
            if (IsOccupied(position)) return false;
            var go = Instantiate(prefab, position, rotation);
            _structures.Add(new PlacedStructure { Position = position, Object = go, PrefabName = prefab.name });
            return true;
        }

        public bool Remove(Vector3 position)
        {
            for (int i = 0; i < _structures.Count; i++)
                if (Vector3.Distance(_structures[i].Position, SnapToGrid(position)) < _gridSize * 0.5f)
                { Destroy(_structures[i].Object); _structures.RemoveAt(i); return true; }
            return false;
        }

        public bool IsOccupied(Vector3 position)
        {
            var snapped = SnapToGrid(position);
            return _structures.Exists(s => Vector3.Distance(s.Position, snapped) < _gridSize * 0.5f);
        }

        private Vector3 SnapToGrid(Vector3 pos) => new Vector3(Mathf.Round(pos.x / _gridSize) * _gridSize, pos.y, Mathf.Round(pos.z / _gridSize) * _gridSize);

        public int StructureCount => _structures.Count;
        public List<PlacedStructure> GetAll() => new(_structures);
    }

    [System.Serializable]
    public struct PlacedStructure { public Vector3 Position; public GameObject Object; public string PrefabName; }
}
