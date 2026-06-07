using UnityEngine;

namespace Hcp.Presentation.Domain
{
    // Pure off-axis (Head-Coupled Perspective) projection math.
    // Ported from the legacy OffAxisCamera; no engine services, only value-type math.
    public static class OffAxisMath
    {
        public static OffAxisResult Compute(Vector3 eye, in ScreenPlane plane, float nearClip, float farClip,
                                            bool flipRight, bool flipUp)
        {
            if (plane.width <= 1e-6f || plane.height <= 1e-6f)
                return OffAxisResult.Invalid;

            Vector3 right = (flipRight ? -plane.right : plane.right);
            Vector3 up = (flipUp ? -plane.up : plane.up);
            Vector3 fwd = plane.forward;

            right = right.normalized;
            up = (up - Vector3.Project(up, right)).normalized;
            if (up.sqrMagnitude <= 1e-6f) return OffAxisResult.Invalid;

            Vector3 normal = Vector3.Cross(right, up).normalized; // out of screen
            if (normal.sqrMagnitude <= 1e-6f) return OffAxisResult.Invalid;

            // Match handedness to the supplied forward.
            if (Vector3.Dot(normal, fwd) < 0f)
            {
                right = -right;
                normal = Vector3.Cross(right, up).normalized;
                if (normal.sqrMagnitude <= 1e-6f) return OffAxisResult.Invalid;
            }

            Vector3 center = plane.center;
            float hw = plane.width * 0.5f;
            float hh = plane.height * 0.5f;
            Vector3 bl = center - right * hw - up * hh;
            Vector3 br = center + right * hw - up * hh;
            Vector3 tl = center - right * hw + up * hh;
            Vector3 tr = center + right * hw + up * hh;

            float d = -Vector3.Dot(bl - eye, normal);
            if (d <= 1e-4f) return OffAxisResult.Invalid; // eye behind screen

            float n = Mathf.Max(nearClip, 0.001f);
            float f = Mathf.Max(farClip, n + 0.01f);

            // View: a real Unity camera at the eye looking toward the screen (-normal, since
            // normal points out toward the viewer). Built the same way Unity builds its own
            // worldToCameraMatrix (Scale(1,1,-1) * worldToLocal), so the matrix is a reflection
            // (det = -1) with the engine's left-handed convention. A hand-rolled rotation basis
            // is a proper rotation (det = +1) and renders the scene horizontally mirrored with
            // inverted winding (front faces culled -> flat objects vanish).
            Quaternion rot = Quaternion.LookRotation(-normal, up);
            Matrix4x4 worldToCam = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                                 * Matrix4x4.TRS(eye, rot, Vector3.one).inverse;

            // Off-center frustum: project the four screen corners into this camera's space and
            // take the extents. Deriving l/r/b/t in the actual camera space (rather than a
            // separate screen basis) keeps horizontal AND vertical parallax consistent with the
            // view handedness, so head motion shifts the scene the correct way on both axes.
            float l = float.MaxValue, r = -float.MaxValue, b = float.MaxValue, t = -float.MaxValue;
            bool ok = true;
            void Accumulate(Vector3 worldCorner)
            {
                Vector3 c = worldToCam.MultiplyPoint3x4(worldCorner);
                float cz = -c.z; // distance in front of camera (camera looks down -Z)
                if (cz <= 1e-4f) { ok = false; return; }
                float xn = c.x * n / cz;
                float yn = c.y * n / cz;
                if (!IsFinite(xn) || !IsFinite(yn)) { ok = false; return; }
                if (xn < l) l = xn; if (xn > r) r = xn;
                if (yn < b) b = yn; if (yn > t) t = yn;
            }
            Accumulate(bl); Accumulate(br); Accumulate(tl); Accumulate(tr);
            if (!ok) return OffAxisResult.Invalid;
            if (Mathf.Abs(r - l) <= 1e-6f || Mathf.Abs(t - b) <= 1e-6f) return OffAxisResult.Invalid;

            var proj = PerspectiveOffCenter(l, r, b, t, n, f);
            return new OffAxisResult(true, proj, worldToCam);
        }

        private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
        {
            float x = 2.0f * near / (right - left);
            float y = 2.0f * near / (top - bottom);
            float a = (right + left) / (right - left);
            float b = (top + bottom) / (top - bottom);
            float c = -(far + near) / (far - near);
            float dd = -(2.0f * far * near) / (far - near);
            float e = -1.0f;

            Matrix4x4 m = new Matrix4x4();
            m[0, 0] = x; m[0, 1] = 0; m[0, 2] = a; m[0, 3] = 0;
            m[1, 0] = 0; m[1, 1] = y; m[1, 2] = b; m[1, 3] = 0;
            m[2, 0] = 0; m[2, 1] = 0; m[2, 2] = c; m[2, 3] = dd;
            m[3, 0] = 0; m[3, 1] = 0; m[3, 2] = e; m[3, 3] = 0;
            return m;
        }

        private static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));
    }
}
