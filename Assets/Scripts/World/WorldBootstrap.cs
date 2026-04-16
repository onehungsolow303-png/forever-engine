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

            // Force-generate the spawn chunk AND create terrain BEFORE spawning player
            var spawnData = new ChunkData(spawnChunk.X, spawnChunk.Z);
            TerrainGenerator.GenerateHeightmap(spawnData, chunkManager.Skeleton, WorldSeed);
            ChunkPersistence.Save(WorldSeed, spawnChunk, spawnData);

            // Create the terrain mesh so the collider exists before player spawns
            var spawnTerrain = TerrainGenerator.CreateTerrain(spawnData);
            Debug.Log($"[WorldBootstrap] Spawn terrain created: biome={spawnData.Biome}, elevation={spawnData.BaseElevation:F2}");

            // Use Unity's Terrain.SampleHeight for accurate surface position
            var spawnPos = spawnChunk.WorldCenter;
            float terrainHeight = spawnTerrain.SampleHeight(spawnPos);
            spawnPos.y = terrainHeight + spawnTerrain.transform.position.y + 3f; // 3m above surface

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

                // Physics — high drag prevents sliding on slopes
                var rb = player.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.mass = 70f;
                rb.linearDamping = 5f; // Prevents sliding when no input
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // High-friction physics material so player doesn't slide on terrain
                var physMat = new PhysicsMaterial("PlayerFriction");
                physMat.staticFriction = 1f;
                physMat.dynamicFriction = 1f;
                physMat.frictionCombine = PhysicsMaterialCombine.Maximum;
                player.GetComponent<Collider>().material = physMat;

                player.AddComponent<SimplePlayerController>();
            }

            // Now initialize chunk manager with player for streaming
            chunkManager.Initialize(player.transform);

            // Setup camera — ensure one exists and follows the player
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("MainCamera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<UnityEngine.Camera>();
                camGO.AddComponent<AudioListener>();
            }

            // Position camera behind and above player
            cam.transform.position = spawnPos + new Vector3(0f, 12f, -18f);
            cam.transform.LookAt(player.transform.position + Vector3.up * 1.5f);

            // Attach follow controller
            var camController = cam.GetComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            if (camController == null)
                camController = cam.gameObject.AddComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            camController.FollowTarget = player.transform;
            Debug.Log($"[WorldBootstrap] Camera following {player.name}, controller={camController != null}");

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

            // Only read WASD keys directly — avoids phantom joystick/gamepad input
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            {
                // No input — kill horizontal velocity to prevent sliding
                var vel = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(0f, vel.y, 0f);
                return;
            }

            Vector3 move = (transform.forward * v + transform.right * h).normalized;
            float speed = MoveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= SprintMultiplier;

            _rb.MovePosition(_rb.position + move * speed * Time.fixedDeltaTime);
        }
    }
}
