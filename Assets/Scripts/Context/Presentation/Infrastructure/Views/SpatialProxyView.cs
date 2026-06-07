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

        private SpatialLayoutService service;
        private Transform screenCenter;
        private OffAxisCameraView offAxis;
        private Transform headPivot;

        private Transform monitor;
        private Transform userMarker;
        private OffAxisFrameGizmo frameGizmo;
        private readonly List<Transform> cameraMarkers = new List<Transform>();

        public Transform ScreenCenter => screenCenter;

        public void Initialize(SpatialLayoutService service, Transform screenCenter,
                               OffAxisCameraView offAxis, Transform headPivot)
        {
            this.service = service;
            this.screenCenter = screenCenter;
            this.offAxis = offAxis;
            this.headPivot = headPivot;

            EnsureVisuals();
            if (this.service != null)
            {
                this.service.LayoutChanged += Apply;
                Apply();
            }
        }

        private void OnDestroy()
        {
            if (service != null) service.LayoutChanged -= Apply;
        }

        private void EnsureVisuals()
        {
            if (monitor == null)
            {
                monitor = MakePrimitive(PrimitiveType.Cube, "MonitorProxy", new Color(0.15f, 0.15f, 0.18f));
                if (screenCenter != null) monitor.SetParent(screenCenter, false);
                Tag(monitor, SpatialKind.Monitor, 0);
            }
            if (userMarker == null)
            {
                userMarker = MakePrimitive(PrimitiveType.Sphere, "UserMarker", new Color(0.2f, 0.8f, 1f));
                Tag(userMarker, SpatialKind.User, 0);
            }
            // "Ventana" (HCP window) Scene-view gizmo: screen rectangle + neutral eye + frustum
            // + framed region. Drawn at the screen center; synced with the layout in Apply().
            if (frameGizmo == null && screenCenter != null)
                frameGizmo = screenCenter.gameObject.AddComponent<OffAxisFrameGizmo>();
        }

        private void Apply()
        {
            if (service == null) return;
            var layout = service.Current;

            // Monitor pose -> ScreenCenter (the off-axis screen plane origin).
            if (screenCenter != null)
                screenCenter.SetPositionAndRotation(layout.monitorPosition, Quaternion.Euler(layout.monitorEuler));

            // Flattened cube sized to the physical monitor (child of ScreenCenter).
            if (monitor != null)
                monitor.localScale = new Vector3(layout.monitorWidth, layout.monitorHeight, MonitorThickness);

            // Push physical size into the off-axis camera (it reads these each LateUpdate).
            if (offAxis != null)
            {
                offAxis.screenWidth = layout.monitorWidth;
                offAxis.screenHeight = layout.monitorHeight;
            }

            // Camera markers (monitor-relative). Rebuild only when the count changes.
            SyncCameraMarkers(layout.cameras.Count);
            for (int i = 0; i < layout.cameras.Count; i++)
            {
                var cam = layout.cameras[i];
                cameraMarkers[i].position = ToWorld(cam.position);
                cameraMarkers[i].rotation = Quaternion.Euler(layout.monitorEuler) * Quaternion.Euler(cam.euler);
                cameraMarkers[i].localScale = Vector3.one * (i == layout.activeCamera ? 0.05f : 0.035f);
            }

            // User reference marker + best-effort neutral viewpoint (head/eye pivot).
            if (userMarker != null)
            {
                userMarker.position = ToWorld(layout.userReferencePosition);
                userMarker.localScale = Vector3.one * 0.1f;
            }
            if (headPivot != null)
                headPivot.position = ToWorld(layout.userReferencePosition);

            // Keep the "Ventana" gizmo in sync (screen-center local space == monitor space).
            if (frameGizmo != null)
            {
                frameGizmo.screenWidth = layout.monitorWidth;
                frameGizmo.screenHeight = layout.monitorHeight;
                frameGizmo.eyeLocalPosition = layout.userReferencePosition;
            }
        }

        // --- Writeback used by RuntimeGizmoView -------------------------------------

        // Move/rotate edit: world transform of the proxy -> layout (converting camera/user
        // back to monitor-relative space).
        public void PushWorldTransform(SpatialTarget target, Vector3 worldPos, Quaternion worldRot)
        {
            if (service == null || target == null) return;
            switch (target.kind)
            {
                case SpatialKind.Monitor:
                    service.SetMonitorPosition(worldPos);
                    service.SetMonitorEuler(worldRot.eulerAngles);
                    break;
                case SpatialKind.Camera:
                {
                    var localPos = screenCenter.InverseTransformPoint(worldPos);
                    var localEuler = (Quaternion.Inverse(screenCenter.rotation) * worldRot).eulerAngles;
                    service.SetCameraOffset(target.index, localPos, localEuler);
                    break;
                }
                case SpatialKind.User:
                {
                    var localPos = screenCenter.InverseTransformPoint(worldPos);
                    service.SetUserReference(localPos, service.Current.userReferenceDistance);
                    break;
                }
            }
        }

        public void PushMonitorScale(float width, float height) => service?.SetMonitorSize(width, height);

        private Vector3 ToWorld(Vector3 monitorLocal)
            => screenCenter != null ? screenCenter.TransformPoint(monitorLocal)
                                     : service.Current.monitorPosition + Quaternion.Euler(service.Current.monitorEuler) * monitorLocal;

        private void SyncCameraMarkers(int count)
        {
            while (cameraMarkers.Count < count)
            {
                int idx = cameraMarkers.Count;
                var m = MakePrimitive(PrimitiveType.Sphere, $"CameraMarker_{idx}", new Color(1f, 0.7f, 0.2f));
                Tag(m, SpatialKind.Camera, idx);
                cameraMarkers.Add(m);
            }
            while (cameraMarkers.Count > count)
            {
                int last = cameraMarkers.Count - 1;
                if (cameraMarkers[last] != null) Destroy(cameraMarkers[last].gameObject);
                cameraMarkers.RemoveAt(last);
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
