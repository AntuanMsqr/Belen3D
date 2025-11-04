using System;
using UnityEngine;
using UnityEngine.Events;

namespace Belen.Interaction
{
    // Simple presence detection: considers a user present if pose updates arrive within a timeout.
    public class PresenceDetector : MonoBehaviour
    {
        public MonoBehaviour sourceBehaviour; // IHeadPoseSource
        public float absenceTimeout = 2.0f; // seconds without updates => absent

        [Header("Events")]
        public UnityEvent onPresent;
        public UnityEvent onAbsent;

        private Belen.Tracking.IHeadPoseSource _source;
        private double _lastTs;
        private bool _isPresent;

        public bool IsPresent => _isPresent;

        private void Awake()
        {
            _source = sourceBehaviour as Belen.Tracking.IHeadPoseSource;
            if (_source != null)
            {
                _source.OnPose += HandlePose;
            }
            else
            {
                Debug.LogWarning("[PresenceDetector] sourceBehaviour must implement IHeadPoseSource");
            }
        }

        private void OnDestroy()
        {
            if (_source != null) _source.OnPose -= HandlePose;
        }

        private void HandlePose(Belen.Tracking.HeadPose pose)
        {
            _lastTs = pose.timestamp;
            if (!_isPresent)
            {
                _isPresent = true;
                onPresent?.Invoke();
            }
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (_isPresent && (now - _lastTs) > absenceTimeout)
            {
                _isPresent = false;
                onAbsent?.Invoke();
            }
        }
    }
}

