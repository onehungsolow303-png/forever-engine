using UnityEngine;

namespace ForeverEngine.Diagnostics
{
    /// <summary>
    /// Drop-in fly-cam: WASD + QE for vertical, mouse-look (right-click held), Shift for fast.
    /// Auto-attaches to Main Camera at runtime via [RuntimeInitializeOnLoadMethod].
    /// Controls:
    ///   W/S         — forward/back
    ///   A/D         — strafe left/right
    ///   E / Space   — move up
    ///   Q / Ctrl    — move down
    ///   Right-click + drag — look around
    ///   Left Shift  — fast mode (4x speed)
    /// </summary>
    public class SimpleFlyCam : UnityEngine.MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToMainCamera()
        {
            // Run a frame later so scene cameras are ready
            var go = new GameObject("SimpleFlyCam_Bootstrapper");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FlyCamBootstrapper>();
        }

        private float _yaw, _pitch;

        private void Start()
        {
            _yaw   = transform.rotation.eulerAngles.y;
            _pitch = transform.rotation.eulerAngles.x;
        }

        private void Update()
        {
            float speed = Input.GetKey(KeyCode.LeftShift) ? 200f : 50f;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W))                                    move += transform.forward;
            if (Input.GetKey(KeyCode.S))                                    move -= transform.forward;
            if (Input.GetKey(KeyCode.A))                                    move -= transform.right;
            if (Input.GetKey(KeyCode.D))                                    move += transform.right;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))     move += Vector3.up;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;
            transform.position += move.normalized * speed * Time.deltaTime;

            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * 2f;
                _pitch -= Input.GetAxis("Mouse Y") * 2f;
                _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }

    /// <summary>
    /// Waits one frame for Camera.main to be available, then attaches SimpleFlyCam and self-destructs.
    /// </summary>
    public class FlyCamBootstrapper : UnityEngine.MonoBehaviour
    {
        private bool _attached;

        private void Update()
        {
            if (_attached) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<SimpleFlyCam>() == null)
                cam.gameObject.AddComponent<SimpleFlyCam>();
            _attached = true;
            Destroy(gameObject);
        }
    }
}
