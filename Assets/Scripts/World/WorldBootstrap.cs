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

            // VSync + frame rate
            QualitySettings.vSyncCount = 1; // Sync to monitor refresh — eliminates tearing
            Application.targetFrameRate = -1; // Let VSync control

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

            // Disable orbit camera if present — we use FPS camera instead
            var orbitCam = cam.GetComponent<ForeverEngine.MonoBehaviour.Camera.PerspectiveCameraController>();
            if (orbitCam != null) orbitCam.enabled = false;

            // Attach FPS camera controller
            var fpsCam = cam.gameObject.AddComponent<FPSCameraController>();
            fpsCam.Target = player.transform;
            Debug.Log($"[WorldBootstrap] FPS camera following {player.name}");

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
    /// Standard FPS player controller: WASD move, mouse look, Space jump, Shift sprint.
    /// Mouse cursor locked to center. Esc to unlock.
    /// </summary>
    public class SimplePlayerController : UnityEngine.MonoBehaviour
    {
        public float MoveSpeed = 8f;
        public float SprintMultiplier = 2f;
        public float JumpForce = 6f;
        public float MouseSensitivity = 2f;

        private Rigidbody _rb;
        private bool _grounded;
        private float _yaw;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _yaw = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Esc toggles cursor lock
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // Mouse look — only horizontal on player (vertical on camera)
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseX = Input.GetAxis("Mouse X") * MouseSensitivity;
                _yaw += mouseX;
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }

            // Jump
            if (_grounded && Input.GetKeyDown(KeyCode.Space))
            {
                _rb.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
                _grounded = false;
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;

            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            {
                var vel = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(0f, vel.y, 0f);
                return;
            }

            Vector3 move = (transform.forward * v + transform.right * h).normalized;
            float speed = MoveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= SprintMultiplier;

            Vector3 targetVel = move * speed;
            targetVel.y = _rb.linearVelocity.y; // preserve gravity
            _rb.linearVelocity = targetVel;
        }

        private void OnCollisionStay(Collision col)
        {
            // Ground check — if any contact normal points mostly up, we're grounded
            foreach (var contact in col.contacts)
            {
                if (contact.normal.y > 0.5f)
                { _grounded = true; return; }
            }
        }

        private void OnCollisionExit(Collision col) => _grounded = false;
    }

    /// <summary>
    /// FPS-style third-person camera: follows player from behind,
    /// mouse Y tilts up/down, smooth follow with height offset.
    /// </summary>
    public class FPSCameraController : UnityEngine.MonoBehaviour
    {
        public Transform Target;
        public float Distance = 12f;
        public float HeightOffset = 5f;
        public float MinPitch = -20f;
        public float MaxPitch = 60f;
        public float MouseSensitivity = 2f;
        public float SmoothSpeed = 8f;
        public float ScrollZoomSpeed = 3f;
        public float MinDistance = 3f;
        public float MaxDistance = 30f;

        private float _pitch = 25f;

        private void LateUpdate()
        {
            if (Target == null) return;

            // Vertical mouse look (pitch)
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseY = Input.GetAxis("Mouse Y") * MouseSensitivity;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            }

            // Scroll zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Distance -= scroll * ScrollZoomSpeed;
                Distance = Mathf.Clamp(Distance, MinDistance, MaxDistance);
            }

            // Camera position: behind and above player
            float yaw = Target.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(_pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -Distance);
            Vector3 targetPos = Target.position + Vector3.up * HeightOffset + offset;

            // Smooth follow
            transform.position = Vector3.Lerp(transform.position, targetPos, SmoothSpeed * Time.deltaTime);
            transform.LookAt(Target.position + Vector3.up * 1.5f);
        }
    }
}
