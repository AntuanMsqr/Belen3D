using UnityEngine;
using Hcp.Presentation.Domain;
using Hcp.Presentation.Application;

namespace Hcp.Presentation.Infrastructure
{
    // View: applies off-axis (HCP) matrices to a Camera each LateUpdate.
    // Assembles a ScreenPlane from the screenCenter Transform, calls the Application
    // service for the matrices, and converts the projection with GL.GetGPUProjectionMatrix.
    [RequireComponent(typeof(Camera))]
    public class OffAxisCameraView : MonoBehaviour
    {
        public Transform screenCenter;
        public Transform eyeTransform;
        public float screenWidth = 0.6f;
        public float screenHeight = 0.34f;
        public float nearClip = 0.01f;
        public float farClip = 100f;
        public bool flipScreenRight = false;
        public bool flipScreenUp = false;
        public bool enableOffAxis = true;

        private Camera _cam;
        private readonly OffAxisProjectionService _service = new OffAxisProjectionService();

        public void Initialize(Transform screen, Transform eye, float width, float height, float near, float far)
        {
            screenCenter = screen;
            eyeTransform = eye;
            screenWidth = width;
            screenHeight = height;
            nearClip = near;
            farClip = far;
        }

        private void OnEnable() => _cam = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (!enableOffAxis || _cam == null || screenCenter == null || eyeTransform == null)
                return;

            var plane = new ScreenPlane(
                screenCenter.position,
                screenCenter.right,
                screenCenter.up,
                screenCenter.forward,
                screenWidth,
                screenHeight);

            var result = _service.Compute(eyeTransform.position, plane, nearClip, farClip, flipScreenRight, flipScreenUp);

            if (!result.valid)
            {
                _cam.ResetProjectionMatrix();
                _cam.ResetWorldToCameraMatrix();
                return;
            }

            _cam.worldToCameraMatrix = result.worldToCamera;
            // worldToCamera already follows Unity's left-handed convention (built via
            // Scale(1,1,-1) * worldToLocal) and the projection is the standard OpenGL-convention
            // off-center frustum; Unity converts it to the platform GPU matrix internally.
            _cam.projectionMatrix = result.projection;
        }
    }
}
