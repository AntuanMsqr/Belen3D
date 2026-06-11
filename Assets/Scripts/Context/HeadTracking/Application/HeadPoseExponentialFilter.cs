using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Simple exponential smoothing for head pose (per-axis alpha).
    // Alphas are interpreted at a 60 Hz reference rate and rescaled by deltaTime,
    // so the effective smoothing time constant is independent of the frame rate.
    public class HeadPoseExponentialFilter
    {
        public float positionAlpha = 0.2f; // 0..1 (lower = more smoothing)
        public float rotationAlpha = 0.2f; // 0..1

        private const float ReferenceRate = 60f;

        private bool hasState;
        private Vector3 pos;
        private Vector3 euler;
        private double ts;

        public void Reset()
        {
            hasState = false;
        }

        public HeadPose Filter(HeadPose input, float deltaTime)
        {
            if (!hasState)
            {
                pos = input.position;
                euler = input.eulerDegrees;
                ts = input.timestamp;
                hasState = true;
                return input;
            }

            float kPos = FrameRateIndependentAlpha(positionAlpha, deltaTime);
            float kRot = FrameRateIndependentAlpha(rotationAlpha, deltaTime);

            pos = Vector3.Lerp(pos, input.position, kPos);
            euler = LerpEuler(euler, input.eulerDegrees, kRot);
            ts = input.timestamp;
            return new HeadPose(pos, euler, ts);
        }

        // Converts a per-frame alpha (defined at 60 Hz) into the equivalent alpha for the
        // actual frame duration: k = 1 - (1 - alpha)^(dt * 60).
        private static float FrameRateIndependentAlpha(float alpha, float deltaTime)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha >= 1f || deltaTime <= 0f) return alpha >= 1f ? 1f : 0f;
            return 1f - Mathf.Pow(1f - alpha, deltaTime * ReferenceRate);
        }

        // Per-axis angle lerp so yaw/pitch/roll take the short way across the ±180° wrap.
        private static Vector3 LerpEuler(Vector3 from, Vector3 to, float t)
        {
            return new Vector3(
                Mathf.LerpAngle(from.x, to.x, t),
                Mathf.LerpAngle(from.y, to.y, t),
                Mathf.LerpAngle(from.z, to.z, t));
        }
    }
}
