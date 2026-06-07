using UnityEngine;

namespace Hcp.Presentation.Infrastructure
{
    // What a proxy in the scene represents in the layout (set by SpatialProxyView).
    public enum SpatialKind { Monitor, Camera, User }

    public class SpatialTarget : MonoBehaviour
    {
        public SpatialKind kind;
        public int index;
    }

    // Runtime gizmo manipulation modes.
    public enum GizmoMode { Move, Rotate, Scale }

    // Tags a single draggable handle (built by RuntimeGizmoView).
    public enum HandleType { MoveAxis, RotateAxis, ScaleAxis }

    public class GizmoHandle : MonoBehaviour
    {
        public HandleType type;
        public int axis; // 0=X, 1=Y, 2=Z
    }
}
