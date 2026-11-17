using System.Diagnostics;

namespace GameMesh.Aoi
{
    /// <summary>
    /// Server-time estimate for AOI velocity extrapolation.
    /// clock_bias updates only when a new AOI frame with server_time_ms arrives.
    /// </summary>
    public static class AoiVelocityClock
    {
        static readonly Stopwatch Sw = Stopwatch.StartNew();
        static long _biasMs;
        static bool _hasBias;

        public static long LocalNowMs => Sw.ElapsedMilliseconds;
        public static long ClockBiasMs => _biasMs;
        public static bool HasBias => _hasBias;

        public static void Reset()
        {
            _biasMs = 0;
            _hasBias = false;
        }

        /// <summary>clock_bias = server_time_ms − local receive time.</summary>
        public static void NoteAoiFrame(long serverTimeMs, long localReceiveMs)
        {
            if (serverTimeMs == 0)
                return;
            _biasMs = serverTimeMs - localReceiveMs;
            _hasBias = true;
        }

        public static long EstServerNowMs(long localNowMs)
        {
            return localNowMs + _biasMs;
        }
    }

    /// <summary>
    /// Display = event position + (vx,vz) * (estServerNow − event.server_time_ms) / 1000.
    /// Time base is the frame's server_time_ms — never move_start_ms (that only marks the
    /// velocity segment start; using it as the position epoch double-counts keyframe age).
    /// </summary>
    public static class AoiVelocityExtrapolate
    {
        public const float SpeedEpsilon = 0.05f;

        public static bool ShouldExtrapolate(bool enabled, long moveStartMs, float vx, float vz)
        {
            if (!enabled || moveStartMs == 0)
                return false;
            if (vx > -SpeedEpsilon && vx < SpeedEpsilon &&
                vz > -SpeedEpsilon && vz < SpeedEpsilon)
                return false;
            return true;
        }

        public static void Sample(
            float anchorX, float anchorZ,
            float vx, float vz,
            long eventServerTimeMs,
            long estServerNowMs,
            out float x, out float z)
        {
            var dtMs = estServerNowMs - eventServerTimeMs;
            if (dtMs < 0)
                dtMs = 0;
            var dt = dtMs / 1000.0;
            x = anchorX + (float)(vx * dt);
            z = anchorZ + (float)(vz * dt);
        }
    }
}
