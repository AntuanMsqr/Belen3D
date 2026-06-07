using UnityEngine;
using Hcp.HeadTracking.Application;

namespace Hcp.Diagnostics.Infrastructure
{
    // Persists calibration once after a short delay (to capture auto-learned neutral)
    // and again on quit. Uses the controller's ICalibrationStore under the hood.
    public class CalibrationAutoSaverView : MonoBehaviour
    {
        public float saveDelay = 3f;

        private HeadTrackingController controller;
        private float elapsed;
        private bool savedOnce;

        public void Initialize(HeadTrackingController controller)
        {
            this.controller = controller;
        }

        private void Update()
        {
            if (controller == null || savedOnce) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= saveDelay)
            {
                controller.SaveCalibration();
                savedOnce = true;
            }
        }

        private void OnApplicationQuit()
        {
            try { controller?.SaveCalibration(); } catch { /* ignore */ }
        }
    }
}
