using UnityEngine;

namespace ForeverEngine.Network
{
    /// <summary>
    /// Renders one remote player. Interpolates between the last two
    /// received poses across InterpSeconds for smooth motion.
    /// </summary>
    public class RemotePlayerView : MonoBehaviour
    {
        private const float InterpSeconds = 0.1f;

        private struct Pose
        {
            public Vector3 Pos;
            public float Yaw;
            public double ReceivedAt;
        }

        public string PlayerId = "";
        private Pose _prev;
        private Pose _curr;
        private bool _hasPrev;

        /// <summary>Ingest a new pose. Called by RemotePlayerManager on each PlayerUpdate.</summary>
        public void PushPose(Vector3 pos, float yaw, double receivedAt)
        {
            if (_hasPrev || _curr.ReceivedAt > 0)
            {
                _prev = _curr;
                _hasPrev = true;
            }
            _curr = new Pose { Pos = pos, Yaw = yaw, ReceivedAt = receivedAt };
        }

        private void Update()
        {
            if (_curr.ReceivedAt <= 0) return;
            if (!_hasPrev)
            {
                transform.position = _curr.Pos;
                transform.rotation = Quaternion.Euler(0f, _curr.Yaw, 0f);
                return;
            }
            double now = Time.timeAsDouble;
            float t = Mathf.InverseLerp(
                (float)_curr.ReceivedAt,
                (float)(_curr.ReceivedAt + InterpSeconds),
                (float)now);
            t = Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(_prev.Pos, _curr.Pos, t);
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(_prev.Yaw, _curr.Yaw, t), 0f);
        }
    }
}
