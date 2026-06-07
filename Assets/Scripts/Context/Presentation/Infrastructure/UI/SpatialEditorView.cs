using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Hcp.Presentation.Application;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hcp.Presentation.Infrastructure
{
    // UI Toolkit screen for the runtime spatial editor. Binds numeric fields to the
    // SpatialLayoutService and refreshes from it on LayoutChanged. Toggle with F2.
    // The _syncing guard prevents the refresh->callback->mutate feedback loop.
    [RequireComponent(typeof(UIDocument))]
    public class SpatialEditorView : MonoBehaviour
    {
        private SpatialLayoutService _service;
        private RuntimeGizmoView _gizmo;
        private UIDocument _doc;
        private bool _bound;
        private bool _syncing;

        private FloatField _width, _height, _userDist;
        private Vector3Field _monPos, _monRot, _camPos, _camRot, _userPos;
        private DropdownField _camIndex;
        private Label _selection;

        // Called by the Bootstrap after AddComponent. The UIDocument is added first, so its
        // rootVisualElement is already built here; bind everything now.
        public void Initialize(SpatialLayoutService service, RuntimeGizmoView gizmo = null)
        {
            _service = service;
            _gizmo = gizmo;
            Bind();
        }

        private void OnEnable() => _doc = GetComponent<UIDocument>();

        private void OnDisable()
        {
            if (_service != null) _service.LayoutChanged -= Refresh;
            if (_gizmo != null) _gizmo.SelectionChanged -= OnSelectionChanged;
            _bound = false;
        }

        private void Bind()
        {
            if (_bound || _service == null) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null) return;

            _width = root.Q<FloatField>("field-width");
            _height = root.Q<FloatField>("field-height");
            _monPos = root.Q<Vector3Field>("field-mon-pos");
            _monRot = root.Q<Vector3Field>("field-mon-rot");
            _camIndex = root.Q<DropdownField>("field-cam-index");
            _camPos = root.Q<Vector3Field>("field-cam-pos");
            _camRot = root.Q<Vector3Field>("field-cam-rot");
            _userPos = root.Q<Vector3Field>("field-user-pos");
            _userDist = root.Q<FloatField>("field-user-dist");

            _width?.RegisterValueChangedCallback(_ => PushMonitorSize());
            _height?.RegisterValueChangedCallback(_ => PushMonitorSize());
            _monPos?.RegisterValueChangedCallback(e => { if (!_syncing) _service?.SetMonitorPosition(e.newValue); });
            _monRot?.RegisterValueChangedCallback(e => { if (!_syncing) _service?.SetMonitorEuler(e.newValue); });
            _camPos?.RegisterValueChangedCallback(_ => PushCameraOffset());
            _camRot?.RegisterValueChangedCallback(_ => PushCameraOffset());
            _camIndex?.RegisterValueChangedCallback(_ => { if (!_syncing) _service?.SetActiveCamera(_camIndex.index); });
            _userPos?.RegisterValueChangedCallback(_ => PushUser());
            _userDist?.RegisterValueChangedCallback(_ => PushUser());

            root.Q<Button>("btn-add-cam")?.RegisterCallback<ClickEvent>(_ => _service?.AddCamera());
            root.Q<Button>("btn-save")?.RegisterCallback<ClickEvent>(_ => _service?.Save());
            root.Q<Button>("btn-reset")?.RegisterCallback<ClickEvent>(_ => _service?.ResetToDefaults());

            // Gizmo mode buttons + selection label.
            _selection = root.Q<Label>("label-selection");
            root.Q<Button>("btn-move")?.RegisterCallback<ClickEvent>(_ => _gizmo?.SetMode(GizmoMode.Move));
            root.Q<Button>("btn-rotate")?.RegisterCallback<ClickEvent>(_ => _gizmo?.SetMode(GizmoMode.Rotate));
            root.Q<Button>("btn-scale")?.RegisterCallback<ClickEvent>(_ => _gizmo?.SetMode(GizmoMode.Scale));
            if (_gizmo != null) _gizmo.SelectionChanged += OnSelectionChanged;

            _service.LayoutChanged += Refresh;
            _bound = true;
            Refresh();
        }

        private void OnSelectionChanged(string text)
        {
            if (_selection != null) _selection.text = $"Sel: {text}";
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.f2Key.wasPressedThisFrame && _doc != null)
            {
                var root = _doc.rootVisualElement;
                root.style.display = root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            }
#endif
        }

        private void PushMonitorSize()
        {
            if (_syncing || _service == null || _width == null || _height == null) return;
            _service.SetMonitorSize(_width.value, _height.value);
        }

        private void PushCameraOffset()
        {
            if (_syncing || _service == null) return;
            _service.SetCameraOffset(_service.Current.activeCamera, _camPos.value, _camRot.value);
        }

        private void PushUser()
        {
            if (_syncing || _service == null) return;
            _service.SetUserReference(_userPos.value, _userDist.value);
        }

        private void Refresh()
        {
            if (_service == null) return;
            var layout = _service.Current;
            _syncing = true;

            if (_width != null) _width.value = layout.monitorWidth;
            if (_height != null) _height.value = layout.monitorHeight;
            if (_monPos != null) _monPos.value = layout.monitorPosition;
            if (_monRot != null) _monRot.value = layout.monitorEuler;

            if (_camIndex != null)
            {
                _camIndex.choices = layout.cameras.Select(c => c.label).ToList();
                _camIndex.index = Mathf.Clamp(layout.activeCamera, 0, Mathf.Max(0, layout.cameras.Count - 1));
            }
            int ai = layout.activeCamera;
            if (ai >= 0 && ai < layout.cameras.Count)
            {
                if (_camPos != null) _camPos.value = layout.cameras[ai].position;
                if (_camRot != null) _camRot.value = layout.cameras[ai].euler;
            }

            if (_userPos != null) _userPos.value = layout.userReferencePosition;
            if (_userDist != null) _userDist.value = layout.userReferenceDistance;

            _syncing = false;
        }
    }
}
