using UnityEngine;

namespace ForeverEngine.Genres.FPS
{
    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        [Header("Movement")] public float WalkSpeed = 5f; public float SprintSpeed = 8f; public float JumpForce = 5f; public float Gravity = -20f;
        [Header("Camera")] public float MouseSensitivity = 2f; public float FieldOfView = 90f; public float HeadBobAmount = 0.05f;

        private CharacterController _cc;
        private Transform _cameraTransform;
        private Vector3 _velocity;
        private float _cameraPitch;
        private bool _sprinting;

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            _cameraTransform = GetComponentInChildren<Camera>()?.transform ?? transform;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            HandleLook(); HandleMovement();
        }

        private void HandleLook()
        {
            float mx = Input.GetAxis("Mouse X") * MouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * MouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch - my, -90f, 90f);
            _cameraTransform.localEulerAngles = new Vector3(_cameraPitch, 0, 0);
            transform.Rotate(Vector3.up * mx);
        }

        private void HandleMovement()
        {
            _sprinting = Input.GetKey(KeyCode.LeftShift);
            float speed = _sprinting ? SprintSpeed : WalkSpeed;
            Vector3 move = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
            _cc.Move(move.normalized * speed * Time.deltaTime);

            if (_cc.isGrounded && _velocity.y < 0) _velocity.y = -2f;
            if (_cc.isGrounded && Input.GetButtonDown("Jump")) _velocity.y = JumpForce;
            _velocity.y += Gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool IsSprinting => _sprinting;
    }
}
