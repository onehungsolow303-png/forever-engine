using ForeverEngine.Core.Network;
using UnityEngine;

namespace ForeverEngine.Network
{
    /// <summary>
    /// Renders one remote player by sampling <see cref="ServerStateCache.PlayerSnapshots"/>
    /// at <c>estimatedServerTime - InterpDelaySec</c>. Buffered playback survives
    /// short packet drops gracefully — the buffer holds the last-known pose and
    /// keeps interpolating smoothly when packets resume.
    /// </summary>
    public class RemotePlayerView : UnityEngine.MonoBehaviour
    {
        public const double InterpDelaySec = 0.1;

        public string PlayerId = "";

        private void Update()
        {
            var cache = ServerStateCache.Instance;
            if (cache == null || !cache.Clock.IsInitialized) return;
            if (!cache.PlayerSnapshots.TryGetValue(PlayerId, out var buffer)) return;

            double targetServerTime = cache.Clock.EstimateServerTimeSec(Time.timeAsDouble) - InterpDelaySec;
            if (!buffer.Sample(targetServerTime, out var x, out var y, out var z, out var yaw)) return;

            transform.position = new Vector3(x, y, z);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
