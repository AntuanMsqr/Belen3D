using System;
using UnityEngine;
using Hcp.Presentation.Domain;

namespace Hcp.Presentation.Application
{
    // Source of truth for the runtime spatial editor (plain C#, no engine services).
    // Seeded from config, then overlaid with any persisted layout (same pattern as
    // HeadTrackingController + ICalibrationStore). Mutators raise LayoutChanged so the
    // proxy/off-axis views update live.
    public class SpatialLayoutService
    {
        private ISpatialLayoutStore store;

        public SpatialLayout Current { get; private set; }
        public ScreenPlane CurrentScreenPlane => Current.ToScreenPlane();

        // Raised after any mutation (and on load/reset/rebind).
        public event Action LayoutChanged;

        public SpatialLayoutService(SpatialLayout seed, ISpatialLayoutStore store)
        {
            this.store = store;
            Current = seed ?? SpatialLayout.Defaults();
            this.store?.TryLoad(Current);
        }

        // Switch to another scene's store + seed (Configurator scene selector). Loads the
        // persisted layout for that scene over the seed and notifies listeners.
        public void Rebind(ISpatialLayoutStore store, SpatialLayout seed)
        {
            this.store = store;
            Current = seed ?? SpatialLayout.Defaults();
            this.store?.TryLoad(Current);
            Raise();
        }

        public void SetMonitorSize(float width, float height)
        {
            Current.monitorWidth = Mathf.Max(0.01f, width);
            Current.monitorHeight = Mathf.Max(0.01f, height);
            Raise();
        }

        public void SetMonitorPosition(Vector3 position)
        {
            Current.monitorPosition = position;
            Raise();
        }

        public void SetMonitorEuler(Vector3 euler)
        {
            Current.monitorEuler = euler;
            Raise();
        }

        public void SetCameraOffset(int index, Vector3 position, Vector3 euler)
        {
            if (index < 0 || index >= Current.cameras.Count) return;
            var cam = Current.cameras[index];
            cam.position = position;
            cam.euler = euler;
            Current.cameras[index] = cam;
            Raise();
        }

        public int AddCamera()
        {
            int n = Current.cameras.Count;
            Current.cameras.Add(new CameraOffset($"Cam {n}", new Vector3(0f, 0.2f, 0.6f), new Vector3(0f, 180f, 0f)));
            Current.activeCamera = n;
            Raise();
            return n;
        }

        public void RemoveCamera(int index)
        {
            if (Current.cameras.Count <= 1 || index < 0 || index >= Current.cameras.Count) return;
            Current.cameras.RemoveAt(index);
            Current.activeCamera = Mathf.Clamp(Current.activeCamera, 0, Current.cameras.Count - 1);
            Raise();
        }

        public void SetActiveCamera(int index)
        {
            if (index < 0 || index >= Current.cameras.Count) return;
            Current.activeCamera = index;
            Raise();
        }

        public void SetUserReference(Vector3 position, float distance)
        {
            Current.userReferencePosition = position;
            Current.userReferenceDistance = Mathf.Max(0.01f, distance);
            Raise();
        }

        public void Save() => store?.Save(Current);

        public void ResetToDefaults()
        {
            Current = SpatialLayout.Defaults();
            Raise();
        }

        private void Raise() => LayoutChanged?.Invoke();
    }
}
