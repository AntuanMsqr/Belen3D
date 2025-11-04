using UnityEngine;
using Belen.Rendering;
using Belen.Tracking;

namespace Belen.DebugUI
{
    // Simple overlay showing head pose, distance to screen, and off-axis state
    public class DebugOverlay : MonoBehaviour
    {
        public FaceTrackerManager tracker;
        public OffAxisCamera offAxis;
        public Transform headPivot; // same as tracker.cameraPivot

        private float _fpsSmoothed;
        private double _lastPoseTs;
        private float _poseFps;

        void Update()
        {
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
        }

        void OnEnable()
        {
            var src = tracker != null ? tracker.GetComponent<MonoBehaviour>() : null;
            if (tracker != null && tracker.sourceBehaviour is Belen.Tracking.IHeadPoseSource srcIf)
            {
                srcIf.OnPose += OnPoseSample;
            }
        }

        void OnDisable()
        {
            if (tracker != null && tracker.sourceBehaviour is Belen.Tracking.IHeadPoseSource srcIf)
            {
                srcIf.OnPose -= OnPoseSample;
            }
        }

        private void OnPoseSample(HeadPose pose)
        {
            if (_lastPoseTs > 0)
            {
                var dt = (float)(pose.timestamp - _lastPoseTs);
                if (dt > 0 && dt < 1.0f) _poseFps = Mathf.Lerp(_poseFps, 1f / dt, 0.2f);
            }
            _lastPoseTs = pose.timestamp;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            Rect r = new Rect(10, 10, 420, 140);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.Label("Belén Debug Overlay", style);
            if (tracker != null && headPivot != null)
            {
                var pos = headPivot.localPosition;
                var eul = headPivot.localRotation.eulerAngles;
                GUILayout.Label($"Head pos: {pos.x:F3}, {pos.y:F3}, {pos.z:F3}", style);
                GUILayout.Label($"Head rot: {eul.x:F1}, {eul.y:F1}, {eul.z:F1}", style);
            }
            if (offAxis != null && offAxis.screenCenter != null && headPivot != null)
            {
                var n = offAxis.screenCenter.forward.normalized;
                var p0 = offAxis.screenCenter.position;
                var eye = headPivot.position;
                float d = Vector3.Dot(n, p0 - eye);
                GUILayout.Label($"Distance to screen (d): {d:F3} m", style);
            }
            GUILayout.Label($"Unity FPS: {_fpsSmoothed:F1}", style);
            GUILayout.Label($"Tracker FPS: {_poseFps:F1}", style);
            GUILayout.EndArea();
        }
    }
}

