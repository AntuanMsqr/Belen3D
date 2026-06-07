using UnityEngine;

namespace Hcp.HeadTracking.Domain
{
    // Result of mapping a head pose to a camera placement, expressed as plain data.
    // The Application layer produces this; an Infrastructure View applies it to a Transform.
    // It must never expose a Transform/Camera.
    public readonly struct CameraTarget
    {
        public readonly MotionMode mode;

        // Direct mode
        public readonly Vector3 localPosition;
        public readonly Quaternion localRotation;
        public readonly bool applyRotation;

        // Orbit mode (degrees, degrees, meters)
        public readonly float yaw;
        public readonly float pitch;
        public readonly float distance;
        public readonly bool keepHorizon;

        private CameraTarget(MotionMode mode, Vector3 localPosition, Quaternion localRotation, bool applyRotation,
                             float yaw, float pitch, float distance, bool keepHorizon)
        {
            this.mode = mode;
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            this.applyRotation = applyRotation;
            this.yaw = yaw;
            this.pitch = pitch;
            this.distance = distance;
            this.keepHorizon = keepHorizon;
        }

        public static CameraTarget Direct(Vector3 localPosition, Quaternion localRotation, bool applyRotation)
        {
            return new CameraTarget(MotionMode.Direct, localPosition, localRotation, applyRotation,
                                    0f, 0f, 0f, true);
        }

        public static CameraTarget Orbit(float yaw, float pitch, float distance, bool keepHorizon)
        {
            return new CameraTarget(MotionMode.OrbitTarget, Vector3.zero, Quaternion.identity, false,
                                    yaw, pitch, distance, keepHorizon);
        }
    }
}
