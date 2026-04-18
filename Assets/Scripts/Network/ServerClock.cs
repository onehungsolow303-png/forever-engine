using UnityEngine;

namespace ForeverEngine.Network
{
    /// <summary>
    /// Estimates the current server-loop time in seconds from the arrival
    /// cadence of <c>PlayerUpdate.ServerTick</c> messages.
    ///
    /// Call <see cref="Observe"/> whenever a PlayerUpdate arrives. Call
    /// <see cref="EstimateServerTimeSec"/> every render frame to get the
    /// sampler target for <c>SnapshotBuffer</c>.
    ///
    /// Approach: on the first sample, lock <c>offset = serverTime - clientNow</c>.
    /// Thereafter, if a fresher sample would raise the offset (i.e., the
    /// server has advanced more than the wall-clock would suggest), accept
    /// it immediately; otherwise bias downward by a small fraction per
    /// observation so the estimate tracks true server time without ping
    /// spikes. A falling-behind offset never snaps — it only drifts.
    /// </summary>
    public class ServerClock
    {
        private readonly double _tickDtSec;
        private readonly double _biasDownFraction;
        private bool _initialized;
        private double _offsetSec; // serverTime - clientTime, in seconds

        public ServerClock(double tickRateHz = 20.0, double biasDownFraction = 0.02)
        {
            _tickDtSec = 1.0 / tickRateHz;
            _biasDownFraction = biasDownFraction;
        }

        public bool IsInitialized => _initialized;

        /// <summary>Observe an incoming server tick against the current client time.</summary>
        public void Observe(uint serverTick, double clientNowSec)
        {
            double candidateOffset = serverTick * _tickDtSec - clientNowSec;
            if (!_initialized)
            {
                _offsetSec = candidateOffset;
                _initialized = true;
                return;
            }

            if (candidateOffset > _offsetSec)
            {
                // Fresh sample is ahead of our estimate — snap up.
                _offsetSec = candidateOffset;
            }
            else
            {
                // Slowly drift down toward true server time to recover from
                // temporarily-delayed packets that inflated the estimate.
                _offsetSec += (candidateOffset - _offsetSec) * _biasDownFraction;
            }
        }

        /// <summary>Estimated server time in seconds at the given client time.</summary>
        public double EstimateServerTimeSec(double clientNowSec)
            => _initialized ? clientNowSec + _offsetSec : 0.0;
    }
}
