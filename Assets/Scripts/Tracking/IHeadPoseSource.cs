using System;

namespace Belen.Tracking
{
    public interface IHeadPoseSource
    {
        // Returns true if a new pose is available since last query
        bool TryGetLatest(out HeadPose pose);

        // Raised when a new pose sample arrives (optional to subscribe)
        event Action<HeadPose> OnPose;

        // Optional lifecycle for sources needing init/teardown
        void Start();
        void Stop();
    }
}

