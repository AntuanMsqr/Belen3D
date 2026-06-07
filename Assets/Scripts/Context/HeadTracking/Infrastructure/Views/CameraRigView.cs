using UnityEngine;
using Hcp.HeadTracking.Domain;
using Hcp.HeadTracking.Application;

namespace Hcp.HeadTracking.Infrastructure
{
    // The single per-frame View for head tracking: pumps the controller's Tick and applies
    // the resulting CameraTarget to the camera pivot Transform. The Application layer never
    // sees a Transform — only the plain CameraTarget produced here is consumed.
    public class CameraRigView : MonoBehaviour
    {
        private HeadTrackingController _controller;
        private Transform _cameraPivot;
        private Transform _orbitTarget;

        public void Initialize(HeadTrackingController controller, Transform cameraPivot, Transform orbitTarget)
        {
            _controller = controller;
            _cameraPivot = cameraPivot;
            _orbitTarget = orbitTarget;
        }

        private void OnEnable()
        {
            _controller?.ResetOrbit();
        }

        private void Update()
        {
            if (_controller == null || _cameraPivot == null) return;

            if (_controller.TryTick(Time.deltaTime, out var target))
            {
                Apply(target);
            }
        }

        private void Apply(in CameraTarget t)
        {
            if (t.mode == MotionMode.Direct)
            {
                _cameraPivot.localPosition = t.localPosition;
                _cameraPivot.localRotation = t.localRotation; // base + optional head delta, baked in service
                return;
            }

            // OrbitTarget
            if (_orbitTarget == null) return;
            var rot = Quaternion.Euler(t.pitch, t.yaw, 0f);
            var offset = rot * new Vector3(0f, 0f, -t.distance);
            var camPos = _orbitTarget.position + offset;
            _cameraPivot.position = camPos;
            var up = t.keepHorizon ? Vector3.up : _cameraPivot.up;
            _cameraPivot.rotation = Quaternion.LookRotation(_orbitTarget.position - camPos, up);
        }
    }
}
