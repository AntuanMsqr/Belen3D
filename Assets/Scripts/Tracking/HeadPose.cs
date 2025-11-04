using UnityEngine;

namespace Belen.Tracking
{
    // Immutable snapshot of head pose in camera (tracking) coordinates
    public struct HeadPose
    {
        public readonly Vector3 position; // meters
        public readonly Vector3 eulerDegrees; // pitch, yaw, roll in degrees (x=pitch, y=yaw, z=roll)
        public readonly double timestamp; // seconds

        public HeadPose(Vector3 position, Vector3 eulerDegrees, double timestamp)
        {
            this.position = position;
            this.eulerDegrees = eulerDegrees;
            this.timestamp = timestamp;
        }

        public static HeadPose Lerp(HeadPose a, HeadPose b, float t)
        {
            return new HeadPose(
                Vector3.Lerp(a.position, b.position, t),
                Vector3.Lerp(a.eulerDegrees, b.eulerDegrees, t),
                Mathf.Lerp((float)a.timestamp, (float)b.timestamp, t)
            );
        }
    }
}

