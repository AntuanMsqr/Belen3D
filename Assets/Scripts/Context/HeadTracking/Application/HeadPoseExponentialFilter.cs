using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Simple exponential smoothing for head pose (per-axis alpha).
    public class HeadPoseExponentialFilter
    {
        public float positionAlpha = 0.2f; // 0..1 (lower = more smoothing)
        public float rotationAlpha = 0.2f; // 0..1

        private bool hasState;
        private Vector3 pos;
        private Vector3 euler;
        private double ts;

        public void Reset()
        {
            hasState = false;
        }

        public HeadPose Filter(HeadPose input)
        {
            if (!hasState)
            {
                pos = input.position;
                euler = input.eulerDegrees;
                ts = input.timestamp;
                hasState = true;
                return input;
            }

            pos = Vector3.Lerp(pos, input.position, Mathf.Clamp01(positionAlpha));
            euler = Vector3.Lerp(euler, input.eulerDegrees, Mathf.Clamp01(rotationAlpha));
            ts = input.timestamp;
            return new HeadPose(pos, euler, ts);
        }
    }
}
