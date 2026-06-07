using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Simple exponential smoothing for head pose (per-axis alpha).
    public class HeadPoseExponentialFilter
    {
        public float positionAlpha = 0.2f; // 0..1 (lower = more smoothing)
        public float rotationAlpha = 0.2f; // 0..1

        private bool _hasState;
        private Vector3 _pos;
        private Vector3 _euler;
        private double _ts;

        public void Reset()
        {
            _hasState = false;
        }

        public HeadPose Filter(HeadPose input)
        {
            if (!_hasState)
            {
                _pos = input.position;
                _euler = input.eulerDegrees;
                _ts = input.timestamp;
                _hasState = true;
                return input;
            }

            _pos = Vector3.Lerp(_pos, input.position, Mathf.Clamp01(positionAlpha));
            _euler = Vector3.Lerp(_euler, input.eulerDegrees, Mathf.Clamp01(rotationAlpha));
            _ts = input.timestamp;
            return new HeadPose(_pos, _euler, _ts);
        }
    }
}
