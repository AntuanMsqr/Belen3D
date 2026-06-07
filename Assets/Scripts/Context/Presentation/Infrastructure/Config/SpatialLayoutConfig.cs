using System.Collections.Generic;
using UnityEngine;
using Hcp.Presentation.Domain;

namespace Hcp.Presentation.Infrastructure
{
    // Inspector-editable defaults for the spatial layout (monitor geometry, cameras,
    // user reference). The Bootstrap reads this as the seed; persisted runtime edits
    // are overlaid on top by the store. Mirrors the HeadTrackingConfig pattern.
    [CreateAssetMenu(menuName = "HCP/Spatial Layout Config", fileName = "SpatialLayoutConfig")]
    public class SpatialLayoutConfig : ScriptableObject
    {
        [Header("Monitor")]
        public Vector3 monitorPosition = Vector3.zero;
        public Vector3 monitorEuler = Vector3.zero;
        public float monitorWidth = 0.6f;
        public float monitorHeight = 0.34f;

        [Header("Cameras (relative to monitor)")]
        public List<CameraOffset> cameras = new List<CameraOffset>
        {
            new CameraOffset("Cam 0", new Vector3(0f, 0.2f, 0.6f), new Vector3(0f, 180f, 0f))
        };
        public int activeCamera = 0;

        [Header("User reference (relative to monitor)")]
        public Vector3 userReferencePosition = new Vector3(0f, 0.2f, 0.6f);
        public float userReferenceDistance = 0.6f;

        public SpatialLayout ToSpatialLayout()
        {
            return new SpatialLayout
            {
                monitorPosition = monitorPosition,
                monitorEuler = monitorEuler,
                monitorWidth = monitorWidth,
                monitorHeight = monitorHeight,
                cameras = cameras != null && cameras.Count > 0
                    ? new List<CameraOffset>(cameras)
                    : SpatialLayout.Defaults().cameras,
                activeCamera = activeCamera,
                userReferencePosition = userReferencePosition,
                userReferenceDistance = userReferenceDistance
            };
        }
    }
}
