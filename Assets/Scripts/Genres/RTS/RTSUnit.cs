using UnityEngine;

namespace ForeverEngine.Genres.RTS
{
    [CreateAssetMenu(menuName = "Forever Engine/RTS/Unit Data")]
    public class UnitData : ScriptableObject
    {
        public string UnitName; public float MaxHP = 100; public float Damage = 10; public float AttackSpeed = 1f;
        public float Speed = 5f; public float Range = 2f; public float SightRange = 10f;
        public enum ArmorType { Light, Medium, Heavy } public ArmorType Armor;
        public float BuildTime = 5f; public int GoldCost = 50; public int SupplyCost = 1;
    }

    public class RTSUnit : MonoBehaviour
    {
        [SerializeField] private UnitData _data;
        public int TeamId;
        public float HP { get; private set; }
        public bool IsAlive => HP > 0;
        public UnitData Data => _data;

        private void Start() { HP = _data != null ? _data.MaxHP : 100; }

        public void TakeDamage(float amount) { HP = Mathf.Max(0, HP - amount); if (HP <= 0) Die(); }
        public void Heal(float amount) { HP = Mathf.Min(_data?.MaxHP ?? 100, HP + amount); }

        public void MoveTo(Vector3 target) => transform.position = Vector3.MoveTowards(transform.position, target, (_data?.Speed ?? 5f) * Time.deltaTime);
        public bool InRange(Vector3 target) => Vector3.Distance(transform.position, target) <= (_data?.Range ?? 2f);
        public bool CanSee(Vector3 target) => Vector3.Distance(transform.position, target) <= (_data?.SightRange ?? 10f);

        private void Die() { gameObject.SetActive(false); }
    }
}
