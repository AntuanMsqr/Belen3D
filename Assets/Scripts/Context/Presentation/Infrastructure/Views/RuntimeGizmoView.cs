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
        private Camera cam;
        private SpatialLayoutService service;
        private SpatialProxyView proxy;

        private SpatialTarget selected;
        private GizmoMode mode = GizmoMode.Move;

        // Layer for gizmo handles so the preview camera can exclude them.
        public int handleLayer = 0;

        // Gizmo geometry
        private Transform root, moveRoot, rotateRoot, scaleRoot;

        // Drag state
        private GizmoHandle activeHandle;
        private Vector3 dragStartPos;
        private Quaternion dragStartRot;
        private Vector3 dragAxisWorld;
        private float dragStartT;
        private Vector3 rotStartDir;
        private float scaleStartW, scaleStartH;

        // Notifies the UI: "Monitor", "Cam 1", "Usuario" or "" (none) + mode.
        public event Action<string> SelectionChanged;
        public GizmoMode Mode => mode;

        public void Initialize(Camera cam, SpatialLayoutService service, SpatialProxyView proxy)
        {
            this.cam = cam;
            this.service = service;
            this.proxy = proxy;
            BuildGizmo();
            SetSelected(null);
        }

        // Select a scene target by kind (used by the UI panel: clicking a section header
        // selects its equivalent proxy in the scene). index only matters for cameras.
        public void Select(SpatialKind kind, int index = 0)
        {
            SpatialTarget match = null;
            foreach (var t in FindObjectsByType<SpatialTarget>(FindObjectsSortMode.None))
            {
                if (t.kind != kind) continue;
                if (kind == SpatialKind.Camera && t.index != index) continue;
                match = t;
                break;
            }
            if (match != null) SetSelected(match);
        }

        public void SetMode(GizmoMode mode)
        {
            this.mode = mode;
            if (this.mode == GizmoMode.Scale && (selected == null || selected.kind != SpatialKind.Monitor))
                this.mode = GizmoMode.Move; // scale only applies to the monitor
            RefreshGizmoVisibility();
            RaiseSelectionChanged();
        }

        private void Update()
        {
            if (cam == null) return;
            ReadInput(out var mousePos, out var down, out var held, out var up,
                      out var kMove, out var kRot, out var kScale);

            if (kMove) SetMode(GizmoMode.Move);
            if (kRot) SetMode(GizmoMode.Rotate);
            if (kScale) SetMode(GizmoMode.Scale);

            var ray = RayFromMouse(mousePos);

            if (down)
            {
                if (selected != null && TryPickHandle(ray, out var handle))
                    BeginDrag(handle, ray);
                else
                    SetSelected(PickTarget(ray));
            }
            else if (held && activeHandle != null)
            {
                Drag(ray);
            }
            else if (up)
            {
                activeHandle = null;
            }

            UpdateGizmoTransform();
        }

        // --- Selection --------------------------------------------------------------

        private void SetSelected(SpatialTarget t)
        {
            selected = t;
            activeHandle = null;
            if (selected == null || selected.kind != SpatialKind.Monitor) // keep scale valid
                if (mode == GizmoMode.Scale) mode = GizmoMode.Move;
            RefreshGizmoVisibility();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            string name = selected == null ? "—"
                : selected.kind == SpatialKind.Monitor ? "Monitor"
                : selected.kind == SpatialKind.User ? "Usuario"
                : $"Cam {selected.index}";
            SelectionChanged?.Invoke($"{name}  [{mode}]");
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
            activeHandle = handle;
            dragStartPos = selected.transform.position;
            dragStartRot = selected.transform.rotation;
            dragAxisWorld = AxisWorld(handle);

            if (handle.type == HandleType.RotateAxis)
            {
                if (PlaneHit(ray, dragStartPos, dragAxisWorld, out var hit))
                    rotStartDir = Vector3.ProjectOnPlane(hit - dragStartPos, dragAxisWorld).normalized;
            }
            else if (handle.type == HandleType.ScaleAxis)
            {
                scaleStartW = service.Current.monitorWidth;
                scaleStartH = service.Current.monitorHeight;
                dragStartT = ClosestT(ray, dragStartPos, dragAxisWorld);
            }
            else // MoveAxis
            {
                dragStartT = ClosestT(ray, dragStartPos, dragAxisWorld);
            }
        }

        private void Drag(Ray ray)
        {
            switch (activeHandle.type)
            {
                case HandleType.MoveAxis:
                {
                    float t = ClosestT(ray, dragStartPos, dragAxisWorld);
                    var newPos = dragStartPos + dragAxisWorld * (t - dragStartT);
                    proxy.PushWorldTransform(selected, newPos, dragStartRot);
                    break;
                }
                case HandleType.RotateAxis:
                {
                    if (PlaneHit(ray, dragStartPos, dragAxisWorld, out var hit))
                    {
                        var dir = Vector3.ProjectOnPlane(hit - dragStartPos, dragAxisWorld).normalized;
                        float ang = Vector3.SignedAngle(rotStartDir, dir, dragAxisWorld);
                        var newRot = Quaternion.AngleAxis(ang, dragAxisWorld) * dragStartRot;
                        proxy.PushWorldTransform(selected, dragStartPos, newRot);
                    }
                    break;
                }
                case HandleType.ScaleAxis:
                {
                    float t = ClosestT(ray, dragStartPos, dragAxisWorld);
                    float delta = t - dragStartT;
                    if (activeHandle.axis == 0)
                        proxy.PushMonitorScale(Mathf.Max(0.02f, scaleStartW + 2f * delta), service.Current.monitorHeight);
                    else
                        proxy.PushMonitorScale(service.Current.monitorWidth, Mathf.Max(0.02f, scaleStartH + 2f * delta));
                    break;
                }
            }
        }

        private Vector3 AxisWorld(GizmoHandle h)
        {
            if (h.type == HandleType.ScaleAxis && selected != null)
            {
                // monitor-local axes
                var rot = selected.transform.rotation;
                return h.axis == 0 ? rot * Vector3.right : rot * Vector3.up;
            }
            // global world axes for move/rotate
            return h.axis == 0 ? Vector3.right : h.axis == 1 ? Vector3.up : Vector3.forward;
        }

        // --- Gizmo geometry ---------------------------------------------------------

        private void BuildGizmo()
        {
            root = new GameObject("Gizmo").transform;
            root.SetParent(transform, false);

            moveRoot = NewChild(root, "Move");
            rotateRoot = NewChild(root, "Rotate");
            scaleRoot = NewChild(root, "Scale");

            var red = new Color(0.95f, 0.3f, 0.3f);
            var green = new Color(0.4f, 0.9f, 0.4f);
            var blue = new Color(0.4f, 0.6f, 1f);
            var yellow = new Color(1f, 0.85f, 0.25f);

            // Move arrows (shaft + tip) along X/Y/Z
            BuildArrow(moveRoot, 0, Vector3.right, Quaternion.Euler(0, 0, -90f), red);
            BuildArrow(moveRoot, 1, Vector3.up, Quaternion.identity, green);
            BuildArrow(moveRoot, 2, Vector3.forward, Quaternion.Euler(90f, 0, 0), blue);

            // Rotate rings (normal = axis)
            BuildRing(rotateRoot, 0, Quaternion.Euler(0, 0, 90f), red);
            BuildRing(rotateRoot, 1, Quaternion.identity, green);
            BuildRing(rotateRoot, 2, Quaternion.Euler(90f, 0, 0), blue);

            // Scale handles for monitor width (X) / height (Y)
            BuildScaleHandle(scaleRoot, 0, new Vector3(0.9f, 0, 0), yellow);
            BuildScaleHandle(scaleRoot, 1, new Vector3(0, 0.9f, 0), yellow);

            SetLayerRecursive(root, handleLayer);
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
            if (root == null) return;
            if (selected == null) { root.gameObject.SetActive(false); return; }

            var t = selected.transform;
            // Keep the origin fixed while dragging so the axis line stays stable.
            if (activeHandle == null) root.position = t.position;
            root.rotation = Quaternion.identity; // move/rotate use world axes

            // Scale handles follow the monitor orientation.
            if (scaleRoot != null) scaleRoot.rotation = t.rotation;

            // Constant-ish on-screen size.
            float dist = Vector3.Distance(cam.transform.position, root.position);
            if (activeHandle == null)
                root.localScale = Vector3.one * Mathf.Max(0.05f, dist * 0.16f);
        }

        private void RefreshGizmoVisibility()
        {
            if (root == null) return;
            bool any = selected != null;
            root.gameObject.SetActive(any);
            if (!any) return;
            bool isMonitor = selected.kind == SpatialKind.Monitor;
            moveRoot.gameObject.SetActive(mode == GizmoMode.Move);
            rotateRoot.gameObject.SetActive(mode == GizmoMode.Rotate);
            scaleRoot.gameObject.SetActive(mode == GizmoMode.Scale && isMonitor);
        }

        // --- Math helpers -----------------------------------------------------------

        private Ray RayFromMouse(Vector2 sp)
        {
            // Build the ray from the camera's actual matrices so the off-axis projection
            // (custom projectionMatrix) is respected — Camera.ScreenPointToRay can't.
            var vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            var inv = vp.inverse;
            float nx = 2f * sp.x / Mathf.Max(1, cam.pixelWidth) - 1f;
            float ny = 2f * sp.y / Mathf.Max(1, cam.pixelHeight) - 1f;
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

        private static Shader gizmoShader;
        private static Shader GizmoShader =>
            gizmoShader != null ? gizmoShader : (gizmoShader =
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
