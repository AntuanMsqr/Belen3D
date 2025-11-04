using UnityEngine;

namespace Belen.Rendering
{
    // Parallax without custom projection: move the camera in screen space and look at a focus point.
    public class SimpleParallaxCamera : MonoBehaviour
    {
        public Transform screenCenter;
        public Transform eyeTransform;

        public Vector2 gainXY = new Vector2(1f, 1f);
        public bool invertX = false;
        public bool invertY = false;
        public bool useZ = true;
        public float zGain = 1f;
        public Vector2 clampX = new Vector2(-0.5f, 0.5f);
        public Vector2 clampY = new Vector2(-0.3f, 0.3f);
        public Vector2 clampZ = new Vector2(0.2f, 3.0f);

        public float focusDepth = 2.0f; // meters behind the screen
        public bool smooth = true;
        public float posSmoothTimeXY = 0.08f;
        public float posSmoothTimeZ = 0.12f;
        public float rotSmoothTime = 0.08f;
        public float deadzoneX = 0.002f;
        public float deadzoneY = 0.002f;

        private bool _initialized;
        private Vector3 _localPos;
        private float _velX, _velY, _velZ;

        void LateUpdate()
        {
            if (screenCenter == null || eyeTransform == null) return;

            var sc = screenCenter;
            Vector3 head = eyeTransform.position;
            // Head in screen-local coordinates
            Vector3 h = sc.InverseTransformPoint(head);

            float x = (invertX ? -h.x : h.x) * gainXY.x;
            float y = (invertY ? -h.y : h.y) * gainXY.y;
            x = Mathf.Clamp(x, clampX.x, clampX.y);
            y = Mathf.Clamp(y, clampY.x, clampY.y);

            // Distance from screen plane along its normal (viewer side is positive)
            float z = Vector3.Dot(head - sc.position, sc.forward);
            if (useZ)
            {
                z = Mathf.Clamp(z * Mathf.Max(0.001f, zGain), clampZ.x, clampZ.y);
            }
            else
            {
                z = Mathf.Clamp(z, clampZ.x, clampZ.y);
            }

            Vector3 targetLocal = new Vector3(x, y, z);

            if (!_initialized)
            {
                _localPos = targetLocal;
                transform.position = sc.TransformPoint(_localPos);
                var firstLook = sc.position - sc.forward * Mathf.Max(0.01f, focusDepth);
                transform.rotation = Quaternion.LookRotation(firstLook - transform.position, Vector3.up);
                _initialized = true;
                return;
            }

            if (!smooth)
            {
                _localPos = targetLocal;
            }
            else
            {
                // Apply deadzone before smoothing to kill micro jitter
                if (Mathf.Abs(targetLocal.x - _localPos.x) <= deadzoneX) targetLocal.x = _localPos.x;
                if (Mathf.Abs(targetLocal.y - _localPos.y) <= deadzoneY) targetLocal.y = _localPos.y;

                _localPos.x = Mathf.SmoothDamp(_localPos.x, targetLocal.x, ref _velX, Mathf.Max(0.001f, posSmoothTimeXY));
                _localPos.y = Mathf.SmoothDamp(_localPos.y, targetLocal.y, ref _velY, Mathf.Max(0.001f, posSmoothTimeXY));
                _localPos.z = Mathf.SmoothDamp(_localPos.z, targetLocal.z, ref _velZ, Mathf.Max(0.001f, posSmoothTimeZ));
            }

            Vector3 worldPos = sc.TransformPoint(_localPos);
            transform.position = worldPos;

            // Smooth rotation towards focus point into the screen
            Vector3 lookTarget = sc.position - sc.forward * Mathf.Max(0.01f, focusDepth);
            Quaternion targetRot = Quaternion.LookRotation(lookTarget - worldPos, Vector3.up);
            float rt = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, rotSmoothTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01(rt));
        }
    }
}
