using UnityEngine;

namespace ForeverEngine.MonoBehaviour.Camera
{
    /// <summary>
    /// Standalone 3D perspective camera controller for the engine transition
    /// from 2D to 3D. Provides an isometric-perspective view with orbit
    /// controls, similar to Neverwinter Nights or Baldur's Gate 3.
    ///
    /// This is a NEW file — does NOT replace the existing CameraController.cs
    /// which stays active for 2D scenes. The transition spec
    /// (2026-04-09-3d-engine-transition-design.md) plans for both to coexist
    /// until the 2D scenes are retired.
    ///
    /// Usage: attach to a Camera GameObject in any 3D scene. Set the
    /// FollowTarget to the player character's Transform. The camera will
    /// orbit around the target at a fixed elevation angle with smooth
    /// follow and zoom.
    ///
    /// Controls:
    ///   Q/E or Middle-mouse drag  — orbit (rotate around target)
    ///   Scroll wheel              — zoom in/out
    ///   Camera auto-follows FollowTarget with damping
    ///
    /// Designed for the purchased environment packs (Dungeon Catacomb,
    /// Lordenfel, Forest/Mountain Dynamic Nature, Medieval Town).
    /// </summary>
    public class PerspectiveCameraController : UnityEngine.MonoBehaviour
    {
        [Header("Follow Target")]
        [Tooltip("The Transform the camera orbits around and follows. " +
                 "Set to the player character.")]
        public Transform FollowTarget;

        [Header("Orbit")]
        [Tooltip("Horizontal angle around the target (degrees). " +
                 "0 = looking north, 90 = looking east.")]
        [SerializeField] private float _orbitAngle = 45f;

        [Tooltip("Vertical elevation angle (degrees above horizontal). " +
                 "45 = classic isometric, 60 = more top-down, 30 = more side-on.")]
        [Range(15f, 75f)]
        [SerializeField] private float _elevationAngle = 50f;

        [Tooltip("Speed of orbit rotation when pressing Q/E (degrees/sec).")]
        [SerializeField] private float _orbitSpeed = 120f;

        [Tooltip("Speed of orbit rotation when dragging middle mouse (degrees/pixel).")]
        [SerializeField] private float _mouseOrbitSpeed = 0.5f;

        [Header("Distance / Zoom")]
        [Tooltip("Distance from the camera to the follow target (units).")]
        [SerializeField] private float _distance = 15f;

        [Tooltip("Minimum zoom distance.")]
        [SerializeField] private float _minDistance = 5f;

        [Tooltip("Maximum zoom distance.")]
        [SerializeField] private float _maxDistance = 40f;

        [Tooltip("Scroll wheel zoom speed (units per scroll tick).")]
        [SerializeField] private float _zoomSpeed = 3f;

        [Tooltip("Zoom smoothing (lower = snappier, higher = smoother).")]
        [SerializeField] private float _zoomSmoothing = 5f;

        [Header("Follow")]
        [Tooltip("How quickly the camera catches up to the target. " +
                 "Lower = more responsive, higher = more cinematic.")]
        [SerializeField] private float _followDamping = 0.15f;

        [Tooltip("Height offset above the target's feet. " +
                 "Adjust so the camera looks at the character's chest, not feet.")]
        [SerializeField] private float _targetHeightOffset = 1.5f;

        [Header("Collision")]
        [Tooltip("Push the camera forward when geometry is between it and the target. " +
                 "Prevents the camera from clipping through walls in dungeons.")]
        [SerializeField] private bool _enableCollision = true;

        [Tooltip("Layer mask for collision checks. Set to Default + Environment.")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Tooltip("Offset from collision surface to prevent Z-fighting.")]
        [SerializeField] private float _collisionOffset = 0.3f;

        // ── Internal state ──────────────────────────────────────────

        private float _targetDistance;
        private float _currentDistance;
        private Vector3 _followPosition;
        private Vector3 _velocity; // for SmoothDamp

        private void Awake()
        {
            _targetDistance = _distance;
            _currentDistance = _distance;

            // Ensure the camera is in perspective mode
            var cam = GetComponent<UnityEngine.Camera>();
            if (cam != null)
            {
                cam.orthographic = false;
                if (cam.fieldOfView < 30f || cam.fieldOfView > 90f)
                    cam.fieldOfView = 45f;
            }

            if (FollowTarget != null)
                _followPosition = FollowTarget.position;
        }

