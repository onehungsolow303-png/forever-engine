using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Bootstrap for the new procedural World scene. In server mode, waits for the
    /// server to stream the spawn chunk before spawning the player. In offline mode,
    /// generates the spawn chunk locally using PlanetSkeleton + TerrainGenerator.
    /// </summary>
    public class WorldBootstrap : UnityEngine.MonoBehaviour
    {
        [Header("World Settings")]
        public int WorldSeed = 42;

        [Header("Player")]
        public GameObject PlayerPrefab;

        private void Start()
        {
            // Keep running when focus is lost — without this, tasklist might show the process
            // but the game's main loop is suspended, making screenshots and testing unreliable.
            Application.runInBackground = true;

            // Use seed from GameManager if available
            var gm = Demo.GameManager.Instance;
            if (gm != null && gm.CurrentSeed != 0)
                WorldSeed = gm.CurrentSeed;

            // Dev autopilot: --skip-menu triggers periodic in-game screenshots written to disk.
            // These land next to the log and are viewable regardless of focus/Windows state.
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "--skip-menu" || arg == "-skip-menu")
                {
                    StartCoroutine(AutoScreenshotLoop());
                    break;
                }
            }

            // Find the native desktop resolution + highest matching refresh rate.
            // Screen.currentResolution returns the game WINDOW's size (may be 720p default),
            // not the display's native resolution. Display.main.systemWidth/Height is authoritative.
            int nativeW = Display.main.systemWidth;
            int nativeH = Display.main.systemHeight;
            RefreshRate bestRefresh = new RefreshRate { numerator = 60, denominator = 1 };
            double bestHz = 0;
            foreach (var r in Screen.resolutions)
            {
                if (r.width == nativeW && r.height == nativeH && r.refreshRateRatio.value > bestHz)
                {
                    bestHz = r.refreshRateRatio.value;
                    bestRefresh = r.refreshRateRatio;
                }
            }
            if (bestHz <= 0) bestHz = 60;

            // Use borderless fullscreen (FullScreenWindow), not ExclusiveFullScreen.
            // ExclusiveFullScreen does get real vsync but crashes on alt-tab / screenshot
            // because the D3D context is destroyed on focus loss. FullScreenWindow keeps the
            // context alive through focus changes. Tearing is prevented instead by the software
            // targetFrameRate cap below (175fps = native refresh), so no frame outruns the display.
            Screen.SetResolution(nativeW, nativeH, FullScreenMode.FullScreenWindow, bestRefresh);

            // Frame rate strategy for borderless fullscreen on Win11:
            // - Disable Unity's D3D vsync (it's broken — spams "vsync is broken" in log).
            // - Let DWM compositor handle sync (it's already vsynced to monitor).
            // - Cap frame rate a few Hz BELOW refresh so no frame finishes mid-vblank.
            //   On fast mouse movement, an uncapped / at-cap frame rate can produce a frame
            //   that straddles vblank → partial-frame tear. Cap at (refresh - 3) keeps every
            //   frame fully inside its vblank window.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = System.Math.Max(60, (int)System.Math.Round(bestHz) - 3);

            // Raise physics tick rate to reduce render-vs-physics mismatch.
            // Default fixedDeltaTime=0.02 (50Hz) — at 175Hz render, that's 3-4 frames between
            // physics updates, causing visible micro-jitter even with Rigidbody interpolation
            // (because transform rotation is written in Update while velocity reads forward in
            // FixedUpdate → direction lag on strafe). Running physics at render rate eliminates
            // the mismatch. Capped at 120Hz to bound CPU cost.
            float physicsHz = Mathf.Min((float)bestHz, 120f);
            Time.fixedDeltaTime = 1f / physicsHz;
            Time.maximumDeltaTime = 0.1f; // safety clamp

            Debug.Log($"[WorldBootstrap] Display: native={nativeW}x{nativeH} @ {bestHz:F2}Hz, " +
                      $"currentMode={Screen.fullScreenMode}, vsync={QualitySettings.vSyncCount}, " +
                      $"targetFPS={Application.targetFrameRate}");

            // Ensure directional light exists (scene might not have one)
            EnsureLighting();

            // Create chunk manager
            var cmGO = new GameObject("ChunkManager");
            var chunkManager = cmGO.AddComponent<ChunkManager>();
            chunkManager.WorldSeed = WorldSeed;

            // Detect server mode: ConnectionManager exists and is logged in (or connecting)
            bool serverMode = Network.ConnectionManager.Instance != null;

            if (serverMode)
            {
                // Server mode: initialize without skeleton, server will stream chunks
                chunkManager.Initialize(null, serverMode: true);
                Debug.Log("[WorldBootstrap] Server mode — waiting for server to stream spawn chunk.");

                // Wait for server chunk before spawning player
                StartCoroutine(WaitForServerChunkAndSpawn(chunkManager));
            }
            else
            {
                // Offline/fallback mode: generate spawn chunk locally
                OfflineSpawn(chunkManager);
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
        }

        /// <summary>
        /// Wait for the server to stream at least one chunk, then spawn the player on it.
        /// </summary>
        private System.Collections.IEnumerator WaitForServerChunkAndSpawn(ChunkManager chunkManager)
        {
            float timeout = 30f;
            float elapsed = 0f;

            while (chunkManager.LoadedChunkCount == 0)
            {
                elapsed += Time.deltaTime;
                if (elapsed > timeout)
                {
                    Debug.LogWarning("[WorldBootstrap] Timed out waiting for server chunk — falling back to local generation.");
                    OfflineSpawn(chunkManager);
                    yield break;
                }
                yield return null;
            }

            // Server chunk is loaded — find spawn chunk data
            var spawnCoord = new ChunkCoord(0, 0);
            var spawnData = chunkManager.GetChunkData(spawnCoord);

            // If the exact spawn chunk isn't loaded yet, use whatever is available
            if (spawnData == null)
            {
                Debug.LogWarning("[WorldBootstrap] Spawn chunk (0,0) not yet received; waiting...");
                while (spawnData == null && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    spawnData = chunkManager.GetChunkData(spawnCoord);
                    yield return null;
                }

                if (spawnData == null)
                {
                    Debug.LogWarning("[WorldBootstrap] Spawn chunk never received — falling back.");
                    OfflineSpawn(chunkManager);
                    yield break;
                }
            }

            // Sample height from heightmap for spawn position
            int hmRes = ChunkData.HeightmapRes;
            int centerHm = hmRes / 2;
            float terrainHeight = spawnData.Heightmap[centerHm * hmRes + centerHm] * TerrainGenerator.MaxHeight;
            var spawnPos = spawnCoord.WorldCenter;

            // Raycast down from well above the heightmap sample to find the true
            // mesh surface Y (bilinear interp can be taller than the sampled cell).
            // Spawn 2m above the hit so the rigidbody lands cleanly instead of
            // starting embedded in the mesh and tunneling out the bottom.
            spawnPos.y = terrainHeight + 50f;
            if (Physics.Raycast(new Vector3(spawnPos.x, terrainHeight + 100f, spawnPos.z),
                                Vector3.down, out var hit, 300f))
            {
                spawnPos.y = hit.point.y + 2f;
            }

            SpawnPlayer(spawnPos, chunkManager);

            Debug.Log($"[WorldBootstrap] Server-mode spawn complete. seed={WorldSeed}, spawn={spawnPos}, biome={spawnData.Biome}");
        }

        /// <summary>
        /// Offline/fallback spawn: generate spawn chunk locally and spawn the player.
        /// </summary>
        private void OfflineSpawn(ChunkManager chunkManager)
        {
            // Generate the initial chunk FIRST so we can sample terrain height
            var spawnChunk = new ChunkCoord(0, 0);
            chunkManager.Initialize(null); // Initialize skeleton without player yet

            // Force-generate the spawn chunk AND create terrain BEFORE spawning player
            var spawnData = new ChunkData(spawnChunk.X, spawnChunk.Z);
            TerrainGenerator.GenerateHeightmap(spawnData, chunkManager.Skeleton, WorldSeed);
            ChunkPersistence.Save(WorldSeed, spawnChunk, spawnData);

            // Create the terrain mesh so the collider exists before player spawns
            var spawnTerrainGO = TerrainGenerator.CreateTerrain(spawnData);
            Debug.Log($"[WorldBootstrap] Spawn terrain created: biome={spawnData.Biome}, elevation={spawnData.BaseElevation:F2}");

            // Sample height from heightmap for spawn position
            int hmRes = ChunkData.HeightmapRes;
            int centerHm = hmRes / 2;
            float terrainHeight = spawnData.Heightmap[centerHm * hmRes + centerHm] * TerrainGenerator.MaxHeight;
            var spawnPos = spawnChunk.WorldCenter;
            spawnPos.y = terrainHeight + 3f; // 3m above surface

            SpawnPlayer(spawnPos, chunkManager);

            Debug.Log($"[WorldBootstrap] World initialized (offline). Seed={WorldSeed}, spawn={spawnPos}, terrainH={terrainHeight:F1}m, biome={spawnData.Biome}");
        }

        /// <summary>
        /// Spawn or instantiate the player, wire up the camera, and register with ChunkManager.
        /// </summary>
        private void SpawnPlayer(Vector3 spawnPos, ChunkManager chunkManager)
        {
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
                rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooths physics-tick position between render frames — kills Rigidbody stutter

                // Start kinematic for 1s to give MeshCollider.sharedMesh time to
                // finish cooking before gravity activates. Without this the first
                // physics tick tunnels the capsule through the not-yet-registered
                // collider and the player free-falls forever.
                rb.isKinematic = true;
                StartCoroutine(EnablePlayerPhysics(rb, 1f));

                // High-friction physics material so player doesn't slide on terrain
                var physMat = new PhysicsMaterial("PlayerFriction");
                physMat.staticFriction = 1f;
                physMat.dynamicFriction = 1f;
                physMat.frictionCombine = PhysicsMaterialCombine.Maximum;
                player.GetComponent<Collider>().material = physMat;

                player.AddComponent<SimplePlayerController>();
            }

            // Now initialize chunk manager with player for streaming
            // (preserves existing mode — server or offline)
            chunkManager.Initialize(player.transform, chunkManager.ServerMode);

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

            // Spec 7 Phase 1: wire local player to network send loop.
            // ConnectionManager.Instance is null in offline mode — send loop
            // starts only if a connection exists.
            var connMgr = Network.ConnectionManager.Instance;
            if (connMgr != null)
                connMgr.RegisterLocalPlayer(player);

            // Spec 7 Task 13: spawn RemotePlayerManager once (idempotent).
            if (UnityEngine.Object.FindFirstObjectByType<ForeverEngine.Network.RemotePlayerManager>() == null)
            {
                new GameObject("RemotePlayerManager")
                    .AddComponent<ForeverEngine.Network.RemotePlayerManager>();
            }

            // Spec 7 Phase 3 Task 6: spawn PartyPanel once, persistent across scene loads.
            if (UnityEngine.Object.FindFirstObjectByType<Demo.UI.PartyPanel>() == null)
            {
                var panelGO = new GameObject("PartyPanel");
                panelGO.AddComponent<Demo.UI.PartyPanel>();
                UnityEngine.Object.DontDestroyOnLoad(panelGO);
            }

            // Spec 7 Phase 3 Task 8: F-key debug dungeon entry.
            if (UnityEngine.Object.FindFirstObjectByType<ForeverEngine.Procedural.DungeonEntryInput>() == null)
            {
                new GameObject("DungeonEntryInput").AddComponent<ForeverEngine.Procedural.DungeonEntryInput>();
            }
        }

        /// <summary>
        /// Holds the player as a kinematic Rigidbody for a short delay so the
        /// freshly-created terrain MeshColliders have time to finish cooking
        /// before gravity engages. Without this the first physics tick tunnels
        /// the capsule through the not-yet-active collider and the player
        /// free-falls indefinitely.
        /// </summary>
        private System.Collections.IEnumerator EnablePlayerPhysics(Rigidbody rb, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (rb != null) rb.isKinematic = false;
        }

        private System.Collections.IEnumerator AutoScreenshotLoop()
        {
            // Wait for first frame to ensure terrain + player + camera exist
            yield return new WaitForSeconds(3f);
            int n = 0;
            while (true)
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath,
                    $"autoshot_{n:D3}.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[AutoShot] {path}");
                n++;
                yield return new WaitForSeconds(2f);
                if (n > 20) yield break; // stop after 20 shots
            }
        }

        private void EnsureLighting()
        {
            // Check if a directional light exists
            var lights = FindObjectsByType<Light>();
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

        // --- Exposed input state for ConnectionManager (see Spec 7 Phase 1) ---
        [System.NonSerialized] public float LastInputX;
        [System.NonSerialized] public float LastInputZ;
        [System.NonSerialized] public bool LastSprint;
        [System.NonSerialized] public bool JumpPressedThisFrame;
        public float Yaw => _yaw;

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

            // Capture Space press as edge-triggered flag. ConnectionManager consumes it
            // on its next send tick so exactly one MoveInput{Jump=true} is emitted per press.
            if (Input.GetKeyDown(KeyCode.Space)) JumpPressedThisFrame = true;

            // Local-prediction jump — server also applies JumpImpulse via MovementHandler.
            if (_grounded && JumpPressedThisFrame)
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

            // Snapshot for ConnectionManager — ensures the same input that drives local
            // physics is what gets sent to the server, no split-brain.
            LastInputX = h;
            LastInputZ = v;
            LastSprint = Input.GetKey(KeyCode.LeftShift);

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
        public float SmoothSpeed = 25f; // Tight follow — lower values create a trailing jitter at high refresh rates
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

            // Smooth follow — frame-rate independent exponential smoothing
            float t = 1f - Mathf.Exp(-SmoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
            transform.LookAt(Target.position + Vector3.up * 1.5f);
        }
    }
}
