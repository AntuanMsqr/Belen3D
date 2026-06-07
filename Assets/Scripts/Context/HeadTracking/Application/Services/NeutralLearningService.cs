using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Learns the neutral head position used as the orbit/parallax origin.
    // Mirrors the legacy FaceTrackerManager behavior: either average over a window
    // (autoNeutral) or snap to the first stable pose (snapNeutralOnStart).
    public sealed class NeutralLearningService
    {
        private readonly IClock _clock;

        private bool _learning;
        private bool _snapped;
        private double _startTs;
        private float _sumX, _sumY, _sumZ;
        private int _count;

        public bool IsLearning => _learning;

        public NeutralLearningService(IClock clock)
        {
            _clock = clock;
        }

        public void Reset()
        {
            _learning = false;
            _snapped = false;
            _count = 0;
            _sumX = _sumY = _sumZ = 0f;
        }

        public void Process(in HeadPose filtered, ref CalibrationData cal)
        {
            if (cal.autoNeutral && !_learning)
            {
                Begin();
            }

            if (_learning)
            {
                Accumulate(filtered, ref cal);
            }
            else if (cal.snapNeutralOnStart && !_snapped)
            {
                cal.neutralZ = filtered.position.z;
                cal.neutralXY = new Vector2(filtered.position.x, filtered.position.y);
                cal.neutralEuler = filtered.eulerDegrees;
                _snapped = true;
            }
        }

        private void Begin()
        {
            _learning = true;
            _sumX = _sumY = _sumZ = 0f;
            _count = 0;
            _startTs = _clock.NowSeconds;
            _snapped = true; // prevent snap once auto-learning has started
        }

        private void Accumulate(in HeadPose pose, ref CalibrationData cal)
        {
            if (_count == 0) _startTs = _clock.NowSeconds;
            _sumX += pose.position.x;
            _sumY += pose.position.y;
            _sumZ += pose.position.z;
            _count++;

            if (_clock.NowSeconds - _startTs >= Mathf.Max(0.1f, cal.autoNeutralDuration))
            {
                float denom = Mathf.Max(1, _count);
                cal.neutralZ = _sumZ / denom;
                cal.neutralXY = new Vector2(_sumX / denom, _sumY / denom);
                _learning = false;
            }
        }
    }
}
