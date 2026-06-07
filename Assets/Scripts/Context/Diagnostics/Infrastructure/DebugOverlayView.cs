using UnityEngine;
using UnityEngine.UIElements;
using Hcp.HeadTracking.Application;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hcp.Diagnostics.Infrastructure
{
    // UI Toolkit read-only overlay: head position, distance to screen, motion mode, FPS.
    // Toggle with F1.
    [RequireComponent(typeof(UIDocument))]
    public class DebugOverlayView : MonoBehaviour
    {
        private HeadTrackingController controller;
        private Transform eye;
        private Transform screen;

        private UIDocument doc;
        private Label label;
        private float fps;

        public void Initialize(HeadTrackingController controller, Transform eye, Transform screen)
        {
            this.controller = controller;
            this.eye = eye;
            this.screen = screen;
        }

        private void OnEnable()
        {
            doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            label = root?.Q<Label>("overlay-label");
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.f1Key.wasPressedThisFrame && doc != null)
            {
                var root = doc.rootVisualElement;
                root.style.display = root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            }
#endif
            if (label == null && doc != null)
                label = doc.rootVisualElement?.Q<Label>("overlay-label");
            if (label == null || controller == null) return;

            float dt = Time.unscaledDeltaTime;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(1e-4f, dt), 0.1f);

            var p = controller.LastFilteredPosition;
            float dist = (eye != null && screen != null)
                ? Vector3.Dot(eye.position - screen.position, screen.forward)
                : 0f;

            label.text =
                $"head  {p.x:F2}, {p.y:F2}, {p.z:F2}\n" +
                $"dist  {dist:F2} m\n" +
                $"mode  {controller.Calibration.motionMode}\n" +
                $"fps   {fps:F0}";
        }
    }
}
