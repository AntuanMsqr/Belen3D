using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hcp.Presentation.Domain
{
    // A camera placed relative to the monitor (the screen plane).
    // For the head-coupled camera this is the neutral viewpoint; extra cameras
    // (multi-display) reuse the same data shape.
    [Serializable]
    public struct CameraOffset
    {
        public string label;
        public Vector3 position; // relative to the monitor (screen plane)
        public Vector3 euler;    // relative to the monitor

        public CameraOffset(string label, Vector3 position, Vector3 euler)
        {
            this.label = label;
            this.position = position;
            this.euler = euler;
        }
    }

    // Spatial setup the user authors at runtime: where the monitor is and how big,
    // where the camera(s) sit relative to it, and the user's reference viewpoint.
    // [Serializable] so JsonUtility can round-trip it (FromJsonOverwrite in the store).
    // The monitor part bridges to the off-axis math via ToScreenPlane().
    [Serializable]
    public class SpatialLayout
    {
        [Header("Monitor")]
        public Vector3 monitorPosition;
        public Vector3 monitorEuler;
        public float monitorWidth;
        public float monitorHeight;

        [Header("Cameras")]
        public List<CameraOffset> cameras = new List<CameraOffset>();
        public int activeCamera;

        [Header("User reference")]
        public Vector3 userReferencePosition; // relative to the monitor
        public float userReferenceDistance;   // neutral distance, meters

        public static SpatialLayout Defaults()
        {
            return new SpatialLayout
            {
                monitorPosition = Vector3.zero,
                monitorEuler = Vector3.zero,
                monitorWidth = 0.6f,
                monitorHeight = 0.34f,
                cameras = new List<CameraOffset>
                {
                    new CameraOffset("Cam 0", new Vector3(0f, 0.2f, 0.6f), new Vector3(0f, 180f, 0f))
                },
                activeCamera = 0,
                userReferencePosition = new Vector3(0f, 0.2f, 0.6f),
                userReferenceDistance = 0.6f
            };
        }

        // Build the world-space screen plane consumed by the off-axis projection.
        public ScreenPlane ToScreenPlane()
        {
            var rot = Quaternion.Euler(monitorEuler);
            return new ScreenPlane(
                monitorPosition,
                rot * Vector3.right,
                rot * Vector3.up,
                rot * Vector3.forward,
                monitorWidth,
                monitorHeight);
        }
    }
}
