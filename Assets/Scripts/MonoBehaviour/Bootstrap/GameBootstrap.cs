using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Entities;
using ECSWorld = Unity.Entities.World;
using ForeverEngine.Demo;
using ForeverEngine.ECS.Data;
using ForeverEngine.MonoBehaviour.Input;
using ForeverEngine.MonoBehaviour.Camera;

namespace ForeverEngine.MonoBehaviour.Bootstrap
{
    public class GameBootstrap : UnityEngine.MonoBehaviour
    {
        [Header("Map Loading")]
        [Tooltip("Path to map_data.json from Map Generator output")]
        public string MapDataPath;

        [Header("References (auto-created if null)")]
        public CameraController CameraController;
        // Legacy 2D renderer fields (TileRenderer, EntityRenderer, FogRenderer) removed
        // 2026-04-25 with Game.unity. 3D scenes use WorldBootstrap + Gaia + asset packs.

        private EntityManager _entityManager;
        private MapDataStore _mapDataStore;

        private void Start()
        {
            EnsureComponents();

            _entityManager = ECSWorld.DefaultGameObjectInjectionWorld.EntityManager;

            string pendingPath = GameManager.Instance?.PendingMapDataPath;
            if (!string.IsNullOrEmpty(pendingPath))
            {
                GameManager.Instance.PendingMapDataPath = null;
                LoadMap(pendingPath);
            }
            else if (!string.IsNullOrEmpty(MapDataPath))
                LoadMap(MapDataPath);
            else
            {
                Debug.Log("[ForeverEngine] No map path — redirecting to MainMenu.");
                SceneManager.LoadScene("MainMenu");
            }
        }

        /// <summary>
        /// Create any missing required components at runtime.
        /// </summary>
        private void EnsureComponents()
        {
            // Camera
            if (CameraController == null)
            {
                var camGO = UnityEngine.Camera.main?.gameObject;
                if (camGO == null)
                {
                    camGO = new GameObject("Main Camera");
                    var cam = camGO.AddComponent<UnityEngine.Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 5;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
                    camGO.transform.position = new Vector3(0, 0, -10);
                    camGO.tag = "MainCamera";
                }
                CameraController = camGO.GetComponent<CameraController>();
                if (CameraController == null)
                    CameraController = camGO.AddComponent<CameraController>();
            }

            // (Legacy 2D renderer init removed 2026-04-25 with Game.unity.)

            // InputManager
            if (FindAnyObjectByType<InputManager>() == null)
                new GameObject("InputManager").AddComponent<InputManager>();

            // PlayerMovement
            if (FindAnyObjectByType<PlayerMovement>() == null)
                new GameObject("PlayerMovement").AddComponent<PlayerMovement>();

            Debug.Log("[GameBootstrap] Components ensured");
        }

        public void LoadMap(string mapDataJsonPath)
        {
            Debug.Log($"[ForeverEngine] Loading: {mapDataJsonPath}");

            var importer = GetComponent<MapImporter>();
            if (importer == null) importer = gameObject.AddComponent<MapImporter>();
            importer.Import(mapDataJsonPath, _entityManager);

            _mapDataStore = MapDataStore.Instance;

            // (Legacy 2D tile/fog rendering calls removed 2026-04-25 with Game.unity.)

            // Center camera on player spawn
            var playerSpawn = importer.GetPlayerSpawnPosition();
            if (CameraController != null)
            {
                CameraController.SnapTo(playerSpawn.x + 0.5f, playerSpawn.y + 0.5f);
                StartCoroutine(FindAndFollowPlayer());
            }

            Debug.Log($"[ForeverEngine] Map loaded: {_mapDataStore.Width}x{_mapDataStore.Height}");
        }

        private System.Collections.IEnumerator FindAndFollowPlayer()
        {
            yield return null;
            yield return null;

            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null && CameraController != null)
                CameraController.SetTarget(playerGO.transform);
        }

        private void OnDestroy()
        {
            _mapDataStore?.Dispose();
        }
    }
}
