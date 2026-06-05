using System;
using UnityEngine;
using Belen.HeadTracking.Domain;

namespace Belen.HeadTracking.Infrastructure
{
    // Presence detection View: considers a user present if pose updates arrive within a timeout.
    // Wired by the Bootstrap via Initialize(); exposes plain C# events for other contexts.
    public class PresenceView : MonoBehaviour
    {
        public float absenceTimeout = 2.0f;

        public event Action OnPresent;
        public event Action OnAbsent;

        private IHeadPoseSource _source;
        private double _lastTs;
        private volatile bool _gotPoseFlag;
        private bool _isPresent;

        public bool IsPresent => _isPresent;

        public void Initialize(IHeadPoseSource source)
        {
            if (_source != null) _source.OnPose -= HandlePose;
            _source = source;
            if (_source != null) _source.OnPose += HandlePose;
        }

        private void OnDestroy()
        {
            if (_source != null) _source.OnPose -= HandlePose;
        }

        // May be raised off the main thread (UDP sources) — keep it trivial.
        private void HandlePose(HeadPose pose)
        {
            _gotPoseFlag = true;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;

            if (_gotPoseFlag)
            {
                _gotPoseFlag = false;
                _lastTs = now;
                if (!_isPresent)
                {
                    _isPresent = true;
                    OnPresent?.Invoke();
                }
            }

            if (_isPresent && (now - _lastTs) > absenceTimeout)
            {
                _isPresent = false;
                OnAbsent?.Invoke();
            }
        }
    }
}
