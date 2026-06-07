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
        private HeadTrackingController controller;
        private Transform cameraPivot;
        private Transform orbitTarget;

        public void Initialize(HeadTrackingController controller, Transform cameraPivot, Transform orbitTarget)
        {
            this.controller = controller;
            this.cameraPivot = cameraPivot;
            this.orbitTarget = orbitTarget;
        }

        private void OnEnable()
        {
            controller?.ResetOrbit();
        }

        private void Update()
        {
            if (controller == null || cameraPivot == null) return;

            if (controller.TryTick(Time.deltaTime, out var target))
            {
                Apply(target);
            }
        }

        private void Apply(in CameraTarget t)
        {
            if (t.mode == MotionMode.Direct)
            {
                cameraPivot.localPosition = t.localPosition;
                cameraPivot.localRotation = t.localRotation; // base + optional head delta, baked in service
                return;
            }

            // OrbitTarget
            if (orbitTarget == null) return;
            var rot = Quaternion.Euler(t.pitch, t.yaw, 0f);
            var offset = rot * new Vector3(0f, 0f, -t.distance);
            var camPos = orbitTarget.position + offset;
            cameraPivot.position = camPos;
            var up = t.keepHorizon ? Vector3.up : cameraPivot.up;
            cameraPivot.rotation = Quaternion.LookRotation(orbitTarget.position - camPos, up);
        }
    }
}
