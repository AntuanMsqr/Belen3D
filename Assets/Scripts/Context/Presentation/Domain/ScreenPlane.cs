using UnityEngine;

namespace Belen.Presentation.Domain
{
    // Physical screen plane in world space: a center, right/up basis, and metric size.
    public readonly struct ScreenPlane
    {
        public readonly Vector3 center;
        public readonly Vector3 right;   // raw right (pre-orthonormalize)
        public readonly Vector3 up;      // raw up
        public readonly Vector3 forward; // screen normal hint (out of screen)
        public readonly float width;     // meters
        public readonly float height;    // meters

        public ScreenPlane(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, float width, float height)
        {
            this.center = center;
            this.right = right;
            this.up = up;
            this.forward = forward;
            this.width = width;
            this.height = height;
        }
    }

    // Result of an off-axis computation. projection is the CPU/OpenGL-style matrix;
    // the Infrastructure View converts it with GL.GetGPUProjectionMatrix before assigning.
    public readonly struct OffAxisResult
    {
        public readonly bool valid;
        public readonly Matrix4x4 projection;
        public readonly Matrix4x4 worldToCamera;

        public OffAxisResult(bool valid, Matrix4x4 projection, Matrix4x4 worldToCamera)
        {
            this.valid = valid;
            this.projection = projection;
            this.worldToCamera = worldToCamera;
        }

        public static readonly OffAxisResult Invalid = new OffAxisResult(false, Matrix4x4.identity, Matrix4x4.identity);
    }
}
