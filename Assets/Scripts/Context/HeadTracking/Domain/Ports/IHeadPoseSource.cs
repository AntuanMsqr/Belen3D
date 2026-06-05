using System;

namespace Belen.HeadTracking.Domain
{
    // Port: a source of head poses (tracker, UDP, keyboard emulator, ...).
    public interface IHeadPoseSource
    {
        // Returns true if a pose is available; pose carries the latest sample.
        bool TryGetLatest(out HeadPose pose);

        // Raised when a new pose sample arrives (may fire off the main thread).
        event Action<HeadPose> OnPose;

        // Lifecycle for sources needing init/teardown.
        void Start();
        void Stop();
    }
}
