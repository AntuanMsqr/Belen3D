using UnityEngine;

namespace Hcp.Presentation.Infrastructure
{
    // Scene-view authoring aid for off-axis (HCP) setups. Place this at the screen-center
    // (same spot/orientation as the runtime ScreenCenter) and it draws, in edit mode:
    //   - the physical screen rectangle (cyan),
    //   - the neutral eye (cyan sphere),
    //   - the view frustum edges (yellow),
    //   - the framed region at one or more depths (green) = exactly what fills the screen.
    // Author content INSIDE the green rectangles and it will be on-screen in game.
    // Pure gizmos: no runtime behaviour.
    public class OffAxisFrameGizmo : MonoBehaviour
    {
        [Header("Screen (physical monitor, meters)")]
        public float screenWidth = 0.62f;
        public float screenHeight = 0.349f;

        [Header("Neutral eye (relative to screen center)")]
        public Vector3 eyeLocalPosition = new Vector3(0f, 0f, 0.6f);

        [Header("Framed-region previews (meters into the scene)")]
        public float[] previewDepths = { 0.5f, 1.5f, 3f };

        [Header("Colors")]
        public Color screenColor = Color.cyan;
        public Color frustumColor = new Color(1f, 1f, 0f, 0.6f);
        public Color frameColor = Color.green;

        private void OnDrawGizmos()
        {
            float hw = screenWidth * 0.5f;
            float hh = screenHeight * 0.5f;

            Vector3 bl = transform.TransformPoint(new Vector3(-hw, -hh, 0f));
            Vector3 br = transform.TransformPoint(new Vector3( hw, -hh, 0f));
            Vector3 tr = transform.TransformPoint(new Vector3( hw,  hh, 0f));
            Vector3 tl = transform.TransformPoint(new Vector3(-hw,  hh, 0f));
            Vector3 eye = transform.TransformPoint(eyeLocalPosition);

            // Screen rectangle.
            Gizmos.color = screenColor;
            Gizmos.DrawLine(bl, br); Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl); Gizmos.DrawLine(tl, bl);
            Gizmos.DrawWireSphere(eye, 0.02f);

            // Scene lies on the opposite side of the screen from the eye.
            Vector3 normal = transform.forward;
            float eyeSide = Mathf.Sign(Vector3.Dot(eye - transform.position, normal));
            Vector3 intoScene = -eyeSide * normal;

            float maxDepth = 0f;
            if (previewDepths != null)
                foreach (var d in previewDepths) if (d > maxDepth) maxDepth = d;

            // Frustum edges.
            Gizmos.color = frustumColor;
            DrawCornerRay(eye, bl, intoScene, maxDepth);
            DrawCornerRay(eye, br, intoScene, maxDepth);
            DrawCornerRay(eye, tr, intoScene, maxDepth);
            DrawCornerRay(eye, tl, intoScene, maxDepth);

            // Framed region at each depth.
            Gizmos.color = frameColor;
            if (previewDepths != null)
            {
                foreach (var depth in previewDepths)
                {
                    Vector3 planePoint = transform.position + intoScene * depth;
                    Vector3 fbl = RayPlane(eye, bl, planePoint, normal);
                    Vector3 fbr = RayPlane(eye, br, planePoint, normal);
                    Vector3 ftr = RayPlane(eye, tr, planePoint, normal);
                    Vector3 ftl = RayPlane(eye, tl, planePoint, normal);
                    Gizmos.DrawLine(fbl, fbr); Gizmos.DrawLine(fbr, ftr);
                    Gizmos.DrawLine(ftr, ftl); Gizmos.DrawLine(ftl, fbl);
                }
            }
        }

        private static void DrawCornerRay(Vector3 eye, Vector3 corner, Vector3 intoScene, float depth)
        {
            Vector3 dir = (corner - eye).normalized;
            float advance = Vector3.Dot(dir, intoScene);
            if (advance <= 1e-3f) return;
            float t = depth / advance;
            Gizmos.DrawLine(eye, eye + dir * t);
        }

        private static Vector3 RayPlane(Vector3 eye, Vector3 corner, Vector3 planePoint, Vector3 normal)
        {
            Vector3 dir = corner - eye;
            float denom = Vector3.Dot(dir, normal);
            if (Mathf.Abs(denom) < 1e-6f) return corner;
            float t = Vector3.Dot(planePoint - eye, normal) / denom;
            return eye + dir * t;
        }
    }
}
