using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    [RequireComponent(typeof(BoxCollider))]
    public class EncounterZone : UnityEngine.MonoBehaviour
    {
        public string EncounterId;
        public int ZoneIndex;
        public bool IsBoss;
        public int Tier;
        private bool _triggered;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var gm = GameManager.Instance;
            if (gm?.PendingDungeonState != null)
                _triggered = gm.PendingDungeonState.TriggeredEncounters.Contains(ZoneIndex);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag("Player")) return;
            _triggered = true;
            var explorer = FindFirstObjectByType<DungeonExplorer>();
            if (explorer != null)
                explorer.EnterBattle(EncounterId, ZoneIndex, IsBoss);
        }
    }
}
