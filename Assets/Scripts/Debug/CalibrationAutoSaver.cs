using System.Collections;
using UnityEngine;
using Belen.Tracking;

namespace Belen.DebugUI
{
    // Saves calibration shortly after Play starts (once), and again on quit.
    public class CalibrationAutoSaver : MonoBehaviour
    {
        public FaceTrackerManager tracker;
        public Belen.Rendering.OffAxisCamera offAxis;
        [Tooltip("Seconds to wait before auto-saving after Play starts.")]
        public float saveDelay = 1.0f;
        private bool _saved;

        private IEnumerator Start()
        {
            if (tracker == null) tracker = FindObjectOfType<FaceTrackerManager>();
            if (offAxis == null) offAxis = FindObjectOfType<Belen.Rendering.OffAxisCamera>();
            // Load off-axis saved size at startup if present
            try { LoadOffAxis(); } catch { }
            // Wait a short while for filters/neutral snapping to settle
            float t = 0f;
            while (t < Mathf.Max(0.1f, saveDelay))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            TrySaveOnce();
        }

        private void OnApplicationQuit()
        {
            TrySaveOnce();
        }

        private void TrySaveOnce()
        {
            if (_saved || tracker == null) return;
            try { tracker.SaveCalibration(); } catch { }
            try { SaveOffAxis(); } catch { }
            _saved = true;
        }

        const string PrefOffAxis = "Belen/OffAxis/";
        void SaveOffAxis()
        {
            if (offAxis == null) return;
            PlayerPrefs.SetFloat(PrefOffAxis + "width", offAxis.screenWidth);
            PlayerPrefs.SetFloat(PrefOffAxis + "height", offAxis.screenHeight);
            PlayerPrefs.Save();
        }

        void LoadOffAxis()
        {
            if (offAxis == null) return;
            if (PlayerPrefs.HasKey(PrefOffAxis + "width"))
                offAxis.screenWidth = PlayerPrefs.GetFloat(PrefOffAxis + "width", offAxis.screenWidth);
            if (PlayerPrefs.HasKey(PrefOffAxis + "height"))
                offAxis.screenHeight = PlayerPrefs.GetFloat(PrefOffAxis + "height", offAxis.screenHeight);
        }
    }
}
