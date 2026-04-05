using UnityEngine;

namespace ForeverEngine.MonoBehaviour.Camera
{
    /// <summary>
    /// Camera controller — rewritten from pygame camera.py.
    /// Smooth follow, zoom, pan, parallax perspective mode.
    /// Runs on main thread (Unity transform manipulation).
    /// </summary>
    public class CameraController : UnityEngine.MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private float _followSpeed = 5f;      // Pygame: 0.12 per frame → ~7.2/sec
        [SerializeField] private Transform _target;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 1.15f;
        [SerializeField] private float _minZoom = 2f;          // Orthographic size
        [SerializeField] private float _maxZoom = 30f;

        [Header("Parallax")]
        [SerializeField] private float _parallaxStrength = 0.05f;  // From pygame PARALLAX_STRENGTH
        [SerializeField] private bool _perspectiveMode = false;

        private UnityEngine.Camera _camera;
        private Vector3 _panOffset;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 8f; // Default zoom
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Smooth follow (frame-rate independent unlike pygame's 0.12 per frame)
            Vector3 targetPos = _target.position + _panOffset;
            targetPos.z = transform.position.z; // Keep camera z fixed
            transform.position = Vector3.SmoothDamp(
                transform.position, targetPos, ref _velocity, 1f / _followSpeed);

            // Parallax offset based on mouse
            if (_perspectiveMode)
            {
                Vector2 mouseOffset = (Vector2)Input.mousePosition -
                    new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                mouseOffset *= _parallaxStrength / Screen.width;
                // Applied to child renderers via shader, not camera position
            }
        }

        public void ZoomIn()
        {
            _camera.orthographicSize = Mathf.Max(
                _camera.orthographicSize / _zoomSpeed, _minZoom);
        }

        public void ZoomOut()
        {
            _camera.orthographicSize = Mathf.Min(
                _camera.orthographicSize * _zoomSpeed, _maxZoom);
        }

        public void Pan(Vector2 screenDelta)
        {
            float worldPerPixel = _camera.orthographicSize * 2f / Screen.height;
            _panOffset += new Vector3(
                -screenDelta.x * worldPerPixel,
                -screenDelta.y * worldPerPixel, 0f);
        }

        public void ResetPan() => _panOffset = Vector3.zero;

        public void SnapTo(float worldX, float worldY)
        {
            transform.position = new Vector3(worldX, worldY, transform.position.z);
            _velocity = Vector3.zero;
            _panOffset = Vector3.zero;
        }

        public void TogglePerspective() => _perspectiveMode = !_perspectiveMode;
        public bool PerspectiveMode => _perspectiveMode;

        public void SetTarget(Transform target) => _target = target;

        /// <summary>
        /// Returns tile coordinates at screen position.
        /// Replaces pygame camera.screen_to_world().
        /// </summary>
        public Vector2Int ScreenToTile(Vector2 screenPos, int tileSize = 32)
        {
            Vector3 world = _camera.ScreenToWorldPoint(screenPos);
            return new Vector2Int(
                Mathf.FloorToInt(world.x / tileSize * 32f),  // Normalize to tile coords
                Mathf.FloorToInt(world.y / tileSize * 32f));
        }
    }
}
