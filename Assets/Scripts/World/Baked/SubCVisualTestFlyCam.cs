using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Minimal WASD + mouse-look fly camera for the Sub C visual test scene.
    /// Holds the right mouse button to look around. Esc unlocks cursor.
    /// </summary>
    public sealed class SubCVisualTestFlyCam : UnityEngine.MonoBehaviour
    {
        public float MoveSpeed = 200f;
        public float LookSpeed = 180f;
        public float SprintMultiplier = 5f;

        private float _yaw;
        private float _pitch;

        void Start()
        {
            var e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }

        void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxisRaw("Mouse X") * LookSpeed * Time.deltaTime;
                _pitch -= Input.GetAxisRaw("Mouse Y") * LookSpeed * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);
            }

            float speed = MoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? SprintMultiplier : 1f);
            var fwd = transform.forward;
            var right = transform.right;
            var up = transform.up;
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += fwd;
            if (Input.GetKey(KeyCode.S)) move -= fwd;
            if (Input.GetKey(KeyCode.D)) move += right;
            if (Input.GetKey(KeyCode.A)) move -= right;
            if (Input.GetKey(KeyCode.E)) move += up;
            if (Input.GetKey(KeyCode.Q)) move -= up;
            transform.position += move * speed * Time.deltaTime;
        }
    }
}
