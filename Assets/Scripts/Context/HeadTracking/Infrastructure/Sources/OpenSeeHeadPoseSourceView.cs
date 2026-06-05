using System;
using UnityEngine;
using OpenSee;
using Belen.HeadTracking.Domain;

namespace Belen.HeadTracking.Infrastructure
{
    // Adapts OpenSee (OpenSeeFace Unity receiver) to IHeadPoseSource.
    public class OpenSeeHeadPoseSourceView : MonoBehaviour, IHeadPoseSource
    {
        public OpenSee.OpenSee openSee;
        [Tooltip("Which face id to use (0 = first)")] public int faceId = 0;
        [Tooltip("Apply overall scale to translation (meters)")] public float positionScale = 1f;
        [Tooltip("Per-axis multiplier after overall scale")] public Vector3 axisScale = Vector3.one;
        [Tooltip("Swap Y/Z from OpenSee if axes mismatch your rig")] public bool swapYZ = false;
        [Tooltip("Invert X (left/right) if horizontal parallax feels reversed")] public bool invertX = false;
        [Tooltip("Invert Y (up/down) if vertical parallax feels reversed")] public bool invertY = false;
        [Tooltip("Invert Z if approaching should decrease Z or vice versa")] public bool invertZ = false;
        [Tooltip("Optional additional offset in meters")] public Vector3 positionOffset = Vector3.zero;
        [Tooltip("Optional additional euler offset in degrees")] public Vector3 rotationOffset = Vector3.zero;

        [Header("Z from Face Size (optional)")]
        public bool estimateZFromFaceSize = false;
        public float neutralDepthMeters = 0.6f;
        public float neutralFacePixels = 0f;
        [Range(0f, 1f)] public float zBlend = 1.0f;
        public Vector2 zClamp = new Vector2(0.2f, 2.5f);
        public float zSmoothTime = 0.12f;

        private float _zSmoothed;
        private float _zVel;
        private float _lastFacePixels;

        private HeadPose _latest;
        public event Action<HeadPose> OnPose;

        private void Reset()
        {
            if (openSee == null) openSee = FindObjectOfType<OpenSee.OpenSee>();
        }

        private void Update()
        {
            if (openSee == null) return;
            var data = openSee.GetOpenSeeData(faceId);
            if (data == null || !data.got3DPoints) return;

            var pos = data.translation * positionScale;
            pos = Vector3.Scale(pos, axisScale);
            if (swapYZ)
            {
                (pos.y, pos.z) = (pos.z, pos.y);
            }
            if (invertX) pos.x = -pos.x;
            if (invertY) pos.y = -pos.y;
            if (invertZ) pos.z = -pos.z;

            float facePx = MeasureFacePixels(data);
            _lastFacePixels = facePx;
            if (estimateZFromFaceSize && facePx > 1f)
            {
                float zFromSize = neutralDepthMeters * (neutralFacePixels > 1f ? (neutralFacePixels / facePx) : 1f);
                zFromSize = Mathf.Clamp(zFromSize, zClamp.x, zClamp.y);
                if (_zSmoothed <= 0f) _zSmoothed = zFromSize;
                _zSmoothed = Mathf.SmoothDamp(_zSmoothed, zFromSize, ref _zVel, Mathf.Max(0.01f, zSmoothTime));
                pos.z = Mathf.Lerp(pos.z, _zSmoothed, Mathf.Clamp01(zBlend));
            }
            pos += positionOffset;
            var eul = data.rotation + rotationOffset;
            var ts = Time.realtimeSinceStartupAsDouble;
            _latest = new HeadPose(pos, eul, ts);
            OnPose?.Invoke(_latest);
        }

        public bool TryGetLatest(out HeadPose pose)
        {
            pose = _latest;
            return true;
        }

        public void Start() { /* no-op */ }
        public void Stop() { /* no-op */ }

        public float GetLastMeasuredFacePixels() => _lastFacePixels;

        public void CalibrateNeutralFaceSizeFromLast()
        {
            if (_lastFacePixels > 1f) neutralFacePixels = _lastFacePixels;
        }

        private static float MeasureFacePixels(OpenSee.OpenSee.OpenSeeData data)
        {
            if (data == null || data.points == null || data.points.Length == 0) return 0f;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < data.points.Length; i++)
            {
                var p = data.points[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            float w = Mathf.Max(1f, maxX - minX);
            float h = Mathf.Max(1f, maxY - minY);
            return Mathf.Max(w, h);
        }
    }
}
