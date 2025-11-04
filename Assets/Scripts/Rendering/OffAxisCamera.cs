using UnityEngine;

namespace Belen.Rendering
{
    // Off-axis projection for Head-Coupled Perspective (HCP).
    // Define a physical screen plane in the scene (center, width, height) and an eye transform.
    // The camera will render as if the screen were a window into the scene.
    [RequireComponent(typeof(Camera))]
    public class OffAxisCamera : MonoBehaviour
    {
        [Header("Screen Plane")]
        public Transform screenCenter; // transform whose forward is screen normal (pointing out of screen), right = screen right
        [Tooltip("Flip the screen 'up' direction if vertical parallax feels inverted")]
        public bool flipScreenUp = false;
        [Tooltip("Flip the screen 'right' direction if horizontal parallax feels inverted")]
        public bool flipScreenRight = false;
        public float screenWidth = 0.6f;  // meters
        public float screenHeight = 0.34f; // meters (e.g., 27" 16:9 ~ 0.597x0.336)

        [Header("Eye (Head) Pose")]
        public Transform eyeTransform; // typically the camera pivot controlled by tracking

        [Header("Clipping")]
        public float nearClip = 0.01f;
        public float farClip = 100f;

        [Header("Options")]
        public bool overrideCameraMatrices = true;
        public bool drawGizmos = true;

        private Camera _cam;

        private void OnEnable()
        {
            _cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (!overrideCameraMatrices || screenCenter == null || eyeTransform == null || _cam == null)
                return;

            if (!UpdateOffAxis())
            {
                // Fallback to standard camera matrices if geometry is invalid
                _cam.ResetProjectionMatrix();
                _cam.ResetWorldToCameraMatrix();
            }
        }

        private bool UpdateOffAxis()
        {
            // Validate geometry
            if (screenWidth <= 1e-6f || screenHeight <= 1e-6f)
                return false;

            // Screen basis from transform with optional flips
            Vector3 right = (flipScreenRight ? -screenCenter.right : screenCenter.right);
            Vector3 up = (flipScreenUp ? -screenCenter.up : screenCenter.up);
            Vector3 fwd = screenCenter.forward;

            // Orthonormalize right/up and derive normal
            right = right.normalized;
            // Make up orthogonal to right
            up = (up - Vector3.Project(up, right)).normalized;
            if (up.sqrMagnitude <= 1e-6f)
                return false;
            Vector3 normal = Vector3.Cross(right, up).normalized; // out of screen
            if (normal.sqrMagnitude <= 1e-6f)
                return false;

            // Ensure handedness matches the transform's forward
            // Ensure handedness matches the transform's forward
            if (Vector3.Dot(normal, fwd) < 0f)
            {
                right = -right;
                normal = Vector3.Cross(right, up).normalized;
                if (normal.sqrMagnitude <= 1e-6f)
                    return false;
            }

            // Screen corners in world space
            Vector3 center = screenCenter.position;
            float hw = screenWidth * 0.5f;
            float hh = screenHeight * 0.5f;
            Vector3 pa = center - right * hw - up * hh; // bottom-left
            Vector3 pb = center + right * hw - up * hh; // bottom-right
            Vector3 pc = center - right * hw + up * hh; // top-left

            Vector3 eye = eyeTransform.position;

            // Vectors from eye to three corners of the screen
            Vector3 va = pa - eye;
            Vector3 vb = pb - eye;
            Vector3 vc = pc - eye;

            // Distance from eye to screen plane
            float d = -Vector3.Dot(va, normal);
            if (d <= 1e-4f)
                return false; // eye behind screen plane

            float n = Mathf.Max(nearClip, 0.001f);
            float f = Mathf.Max(farClip, n + 0.01f);

            // Off-center frustum extents at near plane
            float l = Vector3.Dot(right, va) * n / d;
            float r = Vector3.Dot(right, vb) * n / d;
            float b = Vector3.Dot(up,    va) * n / d;
            float t = Vector3.Dot(up,    vc) * n / d;
            if (!IsFinite(l) || !IsFinite(r) || !IsFinite(b) || !IsFinite(t))
                return false;
            if (Mathf.Abs(r - l) <= 1e-6f || Mathf.Abs(t - b) <= 1e-6f)
                return false;

            // Projection and view matrices
            var proj = PerspectiveOffCenter(l, r, b, t, n, f);
            var m = Matrix4x4.identity;
            m[0, 0] = right.x; m[0, 1] = right.y; m[0, 2] = right.z; m[0, 3] = 0;
            m[1, 0] = up.x;    m[1, 1] = up.y;    m[1, 2] = up.z;    m[1, 3] = 0;
            m[2, 0] = normal.x;m[2, 1] = normal.y;m[2, 2] = normal.z;m[2, 3] = 0;
            m[3, 0] = 0;       m[3, 1] = 0;       m[3, 2] = 0;       m[3, 3] = 1;

            var tEye = Matrix4x4.Translate(-eye);
            var view = m * tEye;

            _cam.worldToCameraMatrix = view;
            _cam.projectionMatrix = GL.GetGPUProjectionMatrix(proj, true);
            return true;
        }

        

        private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
        {
            float x = 2.0f * near / (right - left);
            float y = 2.0f * near / (top - bottom);
            float a = (right + left) / (right - left);
            float b = (top + bottom) / (top - bottom);
            float c = -(far + near) / (far - near);
            float d = -(2.0f * far * near) / (far - near);
            float e = -1.0f;

            Matrix4x4 m = new Matrix4x4();
            m[0, 0] = x;   m[0, 1] = 0;   m[0, 2] = a;   m[0, 3] = 0;
            m[1, 0] = 0;   m[1, 1] = y;   m[1, 2] = b;   m[1, 3] = 0;
            m[2, 0] = 0;   m[2, 1] = 0;   m[2, 2] = c;   m[2, 3] = d;
            m[3, 0] = 0;   m[3, 1] = 0;   m[3, 2] = e;   m[3, 3] = 0;
            return m;
        }

        private static bool IsFinite(float v)
        {
            return !(float.IsNaN(v) || float.IsInfinity(v));
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || screenCenter == null) return;
            Gizmos.color = Color.cyan;
            var right = (flipScreenRight ? -screenCenter.right : screenCenter.right).normalized;
            var up = (flipScreenUp ? -screenCenter.up : screenCenter.up).normalized;
            Vector3 center = screenCenter.position;
            float hw = screenWidth * 0.5f;
            float hh = screenHeight * 0.5f;
            Vector3 pa = center - right * hw - up * hh;
            Vector3 pb = center + right * hw - up * hh;
            Vector3 pc = center - right * hw + up * hh;
            Vector3 pd = center + right * hw + up * hh;

            Gizmos.DrawLine(pa, pb);
            Gizmos.DrawLine(pb, pd);
            Gizmos.DrawLine(pd, pc);
            Gizmos.DrawLine(pc, pa);

            var normal = screenCenter.forward.normalized;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center, center + normal * 0.2f);
        }
    }
}
