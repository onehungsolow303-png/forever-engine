using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Bootstrap MonoBehaviour for the DungeonExploration scene.
    /// Attach to a GameObject in the scene. Reads GameManager state,
    /// loads the RoomCatalog, and initialises DungeonExplorer.
    ///
    /// If returning from a won battle inside the dungeon, also calls
    /// DungeonExplorer.OnBattleWon() to check for boss defeat.
    /// </summary>
    public class DungeonSceneSetup : UnityEngine.MonoBehaviour
    {
        [SerializeField] private RoomCatalog _roomCatalog;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("DungeonSceneSetup: No GameManager"); return; }

            string locationId = gm.PendingLocationId;
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogError("DungeonSceneSetup: No PendingLocationId");
                gm.ReturnToOverworld();
                return;
            }

            if (_roomCatalog == null)
                _roomCatalog = Resources.Load<RoomCatalog>("RoomCatalog");
            if (_roomCatalog == null)
            {
                Debug.LogError("DungeonSceneSetup: No RoomCatalog — assign one in the Inspector " +
                               "or place a RoomCatalog asset at Resources/RoomCatalog");
                gm.ReturnToOverworld();
                return;
            }

            var explorerObj = new GameObject("DungeonExplorer");
            var explorer    = explorerObj.AddComponent<DungeonExplorer>();
            explorer.Initialize(locationId, _roomCatalog);

            if (gm.LastBattleWon && gm.PendingDungeonState != null)
                explorer.OnBattleWon(gm.PendingEncounterId);

            gm.LastBattleWon      = false;
            gm.PendingEncounterId = null;
        }
    }
}
