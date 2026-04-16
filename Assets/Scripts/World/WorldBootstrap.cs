using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Bootstrap for the new procedural World scene. Creates the planet skeleton,
    /// chunk manager, spawns the player at (0,0), and sets up the camera + lighting.
    /// </summary>
    public class WorldBootstrap : UnityEngine.MonoBehaviour
    {
        [Header("World Settings")]
        public int WorldSeed = 42;

        [Header("Player")]
        public GameObject PlayerPrefab;

        private void Start()
        {
            // Use seed from GameManager if available
            var gm = Demo.GameManager.Instance;
            if (gm != null && gm.CurrentSeed != 0)
                WorldSeed = gm.CurrentSeed;

            // Ensure directional light exists (scene might not have one)
            EnsureLighting();

            // Create chunk manager
            var cmGO = new GameObject("ChunkManager");
            var chunkManager = cmGO.AddComponent<ChunkManager>();
            chunkManager.WorldSeed = WorldSeed;

            // Generate the initial chunk FIRST so we can sample terrain height
            var spawnChunk = new ChunkCoord(0, 0);
            chunkManager.Initialize(null); // Initialize skeleton without player yet

            // Force-generate the spawn chunk synchronously
            var spawnData = new ChunkData(spawnChunk.X, spawnChunk.Z);
            TerrainGenerator.GenerateHeightmap(spawnData, chunkManager.Skeleton, WorldSeed);
            ChunkPersistence.Save(WorldSeed, spawnChunk, spawnData);

            // Sample terrain height at center of spawn chunk
            int centerIdx = (ChunkCoord.ChunkSize / 2) * ChunkCoord.ChunkSize + (ChunkCoord.ChunkSize / 2);
            float terrainHeight = spawnData.Heightmap[centerIdx] * TerrainGenerator.MaxHeight;

            // Spawn player ABOVE the terrain
            var spawnPos = spawnChunk.WorldCenter;
            spawnPos.y = terrainHeight + 5f; // 5m above terrain surface

            GameObject player;
            if (PlayerPrefab != null)
            {
                player = Instantiate(PlayerPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Fallback: create capsule player
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.transform.position = spawnPos;
                player.name = "Player";
                player.tag = "Player";

                // Visible material
                var renderer = player.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard"));
                    mat.color = new Color(0.2f, 0.5f, 0.9f); // Blue player
                    renderer.material = mat;
                }

                // Physics
                var rb = player.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.mass = 70f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                player.AddComponent<SimplePlayerController>();
            }

            // Now initialize chunk manager with player for streaming
            chunkManager.Initialize(player.transform);

            // Setup camera
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                // Try to find existing PerspectiveCameraController
                var camController = cam.GetComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
                if (camController == null)
                    camController = cam.gameObject.AddComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
                camController.FollowTarget = player.transform;

                // Position camera behind player initially
                cam.transform.position = spawnPos + new Vector3(0f, 10f, -15f);
                cam.transform.LookAt(spawnPos);
            }

            // Atmosphere (post-processing)
            var atmosGO = new GameObject("Atmosphere");
            atmosGO.AddComponent<Demo.Overworld.AtmosphereSetup>();

            // Skybox
            if (RenderSettings.skybox == null)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.5f, 0.6f, 0.8f);
                RenderSettings.ambientEquatorColor = new Color(0.4f, 0.5f, 0.6f);
                RenderSettings.ambientGroundColor = new Color(0.2f, 0.25f, 0.2f);
            }

            Debug.Log($"[WorldBootstrap] World initialized. Seed={WorldSeed}, spawn={spawnPos}, terrainH={terrainHeight:F1}m, biome={spawnData.Biome}");
        }

        private void EnsureLighting()
        {
            // Check if a directional light exists
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool hasDirectional = false;
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                { hasDirectional = true; break; }
            }

            if (!hasDirectional)
            {
                var lightGO = new GameObject("Sun");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }
    }

    /// <summary>
    /// Minimal WASD + mouse-look player controller for the procedural world.
    /// </summary>
    public class SimplePlayerController : UnityEngine.MonoBehaviour
    {
        public float MoveSpeed = 8f;
        public float SprintMultiplier = 2f;
        public float RotateSpeed = 120f;

        private Rigidbody _rb;

        private void Start() => _rb = GetComponent<Rigidbody>();

        private void Update()
        {
            if (Input.GetKey(KeyCode.Q))
                transform.Rotate(0f, -RotateSpeed * Time.deltaTime, 0f);
            if (Input.GetKey(KeyCode.E))
                transform.Rotate(0f, RotateSpeed * Time.deltaTime, 0f);

            if (Input.GetMouseButton(1))
            {
                float mx = Input.GetAxis("Mouse X") * RotateSpeed * Time.deltaTime;
                transform.Rotate(0f, mx * 2f, 0f);
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 move = (transform.forward * v + transform.right * h).normalized;
            float speed = MoveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= SprintMultiplier;

            _rb.MovePosition(_rb.position + move * speed * Time.fixedDeltaTime);
        }
    }
}
