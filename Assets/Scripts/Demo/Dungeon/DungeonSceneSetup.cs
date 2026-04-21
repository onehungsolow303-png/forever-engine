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

            // Spec 7 Phase 3 Task 7: seed comes from DungeonEnteredMessage (server-authoritative).
            // Fallback to a random seed if scene is loaded directly (editor/solo testing).
            int seed = GameManager.PendingDungeonSeed != 0
                ? GameManager.PendingDungeonSeed
                : UnityEngine.Random.Range(1, int.MaxValue);
            string template = GameManager.PendingDungeonTemplate ?? "debug_small";

            // Consume the pending seed so a subsequent direct scene load doesn't re-use it.
            GameManager.PendingDungeonSeed = 0;
            // PendingDungeonTemplate intentionally kept — it's just a string and defaults cleanly.

            // PendingLocationId: derive from template name for DungeonState keying when
            // arriving via server path (no PendingLocationId set by old EnterDungeon()).
            string locationId = !string.IsNullOrEmpty(gm.PendingLocationId)
                ? gm.PendingLocationId
                : template;

            Debug.Log($"[DungeonSceneSetup] seed={seed} template={template} locationId={locationId}");

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
                ForeverEngine.Network.ConnectionManager.Instance?.Send(
                    new ForeverEngine.Core.Messages.ExitDungeonRequest());
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

            // Set seed and build — DA.Config.Seed is the authoritative seed path for
            // Snap builder. All party members receive the same seed from the server,
            // so Config.Seed guarantees an identical layout on every client.
            var dungeon = dungeonObj.GetComponent<DA.Dungeon>();
            if (dungeon == null)
            {
                Debug.LogError("DungeonSceneSetup: Prefab has no Dungeon component");
                ForeverEngine.Network.ConnectionManager.Instance?.Send(
                    new ForeverEngine.Core.Messages.ExitDungeonRequest());
                return;
            }

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
