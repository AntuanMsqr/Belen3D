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
    // The syncing guard prevents the refresh->callback->mutate feedback loop.
    [RequireComponent(typeof(UIDocument))]
    public class SpatialEditorView : MonoBehaviour
    {
        private SpatialLayoutService service;
        private RuntimeGizmoView gizmo;
        private UIDocument doc;
        private bool bound;
        private bool syncing;

        private FloatField width, height, userDist;
        private Vec3Binding monPos, monRot, camPos, camRot, userPos;
        private DropdownField camIndex;
        private Label selection;

        // A Vector3 built from three FloatFields named "<base>-x/-y/-z" in the UXML.
        // Keeps X/Y/Z color + layout fully in USS while exposing a Vector3 to logic.
        private sealed class Vec3Binding
        {
            private readonly FloatField x, y, z;

            public Vec3Binding(VisualElement root, string baseName)
            {
                x = root.Q<FloatField>(baseName + "-x");
                y = root.Q<FloatField>(baseName + "-y");
                z = root.Q<FloatField>(baseName + "-z");
            }

            public Vector3 Value
            {
                get => new Vector3(x?.value ?? 0f, y?.value ?? 0f, z?.value ?? 0f);
                set
                {
                    if (x != null) x.value = value.x;
                    if (y != null) y.value = value.y;
                    if (z != null) z.value = value.z;
                }
            }

            public void OnChanged(System.Action cb)
            {
                x?.RegisterValueChangedCallback(_ => cb());
                y?.RegisterValueChangedCallback(_ => cb());
                z?.RegisterValueChangedCallback(_ => cb());
            }
        }

        // Called by the Bootstrap after AddComponent. The UIDocument is added first, so its
        // rootVisualElement is already built here; bind everything now.
        public void Initialize(SpatialLayoutService service, RuntimeGizmoView gizmo = null)
        {
            this.service = service;
            this.gizmo = gizmo;
            Bind();
        }

        private void OnEnable() => doc = GetComponent<UIDocument>();

        private void OnDisable()
        {
            if (service != null) service.LayoutChanged -= Refresh;
            if (gizmo != null) gizmo.SelectionChanged -= OnSelectionChanged;
            bound = false;
        }

        private void Bind()
        {
            if (bound || service == null) return;
            if (doc == null) doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;

            width = root.Q<FloatField>("field-width");
            height = root.Q<FloatField>("field-height");
            monPos = new Vec3Binding(root, "field-mon-pos");
            monRot = new Vec3Binding(root, "field-mon-rot");
            camIndex = root.Q<DropdownField>("field-cam-index");
            camPos = new Vec3Binding(root, "field-cam-pos");
            camRot = new Vec3Binding(root, "field-cam-rot");
            userPos = new Vec3Binding(root, "field-user-pos");
            userDist = root.Q<FloatField>("field-user-dist");

            width?.RegisterValueChangedCallback(_ => PushMonitorSize());
            height?.RegisterValueChangedCallback(_ => PushMonitorSize());
            monPos.OnChanged(() => { if (!syncing) service?.SetMonitorPosition(monPos.Value); });
            monRot.OnChanged(() => { if (!syncing) service?.SetMonitorEuler(monRot.Value); });
            camPos.OnChanged(PushCameraOffset);
            camRot.OnChanged(PushCameraOffset);
            camIndex?.RegisterValueChangedCallback(_ => { if (!syncing) service?.SetActiveCamera(camIndex.index); });
            userPos.OnChanged(PushUser);
            userDist?.RegisterValueChangedCallback(_ => PushUser());

            root.Q<Button>("btn-add-cam")?.RegisterCallback<ClickEvent>(_ => service?.AddCamera());
            root.Q<Button>("btn-save")?.RegisterCallback<ClickEvent>(_ => service?.Save());
            root.Q<Button>("btn-reset")?.RegisterCallback<ClickEvent>(_ => service?.ResetToDefaults());

            // Gizmo mode buttons + selection label.
            selection = root.Q<Label>("label-selection");
            root.Q<Button>("btn-move")?.RegisterCallback<ClickEvent>(_ => gizmo?.SetMode(GizmoMode.Move));
            root.Q<Button>("btn-rotate")?.RegisterCallback<ClickEvent>(_ => gizmo?.SetMode(GizmoMode.Rotate));
            root.Q<Button>("btn-scale")?.RegisterCallback<ClickEvent>(_ => gizmo?.SetMode(GizmoMode.Scale));

            // Clicking a panel section header selects the matching scene element.
            root.Q<Label>("sec-monitor")?.RegisterCallback<ClickEvent>(_ => gizmo?.Select(SpatialKind.Monitor));
            root.Q<Label>("sec-camera")?.RegisterCallback<ClickEvent>(_ => gizmo?.Select(SpatialKind.Camera, service != null ? service.Current.activeCamera : 0));
            root.Q<Label>("sec-user")?.RegisterCallback<ClickEvent>(_ => gizmo?.Select(SpatialKind.User));

            if (gizmo != null) gizmo.SelectionChanged += OnSelectionChanged;

            service.LayoutChanged += Refresh;
            bound = true;
            Refresh();
        }

        private void OnSelectionChanged(string text)
        {
            if (selection != null) selection.text = $"Sel: {text}";
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.f2Key.wasPressedThisFrame && doc != null)
            {
                var root = doc.rootVisualElement;
                root.style.display = root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            }
#endif
        }

        private void PushMonitorSize()
        {
            if (syncing || service == null || width == null || height == null) return;
            service.SetMonitorSize(width.value, height.value);
        }

        private void PushCameraOffset()
        {
            if (syncing || service == null) return;
            service.SetCameraOffset(service.Current.activeCamera, camPos.Value, camRot.Value);
        }

        private void PushUser()
        {
            if (syncing || service == null) return;
            service.SetUserReference(userPos.Value, userDist.value);
        }

        private void Refresh()
        {
            if (service == null) return;
            var layout = service.Current;
            // try/finally so a mid-refresh exception can never leave syncing stuck true,
            // which would silently swallow every later field edit (the !syncing guard).
            syncing = true;
            try
            {
                if (width != null) width.value = layout.monitorWidth;
                if (height != null) height.value = layout.monitorHeight;
                monPos.Value = layout.monitorPosition;
                monRot.Value = layout.monitorEuler;

                if (camIndex != null)
                {
                    camIndex.choices = layout.cameras.Select(c => c.label).ToList();
                    camIndex.index = Mathf.Clamp(layout.activeCamera, 0, Mathf.Max(0, layout.cameras.Count - 1));
                }
                int ai = layout.activeCamera;
                if (ai >= 0 && ai < layout.cameras.Count)
                {
                    camPos.Value = layout.cameras[ai].position;
                    camRot.Value = layout.cameras[ai].euler;
                }

                userPos.Value = layout.userReferencePosition;
                if (userDist != null) userDist.value = layout.userReferenceDistance;
            }
            finally
            {
                syncing = false;
            }
        }
    }
}
