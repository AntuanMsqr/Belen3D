using UnityEngine;
using Hcp.Presentation.Domain;

namespace Hcp.Presentation.Application
{
    // Use case: produce off-axis matrices for a given eye + screen plane.
    public sealed class OffAxisProjectionService
    {
        public OffAxisResult Compute(Vector3 eye, in ScreenPlane plane, float nearClip, float farClip,
                                     bool flipRight, bool flipUp)
        {
            return OffAxisMath.Compute(eye, plane, nearClip, farClip, flipRight, flipUp);
        }
    }
}
