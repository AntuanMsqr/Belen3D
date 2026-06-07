using System;
using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Infrastructure
{
    // Presence detection View: considers a user present if pose updates arrive within a timeout.
    // Wired by the Bootstrap via Initialize(); exposes plain C# events for other contexts.
    public class PresenceView : MonoBehaviour
    {
        public float absenceTimeout = 2.0f;

        public event Action OnPresent;
        public event Action OnAbsent;

        private IHeadPoseSource source;
        private double lastTs;
        private volatile bool gotPoseFlag;
        private bool isPresent;

        public bool IsPresent => isPresent;

        public void Initialize(IHeadPoseSource source)
        {
            if (this.source != null) this.source.OnPose -= HandlePose;
            this.source = source;
            if (this.source != null) this.source.OnPose += HandlePose;
        }

        private void OnDestroy()
        {
            if (source != null) source.OnPose -= HandlePose;
        }

        // May be raised off the main thread (UDP sources) — keep it trivial.
        private void HandlePose(HeadPose pose)
        {
            gotPoseFlag = true;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;

            if (gotPoseFlag)
            {
                gotPoseFlag = false;
                lastTs = now;
                if (!isPresent)
                {
                    isPresent = true;
                    OnPresent?.Invoke();
                }
            }

            if (isPresent && (now - lastTs) > absenceTimeout)
            {
                isPresent = false;
                OnAbsent?.Invoke();
            }
        }
    }
}
