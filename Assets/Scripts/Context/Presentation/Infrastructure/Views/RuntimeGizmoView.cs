using System;
using UnityEngine;
using Hcp.Presentation.Application;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hcp.Presentation.Infrastructure
{
    // Runtime transform gizmo (no third-party deps). Click a proxy to select it, then drag
    // axis handles to move/rotate, or the corner handles to resize the monitor. All edits go
    // through SpatialProxyView -> SpatialLayoutService, so the UI panel, persistence and the
    // off-axis projection stay in sync. Keys: W move, E rotate, R scale (monitor), Esc deselect.
    public class RuntimeGizmoView : MonoBehaviour
    {
        private Camera _cam;
        private SpatialLayoutService _service;
        private SpatialProxyView _proxy;

        private SpatialTarget _selected;
        private GizmoMode _mode = GizmoMode.Move;

        // Layer for gizmo handles so the preview camera can exclude them.
        public int handleLayer = 0;

        // Gizmo geometry
        private Transform _root, _moveRoot, _rotateRoot, _scaleRoot;

        // Drag state
        private GizmoHandle _activeHandle;
        private Vector3 _dragStartPos;
        private Quaternion _dragStartRot;
        private Vector3 _dragAxisWorld;
        private float _dragStartT;
        private Vector3 _rotStartDir;
        private float _scaleStartW, _scaleStartH;

        // Notifies the UI: "Monitor", "Cam 1", "Usuario" or "" (none) + mode.
        public event Action<string> SelectionChanged;
        public GizmoMode Mode => _mode;

        public void Initialize(Camera cam, SpatialLayoutService service, SpatialProxyView proxy)
        {
            _cam = cam;
            _service = service;
            _proxy = proxy;
            BuildGizmo();
            SetSelected(null);
        }

        public void SetMode(GizmoMode mode)
        {
            _mode = mode;
            if (_mode == GizmoMode.Scale && (_selected == null || _selected.kind != SpatialKind.Monitor))
                _mode = GizmoMode.Move; // scale only applies to the monitor
            RefreshGizmoVisibility();
            RaiseSelectionChanged();
        }

        private void Update()
        {
            if (_cam == null) return;
            ReadInput(out var mousePos, out var down, out var held, out var up,
                      out var kMove, out var kRot, out var kScale);

            if (kMove) SetMode(GizmoMode.Move);
            if (kRot) SetMode(GizmoMode.Rotate);
            if (kScale) SetMode(GizmoMode.Scale);

            var ray = RayFromMouse(mousePos);

            if (down)
            {
                if (_selected != null && TryPickHandle(ray, out var handle))
                    BeginDrag(handle, ray);
                else
                    SetSelected(PickTarget(ray));
            }
            else if (held && _activeHandle != null)
            {
                Drag(ray);
            }
            else if (up)
            {
                _activeHandle = null;
            }

            UpdateGizmoTransform();
        }

        // --- Selection --------------------------------------------------------------

        private void SetSelected(SpatialTarget t)
        {
            _selected = t;
            _activeHandle = null;
            if (_selected == null || _selected.kind != SpatialKind.Monitor) // keep scale valid
                if (_mode == GizmoMode.Scale) _mode = GizmoMode.Move;
            RefreshGizmoVisibility();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            string name = _selected == null ? "—"
                : _selected.kind == SpatialKind.Monitor ? "Monitor"
                : _selected.kind == SpatialKind.User ? "Usuario"
                : $"Cam {_selected.index}";
            SelectionChanged?.Invoke($"{name}  [{_mode}]");
        }

        private SpatialTarget PickTarget(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, 100f);
            SpatialTarget best = null;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var st = h.collider.GetComponentInParent<SpatialTarget>();
                if (st != null && h.distance < bestDist) { best = st; bestDist = h.distance; }
            }
            return best;
        }

        private bool TryPickHandle(Ray ray, out GizmoHandle handle)
        {
            handle = null;
            var hits = Physics.RaycastAll(ray, 100f);
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var gh = h.collider.GetComponentInParent<GizmoHandle>();
                if (gh != null && h.distance < bestDist) { handle = gh; bestDist = h.distance; }
            }
            return handle != null;
        }

        // --- Dragging ---------------------------------------------------------------

        private void BeginDrag(GizmoHandle handle, Ray ray)
        {
            _activeHandle = handle;
            _dragStartPos = _selected.transform.position;
            _dragStartRot = _selected.transform.rotation;
            _dragAxisWorld = AxisWorld(handle);

            if (handle.type == HandleType.RotateAxis)
            {
                if (PlaneHit(ray, _dragStartPos, _dragAxisWorld, out var hit))
                    _rotStartDir = Vector3.ProjectOnPlane(hit - _dragStartPos, _dragAxisWorld).normalized;
            }
            else if (handle.type == HandleType.ScaleAxis)
            {
                _scaleStartW = _service.Current.monitorWidth;
                _scaleStartH = _service.Current.monitorHeight;
                _dragStartT = ClosestT(ray, _dragStartPos, _dragAxisWorld);
            }
            else // MoveAxis
            {
                _dragStartT = ClosestT(ray, _dragStartPos, _dragAxisWorld);
            }
        }

        private void Drag(Ray ray)
        {
            switch (_activeHandle.type)
            {
                case HandleType.MoveAxis:
                {
                    float t = ClosestT(ray, _dragStartPos, _dragAxisWorld);
                    var newPos = _dragStartPos + _dragAxisWorld * (t - _dragStartT);
                    _proxy.PushWorldTransform(_selected, newPos, _dragStartRot);
                    break;
                }
                case HandleType.RotateAxis:
                {
                    if (PlaneHit(ray, _dragStartPos, _dragAxisWorld, out var hit))
                    {
                        var dir = Vector3.ProjectOnPlane(hit - _dragStartPos, _dragAxisWorld).normalized;
                        float ang = Vector3.SignedAngle(_rotStartDir, dir, _dragAxisWorld);
                        var newRot = Quaternion.AngleAxis(ang, _dragAxisWorld) * _dragStartRot;
                        _proxy.PushWorldTransform(_selected, _dragStartPos, newRot);
                    }
                    break;
                }
                case HandleType.ScaleAxis:
                {
                    float t = ClosestT(ray, _dragStartPos, _dragAxisWorld);
                    float delta = t - _dragStartT;
                    if (_activeHandle.axis == 0)
                        _proxy.PushMonitorScale(Mathf.Max(0.02f, _scaleStartW + 2f * delta), _service.Current.monitorHeight);
                    else
                        _proxy.PushMonitorScale(_service.Current.monitorWidth, Mathf.Max(0.02f, _scaleStartH + 2f * delta));
                    break;
                }
            }
        }

        private Vector3 AxisWorld(GizmoHandle h)
        {
            if (h.type == HandleType.ScaleAxis && _selected != null)
            {
                // monitor-local axes
                var rot = _selected.transform.rotation;
                return h.axis == 0 ? rot * Vector3.right : rot * Vector3.up;
            }
            // global world axes for move/rotate
            return h.axis == 0 ? Vector3.right : h.axis == 1 ? Vector3.up : Vector3.forward;
        }

        // --- Gizmo geometry ---------------------------------------------------------

        private void BuildGizmo()
        {
            _root = new GameObject("Gizmo").transform;
            _root.SetParent(transform, false);

            _moveRoot = NewChild(_root, "Move");
            _rotateRoot = NewChild(_root, "Rotate");
            _scaleRoot = NewChild(_root, "Scale");

            var red = new Color(0.95f, 0.3f, 0.3f);
            var green = new Color(0.4f, 0.9f, 0.4f);
            var blue = new Color(0.4f, 0.6f, 1f);
            var yellow = new Color(1f, 0.85f, 0.25f);

            // Move arrows (shaft + tip) along X/Y/Z
            BuildArrow(_moveRoot, 0, Vector3.right, Quaternion.Euler(0, 0, -90f), red);
            BuildArrow(_moveRoot, 1, Vector3.up, Quaternion.identity, green);
            BuildArrow(_moveRoot, 2, Vector3.forward, Quaternion.Euler(90f, 0, 0), blue);

            // Rotate rings (normal = axis)
            BuildRing(_rotateRoot, 0, Quaternion.Euler(0, 0, 90f), red);
            BuildRing(_rotateRoot, 1, Quaternion.identity, green);
            BuildRing(_rotateRoot, 2, Quaternion.Euler(90f, 0, 0), blue);

            // Scale handles for monitor width (X) / height (Y)
            BuildScaleHandle(_scaleRoot, 0, new Vector3(0.9f, 0, 0), yellow);
            BuildScaleHandle(_scaleRoot, 1, new Vector3(0, 0.9f, 0), yellow);

            SetLayerRecursive(_root, handleLayer);
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        private void BuildArrow(Transform parent, int axis, Vector3 dir, Quaternion alignYToAxis, Color c)
        {
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // default has CapsuleCollider
            shaft.name = $"Move{axis}";
            shaft.transform.SetParent(parent, false);
            shaft.transform.localRotation = alignYToAxis;
            shaft.transform.localPosition = dir * 0.5f;
            shaft.transform.localScale = new Vector3(0.045f, 0.5f, 0.045f);
            Paint(shaft, c);
            AddHandle(shaft, HandleType.MoveAxis, axis);

            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = $"MoveTip{axis}";
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = dir * 1f;
            tip.transform.localScale = Vector3.one * 0.12f;
            Paint(tip, c);
            AddHandle(tip, HandleType.MoveAxis, axis);
        }

        private void BuildRing(Transform parent, int axis, Quaternion rot, Color c)
        {
            var go = new GameObject($"Rotate{axis}");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = rot;
            var mesh = BuildTorus(0.8f, 0.03f, 48, 10);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Paint(go.AddComponent<MeshRenderer>(), c);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = false; // non-convex is fine for raycasts
            AddHandle(go, HandleType.RotateAxis, axis);
        }

        private void BuildScaleHandle(Transform parent, int axis, Vector3 pos, Color c)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Scale{axis}";
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = pos;
            cube.transform.localScale = Vector3.one * 0.14f;
            Paint(cube, c);
            AddHandle(cube, HandleType.ScaleAxis, axis);
        }

        private void UpdateGizmoTransform()
        {
            if (_root == null) return;
            if (_selected == null) { _root.gameObject.SetActive(false); return; }

            var t = _selected.transform;
            // Keep the origin fixed while dragging so the axis line stays stable.
            if (_activeHandle == null) _root.position = t.position;
            _root.rotation = Quaternion.identity; // move/rotate use world axes

            // Scale handles follow the monitor orientation.
            if (_scaleRoot != null) _scaleRoot.rotation = t.rotation;

            // Constant-ish on-screen size.
            float dist = Vector3.Distance(_cam.transform.position, _root.position);
            if (_activeHandle == null)
                _root.localScale = Vector3.one * Mathf.Max(0.05f, dist * 0.16f);
        }

        private void RefreshGizmoVisibility()
        {
            if (_root == null) return;
            bool any = _selected != null;
            _root.gameObject.SetActive(any);
            if (!any) return;
            bool isMonitor = _selected.kind == SpatialKind.Monitor;
            _moveRoot.gameObject.SetActive(_mode == GizmoMode.Move);
            _rotateRoot.gameObject.SetActive(_mode == GizmoMode.Rotate);
            _scaleRoot.gameObject.SetActive(_mode == GizmoMode.Scale && isMonitor);
        }

        // --- Math helpers -----------------------------------------------------------

        private Ray RayFromMouse(Vector2 sp)
        {
            // Build the ray from the camera's actual matrices so the off-axis projection
            // (custom projectionMatrix) is respected — Camera.ScreenPointToRay can't.
            var vp = _cam.projectionMatrix * _cam.worldToCameraMatrix;
            var inv = vp.inverse;
            float nx = 2f * sp.x / Mathf.Max(1, _cam.pixelWidth) - 1f;
            float ny = 2f * sp.y / Mathf.Max(1, _cam.pixelHeight) - 1f;
            Vector4 nearH = inv * new Vector4(nx, ny, -1f, 1f);
            Vector4 farH = inv * new Vector4(nx, ny, 1f, 1f);
            var near = new Vector3(nearH.x, nearH.y, nearH.z) / nearH.w;
            var far = new Vector3(farH.x, farH.y, farH.z) / farH.w;
            return new Ray(near, (far - near).normalized);
        }

        // Scalar t along the infinite line (p, dir) at the point closest to the ray.
        // Both r.direction and dir are unit vectors, so a=c=1 and t=(e-b*d)/(1-b^2).
        private static float ClosestT(Ray r, Vector3 p, Vector3 dir)
        {
            Vector3 w0 = r.origin - p;
            float b = Vector3.Dot(r.direction, dir);
            float d = Vector3.Dot(r.direction, w0);
            float e = Vector3.Dot(dir, w0);
            float denom = 1f - b * b;
            if (Mathf.Abs(denom) < 1e-6f) return 0f;
            return (e - b * d) / denom;
        }

        private static bool PlaneHit(Ray ray, Vector3 point, Vector3 normal, out Vector3 hit)
        {
            hit = Vector3.zero;
            float denom = Vector3.Dot(ray.direction, normal);
            if (Mathf.Abs(denom) < 1e-6f) return false;
            float t = Vector3.Dot(point - ray.origin, normal) / denom;
            if (t < 0f) return false;
            hit = ray.origin + ray.direction * t;
            return true;
        }

        private static Mesh BuildTorus(float radius, float tube, int seg, int sides)
        {
            var mesh = new Mesh { name = "GizmoTorus" };
            var verts = new Vector3[seg * sides];
            var tris = new int[seg * sides * 6];
            for (int i = 0; i < seg; i++)
            {
                float u = (float)i / seg * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(u) * radius, 0f, Mathf.Sin(u) * radius);
                for (int j = 0; j < sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    verts[i * sides + j] = center + dir * tube;
                }
            }
            int ti = 0;
            for (int i = 0; i < seg; i++)
            {
                int ni = (i + 1) % seg;
                for (int j = 0; j < sides; j++)
                {
                    int nj = (j + 1) % sides;
                    int a = i * sides + j, b = ni * sides + j, c = ni * sides + nj, d = i * sides + nj;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        // --- Misc -------------------------------------------------------------------

        private static Transform NewChild(Transform parent, string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void AddHandle(GameObject go, HandleType type, int axis)
        {
            var h = go.AddComponent<GizmoHandle>();
            h.type = type;
            h.axis = axis;
        }

        private static Shader _gizmoShader;
        private static Shader GizmoShader =>
            _gizmoShader != null ? _gizmoShader : (_gizmoShader =
                Shader.Find("Unlit/Color")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard"));

        private static void Paint(GameObject go, Color c) => Paint(go.GetComponent<Renderer>(), c);

        private static void Paint(Renderer r, Color c)
        {
            if (r == null) return;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var mat = GizmoShader != null ? new Material(GizmoShader) : null;
            if (mat == null) return;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            r.material = mat;
        }

        private void ReadInput(out Vector2 mousePos, out bool down, out bool held, out bool up,
                               out bool kMove, out bool kRot, out bool kScale)
        {
            mousePos = Vector2.zero; down = held = up = false;
            kMove = kRot = kScale = false;
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null)
            {
                mousePos = m.position.ReadValue();
                down = m.leftButton.wasPressedThisFrame;
                held = m.leftButton.isPressed;
                up = m.leftButton.wasReleasedThisFrame;
            }
            var k = Keyboard.current;
            if (k != null)
            {
                kMove = k.wKey.wasPressedThisFrame;
                kRot = k.eKey.wasPressedThisFrame;
                kScale = k.rKey.wasPressedThisFrame;
            }
#endif
        }
    }
}
