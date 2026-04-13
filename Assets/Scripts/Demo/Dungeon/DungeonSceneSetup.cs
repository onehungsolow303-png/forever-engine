using UnityEngine;
using DA = DungeonArchitect;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Bootstrap for the DungeonExploration scene.
    /// Instantiates the Dungeon Architect Snap builder prefab,
    /// builds the dungeon, then initializes DungeonExplorer.
    /// </summary>
    public class DungeonSceneSetup : UnityEngine.MonoBehaviour
    {
        [SerializeField] private GameObject _dungeonPrefab;

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

            // Load the Lordenfel DA Snap prefab
            if (_dungeonPrefab == null)
                _dungeonPrefab = Resources.Load<GameObject>("DungeonSnap");
            if (_dungeonPrefab == null)
            {
                // Try loading directly from Lordenfel path
                _dungeonPrefab = Resources.Load<GameObject>(
                    "Lordenfel/FPD_DungeonSnap_01");
            }
            if (_dungeonPrefab == null)
            {
                Debug.LogError("DungeonSceneSetup: No dungeon prefab found. " +
                    "Assign in Inspector or place at Resources/DungeonSnap");
                gm.ReturnToOverworld();
                return;
            }

            // Instantiate DA dungeon
            var dungeonObj = Instantiate(_dungeonPrefab);
            dungeonObj.name = "DungeonArchitect";

            // Add our event listener
            var builder = dungeonObj.AddComponent<DADungeonBuilder>();

            // Ensure SnapQuery is present
            var query = dungeonObj.GetComponent<DungeonArchitect.Builders.Snap.SnapQuery>();
            if (query == null)
                query = dungeonObj.AddComponent<DungeonArchitect.Builders.Snap.SnapQuery>();

            // Set seed and build
            var dungeon = dungeonObj.GetComponent<DA.Dungeon>();
            if (dungeon == null)
            {
                Debug.LogError("DungeonSceneSetup: Prefab has no Dungeon component");
                gm.ReturnToOverworld();
                return;
            }

            int seed = gm.CurrentSeed + locationId.GetHashCode();
            dungeon.Config.Seed = (uint)Mathf.Abs(seed);
            dungeon.Build();

            // Spawn NPCs in dungeon rooms
            var npcConfig = Resources.Load<DungeonNPCConfig>("DungeonNPCConfig");
            if (npcConfig != null)
                DungeonNPCSpawner.SpawnNPCs(builder, npcConfig, seed);

            // Create explorer and initialize with DA builder
            var explorerObj = new GameObject("DungeonExplorer");
            var explorer = explorerObj.AddComponent<DungeonExplorer>();
            explorer.InitializeWithDA(locationId, builder);

            // Handle return from battle
            if (gm.LastBattleWon && gm.PendingDungeonState != null)
                explorer.OnBattleWon(gm.PendingEncounterId);

            gm.LastBattleWon = false;
            gm.PendingEncounterId = null;
        }
    }
}
