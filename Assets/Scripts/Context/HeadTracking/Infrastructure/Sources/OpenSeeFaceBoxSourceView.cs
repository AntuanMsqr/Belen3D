using System;
using UnityEngine;
using OpenSee;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Infrastructure
{
    // Generates head pose from the 2D face box only (no rotation):
    // XY from face centroid in image space; Z from face size vs neutral.
    public class OpenSeeFaceBoxSourceView : MonoBehaviour, IHeadPoseSource
    {
        public OpenSee.OpenSee openSee;
        [Tooltip("Which face id to use (0 = first)")] public int faceId = 0;

        [Header("Mapping to meters")]
        public float widthMeters = 0.6f;
        public float heightMeters = 0.34f;

        [Header("Depth from face size")]
        public float neutralDepthMeters = 0.6f;
        public float neutralFacePixels = 0f;
        [Range(0f, 1f)] public float zBlend = 1.0f;
        public Vector2 zClamp = new Vector2(0.2f, 2.5f);
        public float zSmoothTime = 0.12f;
        public bool autoNeutral = true;
        public float autoNeutralDuration = 0.8f;
        public Vector2 faceSizeClamp01 = new Vector2(0.05f, 0.7f);
        public bool use3DZFallback = true;

        [Header("Orientation / Options")]
        public bool invertX = false;
        public bool invertY = false;

        private float zSmoothed;
        private float zVel;
        private float lastFacePixels;
        private float sumFace; private int countFace; private double t0;
        private HeadPose latest;

        public event Action<HeadPose> OnPose;

        private void Reset()
        {
            if (openSee == null) openSee = FindObjectOfType<OpenSee.OpenSee>();
        }

        private void Update()
        {
            if (openSee == null) return;
            var data = openSee.GetOpenSeeData(faceId);
            if (data == null || !data.got3DPoints || data.points == null || data.points.Length == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < data.points.Length; i++)
            {
                var p = data.points[i];
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }
            float cxPx = 0.5f * (minX + maxX);
            float cyPx = 0.5f * (minY + maxY);
            float wPx = Mathf.Max(1f, maxX - minX);
            float hPx = Mathf.Max(1f, maxY - minY);

            float camW = Mathf.Max(1f, data.cameraResolution.x);
            float camH = Mathf.Max(1f, data.cameraResolution.y);
            float cx = Mathf.Clamp01(cxPx / camW);
            float cy = Mathf.Clamp01(cyPx / camH);
            float faceN = Mathf.Clamp(Mathf.Max(wPx / camW, hPx / camH), faceSizeClamp01.x, faceSizeClamp01.y);
            lastFacePixels = faceN;

            float nx = (cx - 0.5f) * (invertX ? -1f : 1f);
            float ny = (cy - 0.5f) * (invertY ? 1f : -1f); // Y up

            Vector3 pos = new Vector3(nx * widthMeters, ny * heightMeters, neutralDepthMeters);

            if (autoNeutral && neutralFacePixels <= 0f)
            {
                if (countFace == 0) t0 = Time.realtimeSinceStartupAsDouble;
                sumFace += faceN; countFace++;
                if (Time.realtimeSinceStartupAsDouble - t0 >= Mathf.Max(0.1f, autoNeutralDuration))
                {
                    neutralFacePixels = Mathf.Max(1e-4f, sumFace / Mathf.Max(1, countFace));
                }
            }

            if (faceN > 1e-4f && neutralFacePixels > 1e-4f)
            {
                float zFromSize = neutralDepthMeters * (neutralFacePixels / faceN);
                zFromSize = Mathf.Clamp(zFromSize, zClamp.x, zClamp.y);
                if (zSmoothed <= 0f) zSmoothed = zFromSize;
                zSmoothed = Mathf.SmoothDamp(zSmoothed, zFromSize, ref zVel, Mathf.Max(0.01f, zSmoothTime));
                pos.z = Mathf.Lerp(pos.z, zSmoothed, Mathf.Clamp01(zBlend));
            }
            else if (use3DZFallback)
            {
                float z3d = Mathf.Abs(data.translation.z);
                z3d = Mathf.Clamp(z3d, zClamp.x, zClamp.y);
                if (zSmoothed <= 0f) zSmoothed = z3d;
                zSmoothed = Mathf.SmoothDamp(zSmoothed, z3d, ref zVel, Mathf.Max(0.01f, zSmoothTime));
                pos.z = zSmoothed;
            }

            var eul = Vector3.zero; // no rotation
            var ts = Time.realtimeSinceStartupAsDouble;
            latest = new HeadPose(pos, eul, ts);
            OnPose?.Invoke(latest);
        }

        public bool TryGetLatest(out HeadPose pose)
        {
            pose = latest;
            return true;
        }

        public void Start() { /* no-op */ }
        public void Stop() { /* no-op */ }

        public void CalibrateNeutralFaceSizeFromLast()
        {
            if (lastFacePixels > 1f) neutralFacePixels = lastFacePixels;
        }
    }
}
