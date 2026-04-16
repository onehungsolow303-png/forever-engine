using UnityEngine;

namespace ForeverEngine.World
{
    /// <summary>
    /// Bootstrap for the new procedural World scene. Creates the planet skeleton,
    /// chunk manager, spawns the player at (0,0), and sets up the camera.
    /// Replaces the old Overworld3DSetup.
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

            // Create chunk manager
            var cmGO = new GameObject("ChunkManager");
            var chunkManager = cmGO.AddComponent<ChunkManager>();
            chunkManager.WorldSeed = WorldSeed;

            // Spawn player at chunk (0,0) center
            var spawnPos = new ChunkCoord(0, 0).WorldCenter;
            spawnPos.y = 50f; // Start above terrain, will settle with gravity

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
                player.tag = "Player";

                // Add basic movement
                var rb = player.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.mass = 70f;

                player.AddComponent<SimplePlayerController>();
            }

            // Initialize chunk manager with player
            chunkManager.Initialize(player.transform);

            // Setup camera — wire PerspectiveCameraController follow target
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var camController = cam.GetComponent<MonoBehaviour.Camera.PerspectiveCameraController>();
                if (camController == null)
                    camController = cam.gameObject.AddComponent<MonoBehaviour.Camera.PerspectiveCameraController>();
                camController.FollowTarget = player.transform;
            }

            // Atmosphere
            var atmosGO = new GameObject("Atmosphere");
            atmosGO.AddComponent<Demo.Overworld.AtmosphereSetup>();

            Debug.Log($"[WorldBootstrap] World initialized. Seed={WorldSeed}, spawn={spawnPos}");
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
            // Rotation
            if (Input.GetKey(KeyCode.Q))
                transform.Rotate(0f, -RotateSpeed * Time.deltaTime, 0f);
            if (Input.GetKey(KeyCode.E))
                transform.Rotate(0f, RotateSpeed * Time.deltaTime, 0f);

            // Mouse rotation
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
