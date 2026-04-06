using UnityEngine;
using Unity.Entities;
using ForeverEngine.Demo;
using ForeverEngine.ECS.Data;
using ForeverEngine.MonoBehaviour.Rendering;
using ForeverEngine.MonoBehaviour.Input;
using ForeverEngine.MonoBehaviour.UI;
using ForeverEngine.MonoBehaviour.Camera;

namespace ForeverEngine.MonoBehaviour.Bootstrap
{
    public class GameBootstrap : UnityEngine.MonoBehaviour
    {
        [Header("Map Loading")]
        [Tooltip("Path to map_data.json from Map Generator output")]
        public string MapDataPath;

        [Header("References")]
        public GameConfig GameConfig;
        public CameraController CameraController;
        public TileRenderer TileRenderer;
        public EntityRenderer EntityRenderer;
        public FogRenderer FogRenderer;
        public HUDManager HUDManager;
        public CombatLogUI CombatLogUI;

        private EntityManager _entityManager;
        private MapDataStore _mapDataStore;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            string pendingPath = GameManager.Instance?.PendingMapDataPath;
            if (!string.IsNullOrEmpty(pendingPath))
            {
                GameManager.Instance.PendingMapDataPath = null;
                LoadMap(pendingPath);
            }
            else if (!string.IsNullOrEmpty(MapDataPath))
                LoadMap(MapDataPath);
            else
                Debug.Log("[ForeverEngine] No map path. Use File > Open to load a map.");
        }

        public void LoadMap(string mapDataJsonPath)
        {
            Debug.Log($"[ForeverEngine] Loading: {mapDataJsonPath}");

            var importer = GetComponent<MapImporter>();
            if (importer == null) importer = gameObject.AddComponent<MapImporter>();
            importer.Import(mapDataJsonPath, _entityManager);

            _mapDataStore = MapDataStore.Instance;

            if (TileRenderer != null)
                TileRenderer.RenderLevel(_mapDataStore.CurrentZ);

            if (FogRenderer != null)
                FogRenderer.Initialize(_mapDataStore.Width, _mapDataStore.Height);

            // Center camera on player spawn
            var playerSpawn = importer.GetPlayerSpawnPosition();
            if (CameraController != null)
            {
                CameraController.SnapTo(playerSpawn.x + 0.5f, playerSpawn.y + 0.5f);
                // Find the player GameObject (created by EntityRenderer) and set as target
                StartCoroutine(FindAndFollowPlayer());
            }

            Debug.Log($"[ForeverEngine] Map loaded: {_mapDataStore.Width}x{_mapDataStore.Height}");
        }

        private System.Collections.IEnumerator FindAndFollowPlayer()
        {
            // Wait a frame for EntityRenderer to create GameObjects
            yield return null;
            yield return null;

            if (EntityRenderer != null)
            {
                // Camera follows the entity container — player token will be a child
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null && CameraController != null)
                    CameraController.SetTarget(playerGO.transform);
            }
        }

        private void OnDestroy()
        {
            _mapDataStore?.Dispose();
        }
    }
}
