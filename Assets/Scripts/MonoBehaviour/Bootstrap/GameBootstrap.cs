using UnityEngine;
using Unity.Entities;

namespace ForeverEngine.MonoBehaviour.Bootstrap
{
    /// <summary>
    /// Entry point — rewritten from pygame main.py main().
    /// Initializes ECS world, loads map, spawns entities, starts game loop.
    /// Attach to a single GameObject in the bootstrap scene.
    /// </summary>
    public class GameBootstrap : UnityEngine.MonoBehaviour
    {
        [Header("Map Loading")]
        [Tooltip("Path to map_data.json from Map Generator output")]
        public string MapDataPath;

        [Header("References")]
        public GameConfig GameConfig;
        public CameraController CameraController;

        private EntityManager _entityManager;
        private bool _initialized;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!string.IsNullOrEmpty(MapDataPath))
            {
                LoadMap(MapDataPath);
            }
            else
            {
                Debug.Log("[ForeverEngine] No map path specified. Waiting for map selection.");
            }
        }

        public void LoadMap(string mapDataJsonPath)
        {
            Debug.Log($"[ForeverEngine] Loading map: {mapDataJsonPath}");

            var importer = GetComponent<MapImporter>();
            if (importer == null)
                importer = gameObject.AddComponent<MapImporter>();

            importer.Import(mapDataJsonPath, _entityManager);
            _initialized = true;

            // Center camera on player spawn
            var playerSpawn = importer.GetPlayerSpawnPosition();
            if (CameraController != null)
                CameraController.SnapTo(playerSpawn.x, playerSpawn.y);

            Debug.Log("[ForeverEngine] Map loaded. Game ready.");
        }
    }
}