        private void LateUpdate()
        {
            HandleInput();
            UpdatePosition();
        }

        // ── Input handling ──────────────────────────────────────────

        private void HandleInput()
        {
            // Orbit: Q/E keys
            if (UnityEngine.Input.GetKey(KeyCode.Q))
                _orbitAngle -= _orbitSpeed * Time.deltaTime;
            if (UnityEngine.Input.GetKey(KeyCode.E))
                _orbitAngle += _orbitSpeed * Time.deltaTime;

            // Orbit: middle mouse drag
            if (UnityEngine.Input.GetMouseButton(2)) // middle mouse
            {
                float dx = UnityEngine.Input.GetAxis("Mouse X");
                _orbitAngle += dx * _mouseOrbitSpeed * 10f;
            }

            // Keep orbit angle in 0-360 range
            _orbitAngle = Mathf.Repeat(_orbitAngle, 360f);

            // Zoom: scroll wheel
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _targetDistance -= scroll * _zoomSpeed;
                _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
            }

            // Smooth zoom
            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * _zoomSmoothing);
        }

        // ── Position calculation ────────────────────────────────────

        private void UpdatePosition()
        {
            // Follow target with damping
            Vector3 targetPos;
            if (FollowTarget != null)
            {
                targetPos = FollowTarget.position + Vector3.up * _targetHeightOffset;
                _followPosition = Vector3.SmoothDamp(
                    _followPosition, targetPos, ref _velocity, _followDamping);
            }
            else
            {
                targetPos = _followPosition;
            }

            // Calculate camera position on the orbit sphere
            float orbRad = _orbitAngle * Mathf.Deg2Rad;
            float elvRad = _elevationAngle * Mathf.Deg2Rad;

            // Spherical to Cartesian: the camera sits on a sphere centered
            // at the follow position, at the configured distance.
            Vector3 offset = new Vector3(
                Mathf.Sin(orbRad) * Mathf.Cos(elvRad),
                Mathf.Sin(elvRad),
                Mathf.Cos(orbRad) * Mathf.Cos(elvRad)
            ) * _currentDistance;

            Vector3 desiredPosition = _followPosition + offset;

            // Collision: if geometry is between the camera and the target,
            // push the camera forward to avoid clipping through walls.
            float effectiveDistance = _currentDistance;
            if (_enableCollision)
            {
                Vector3 direction = (desiredPosition - _followPosition).normalized;
                if (Physics.Raycast(
                    _followPosition,
                    direction,
                    out RaycastHit hit,
                    _currentDistance,
                    _collisionMask))
                {
                    effectiveDistance = hit.distance - _collisionOffset;
                    if (effectiveDistance < _minDistance * 0.5f)
                        effectiveDistance = _minDistance * 0.5f;
                    desiredPosition = _followPosition + direction * effectiveDistance;
                }
            }

            transform.position = desiredPosition;

            // Look at the follow position (the target's chest height)
            transform.LookAt(_followPosition);
        }

        // ── Public API ──────────────────────────────────────────────

        /// <summary>Snap the orbit to a specific angle (e.g., face north).</summary>
        public void SetOrbitAngle(float degrees)
        {
            _orbitAngle = Mathf.Repeat(degrees, 360f);
        }

        /// <summary>Snap the zoom to a specific distance.</summary>
        public void SetDistance(float distance)
        {
            _targetDistance = Mathf.Clamp(distance, _minDistance, _maxDistance);
            _currentDistance = _targetDistance;
        }

        /// <summary>Smoothly zoom to a distance.</summary>
        public void ZoomTo(float distance)
        {
            _targetDistance = Mathf.Clamp(distance, _minDistance, _maxDistance);
        }

        /// <summary>Snap the camera to the target immediately (no damping).</summary>
        public void SnapToTarget()
        {
            if (FollowTarget != null)
            {
                _followPosition = FollowTarget.position + Vector3.up * _targetHeightOffset;
                _velocity = Vector3.zero;
            }
        }

        /// <summary>Get the current orbit angle (0-360 degrees).</summary>
        public float OrbitAngle => _orbitAngle;

        /// <summary>Get the current zoom distance.</summary>
        public float Distance => _currentDistance;
    }
}
