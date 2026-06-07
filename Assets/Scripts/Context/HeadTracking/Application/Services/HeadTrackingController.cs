using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Orchestrates one frame of head tracking: pull -> process -> map -> CameraTarget.
    // Owns calibration state. Plain class (NOT a MonoBehaviour) — ticked by an Infrastructure View.
    public sealed class HeadTrackingController
    {
        private readonly IHeadPoseSource _source;
        private readonly HeadPoseProcessingService _processing;
        private readonly CameraMappingService _mapping;
        private readonly ICalibrationStore _store;

        private CalibrationData _cal;
        private bool _started;

        public Vector3 LastFilteredPosition { get; private set; }
        public Vector3 LastFilteredEuler { get; private set; }

        public HeadTrackingController(IHeadPoseSource source,
                                      HeadPoseProcessingService processing,
                                      CameraMappingService mapping,
                                      ICalibrationStore store,
                                      CalibrationData seed)
        {
            _source = source;
            _processing = processing;
            _mapping = mapping;
            _store = store;
            _cal = seed;
        }

        public CalibrationData Calibration => _cal;

        public void SetCalibration(in CalibrationData cal) => _cal = cal;

        public void Start()
        {
            if (_started) return;
            _started = true;
            _source.Start();
            _store.TryLoad(ref _cal); // overlay persisted values onto the config seed
            _processing.Reset();
            _mapping.ResetOrbit(_cal);
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;
            _source.Stop();
        }

        // Returns true when a fresh pose produced a new CameraTarget.
        public bool TryTick(float deltaTime, out CameraTarget target)
        {
            target = default;
            if (!_source.TryGetLatest(out var raw)) return false;

            var filtered = _processing.Process(raw, ref _cal);
            LastFilteredPosition = filtered.position;
            LastFilteredEuler = filtered.eulerDegrees;

            target = _mapping.Map(filtered, _cal, deltaTime);
            return true;
        }

        public void ResetOrbit() => _mapping.ResetOrbit(_cal);

        public void SaveCalibration() => _store.Save(_cal);

        public void ClearCalibration() => _store.Clear();
    }
}
