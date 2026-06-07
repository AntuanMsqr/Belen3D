using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hcp.Presentation.Infrastructure
{
    // Editor-mode orbital camera (HCP off). Right-drag orbits, wheel zooms, middle-drag pans.
    // Left mouse is left free for gizmo selection/drag. Drives its own camera Transform.
    [RequireComponent(typeof(Camera))]
    public class OrbitCameraView : MonoBehaviour
    {
        public Vector3 pivot = Vector3.zero;
        public float distance = 2.0f;
        public float yaw = 0f;
        public float pitch = 15f;

        public float orbitSpeed = 0.25f;
        public float panSpeed = 0.002f;
        public float zoomSpeed = 0.15f;

        public void Initialize(Vector3 pivot, float distance)
        {
            this.pivot = pivot;
            this.distance = distance;
            Apply();
        }

        private void LateUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null)
            {
                Vector2 delta = m.delta.ReadValue();
                if (m.rightButton.isPressed)
                {
                    yaw += delta.x * orbitSpeed;
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSpeed, -89f, 89f);
                }
                else if (m.middleButton.isPressed)
                {
                    var right = transform.right;
                    var up = transform.up;
                    pivot -= (right * delta.x + up * delta.y) * panSpeed * Mathf.Max(0.2f, distance);
                }
                float scroll = m.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    distance = Mathf.Clamp(distance * (1f - Mathf.Sign(scroll) * zoomSpeed), 0.1f, 50f);
            }
#endif
            Apply();
        }

        private void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = pivot - rot * Vector3.forward * distance;
            transform.rotation = rot;
        }
    }
}
