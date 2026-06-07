using System.Collections.Generic;
using UnityEngine;
using Hcp.Presentation.Application;

namespace Hcp.Presentation.Infrastructure
{
    // View: turns the SpatialLayoutService into scene geometry.
    //  - drives the ScreenCenter transform (monitor pose) and pushes width/height into
    //    OffAxisCameraView  -> the off-axis projection updates live (View stays untouched);
    //  - shows a flattened cube as the monitor plus markers for each camera and the user;
    //  - proxies carry colliders + SpatialTarget so RuntimeGizmoView can pick/drag them,
    //    and PushWorldTransform/PushMonitorScale write edits back to the service.
    public class SpatialProxyView : MonoBehaviour
    {
        private const float MonitorThickness = 0.02f;

        // Layer for the editor visuals (cube + markers) so the preview camera can exclude them.
        public int visualLayer = 0;

        private SpatialLayoutService _service;
        private Transform _screenCenter;
        private OffAxisCameraView _offAxis;
        private Transform _headPivot;

        private Transform _monitor;
        private Transform _userMarker;
        private readonly List<Transform> _cameraMarkers = new List<Transform>();

        public Transform ScreenCenter => _screenCenter;

        public void Initialize(SpatialLayoutService service, Transform screenCenter,
                               OffAxisCameraView offAxis, Transform headPivot)
        {
            _service = service;
            _screenCenter = screenCenter;
            _offAxis = offAxis;
            _headPivot = headPivot;

            EnsureVisuals();
            if (_service != null)
            {
                _service.LayoutChanged += Apply;
                Apply();
            }
        }

        private void OnDestroy()
        {
            if (_service != null) _service.LayoutChanged -= Apply;
        }

        private void EnsureVisuals()
        {
            if (_monitor == null)
            {
                _monitor = MakePrimitive(PrimitiveType.Cube, "MonitorProxy", new Color(0.15f, 0.15f, 0.18f));
                if (_screenCenter != null) _monitor.SetParent(_screenCenter, false);
                Tag(_monitor, SpatialKind.Monitor, 0);
            }
            if (_userMarker == null)
            {
                _userMarker = MakePrimitive(PrimitiveType.Sphere, "UserMarker", new Color(0.2f, 0.8f, 1f));
                Tag(_userMarker, SpatialKind.User, 0);
            }
        }

        private void Apply()
        {
            if (_service == null) return;
            var layout = _service.Current;

            // Monitor pose -> ScreenCenter (the off-axis screen plane origin).
            if (_screenCenter != null)
                _screenCenter.SetPositionAndRotation(layout.monitorPosition, Quaternion.Euler(layout.monitorEuler));

            // Flattened cube sized to the physical monitor (child of ScreenCenter).
            if (_monitor != null)
                _monitor.localScale = new Vector3(layout.monitorWidth, layout.monitorHeight, MonitorThickness);

            // Push physical size into the off-axis camera (it reads these each LateUpdate).
            if (_offAxis != null)
            {
                _offAxis.screenWidth = layout.monitorWidth;
                _offAxis.screenHeight = layout.monitorHeight;
            }

            // Camera markers (monitor-relative). Rebuild only when the count changes.
            SyncCameraMarkers(layout.cameras.Count);
            for (int i = 0; i < layout.cameras.Count; i++)
            {
                var cam = layout.cameras[i];
                _cameraMarkers[i].position = ToWorld(cam.position);
                _cameraMarkers[i].rotation = Quaternion.Euler(layout.monitorEuler) * Quaternion.Euler(cam.euler);
                _cameraMarkers[i].localScale = Vector3.one * (i == layout.activeCamera ? 0.05f : 0.035f);
            }

            // User reference marker + best-effort neutral viewpoint (head/eye pivot).
            if (_userMarker != null)
            {
                _userMarker.position = ToWorld(layout.userReferencePosition);
                _userMarker.localScale = Vector3.one * 0.04f;
            }
            if (_headPivot != null)
                _headPivot.position = ToWorld(layout.userReferencePosition);
        }

        // --- Writeback used by RuntimeGizmoView -------------------------------------

        // Move/rotate edit: world transform of the proxy -> layout (converting camera/user
        // back to monitor-relative space).
        public void PushWorldTransform(SpatialTarget target, Vector3 worldPos, Quaternion worldRot)
        {
            if (_service == null || target == null) return;
            switch (target.kind)
            {
                case SpatialKind.Monitor:
                    _service.SetMonitorPosition(worldPos);
                    _service.SetMonitorEuler(worldRot.eulerAngles);
                    break;
                case SpatialKind.Camera:
                {
                    var localPos = _screenCenter.InverseTransformPoint(worldPos);
                    var localEuler = (Quaternion.Inverse(_screenCenter.rotation) * worldRot).eulerAngles;
                    _service.SetCameraOffset(target.index, localPos, localEuler);
                    break;
                }
                case SpatialKind.User:
                {
                    var localPos = _screenCenter.InverseTransformPoint(worldPos);
                    _service.SetUserReference(localPos, _service.Current.userReferenceDistance);
                    break;
                }
            }
        }

        public void PushMonitorScale(float width, float height) => _service?.SetMonitorSize(width, height);

        private Vector3 ToWorld(Vector3 monitorLocal)
            => _screenCenter != null ? _screenCenter.TransformPoint(monitorLocal)
                                     : _service.Current.monitorPosition + Quaternion.Euler(_service.Current.monitorEuler) * monitorLocal;

        private void SyncCameraMarkers(int count)
        {
            while (_cameraMarkers.Count < count)
            {
                int idx = _cameraMarkers.Count;
                var m = MakePrimitive(PrimitiveType.Sphere, $"CameraMarker_{idx}", new Color(1f, 0.7f, 0.2f));
                Tag(m, SpatialKind.Camera, idx);
                _cameraMarkers.Add(m);
            }
            while (_cameraMarkers.Count > count)
            {
                int last = _cameraMarkers.Count - 1;
                if (_cameraMarkers[last] != null) Destroy(_cameraMarkers[last].gameObject);
                _cameraMarkers.RemoveAt(last);
            }
        }

        private static void Tag(Transform t, SpatialKind kind, int index)
        {
            var st = t.gameObject.GetComponent<SpatialTarget>();
            if (st == null) st = t.gameObject.AddComponent<SpatialTarget>();
            st.kind = kind;
            st.index = index;
        }

        private Transform MakePrimitive(PrimitiveType type, string n, Color color)
        {
            var go = GameObject.CreatePrimitive(type); // keeps its collider for gizmo picking
            go.name = n;
            go.layer = visualLayer;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r.material != null) r.material.color = color;
            }
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
