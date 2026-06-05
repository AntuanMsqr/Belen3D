using UnityEngine;
using UnityEngine.UIElements;
using Belen.HeadTracking.Application;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Belen.Diagnostics.Infrastructure
{
    // UI Toolkit read-only overlay: head position, distance to screen, motion mode, FPS.
    // Toggle with F1.
    [RequireComponent(typeof(UIDocument))]
    public class DebugOverlayView : MonoBehaviour
    {
        private HeadTrackingController _controller;
        private Transform _eye;
        private Transform _screen;

        private UIDocument _doc;
        private Label _label;
        private float _fps;

        public void Initialize(HeadTrackingController controller, Transform eye, Transform screen)
        {
            _controller = controller;
            _eye = eye;
            _screen = screen;
        }

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc != null ? _doc.rootVisualElement : null;
            _label = root?.Q<Label>("overlay-label");
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.f1Key.wasPressedThisFrame && _doc != null)
            {
                var root = _doc.rootVisualElement;
                root.style.display = root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            }
#endif
            if (_label == null && _doc != null)
                _label = _doc.rootVisualElement?.Q<Label>("overlay-label");
            if (_label == null || _controller == null) return;

            float dt = Time.unscaledDeltaTime;
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(1e-4f, dt), 0.1f);

            var p = _controller.LastFilteredPosition;
            float dist = (_eye != null && _screen != null)
                ? Vector3.Dot(_eye.position - _screen.position, _screen.forward)
                : 0f;

            _label.text =
                $"head  {p.x:F2}, {p.y:F2}, {p.z:F2}\n" +
                $"dist  {dist:F2} m\n" +
                $"mode  {_controller.Calibration.motionMode}\n" +
                $"fps   {_fps:F0}";
        }
    }
}
