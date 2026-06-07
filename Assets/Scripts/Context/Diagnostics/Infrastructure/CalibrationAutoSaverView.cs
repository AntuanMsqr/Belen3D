using UnityEngine;
using Hcp.HeadTracking.Application;

namespace Hcp.Diagnostics.Infrastructure
{
    // Persists calibration once after a short delay (to capture auto-learned neutral)
    // and again on quit. Uses the controller's ICalibrationStore under the hood.
    public class CalibrationAutoSaverView : MonoBehaviour
    {
        public float saveDelay = 3f;

        private HeadTrackingController _controller;
        private float _elapsed;
        private bool _savedOnce;

        public void Initialize(HeadTrackingController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            if (_controller == null || _savedOnce) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= saveDelay)
            {
                _controller.SaveCalibration();
                _savedOnce = true;
            }
        }

        private void OnApplicationQuit()
        {
            try { _controller?.SaveCalibration(); } catch { /* ignore */ }
        }
    }
}
