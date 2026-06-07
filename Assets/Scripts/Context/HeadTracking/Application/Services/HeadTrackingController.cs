using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Orchestrates one frame of head tracking: pull -> process -> map -> CameraTarget.
    // Owns calibration state. Plain class (NOT a MonoBehaviour) — ticked by an Infrastructure View.
    public sealed class HeadTrackingController
    {
        private readonly IHeadPoseSource source;
        private readonly HeadPoseProcessingService processing;
        private readonly CameraMappingService mapping;
        private readonly ICalibrationStore store;

        private CalibrationData cal;
        private bool started;

        public Vector3 LastFilteredPosition { get; private set; }
        public Vector3 LastFilteredEuler { get; private set; }

        public HeadTrackingController(IHeadPoseSource source,
                                      HeadPoseProcessingService processing,
                                      CameraMappingService mapping,
                                      ICalibrationStore store,
                                      CalibrationData seed)
        {
            this.source = source;
            this.processing = processing;
            this.mapping = mapping;
            this.store = store;
            cal = seed;
        }

        public CalibrationData Calibration => cal;

        public void SetCalibration(in CalibrationData cal) => this.cal = cal;

        public void Start()
        {
            if (started) return;
            started = true;
            source.Start();
            store.TryLoad(ref cal); // overlay persisted values onto the config seed
            processing.Reset();
            mapping.ResetOrbit(cal);
        }

        public void Stop()
        {
            if (!started) return;
            started = false;
            source.Stop();
        }

        // Returns true when a fresh pose produced a new CameraTarget.
        public bool TryTick(float deltaTime, out CameraTarget target)
        {
            target = default;
            if (!source.TryGetLatest(out var raw)) return false;

            var filtered = processing.Process(raw, ref cal);
            LastFilteredPosition = filtered.position;
            LastFilteredEuler = filtered.eulerDegrees;

            target = mapping.Map(filtered, cal, deltaTime);
            return true;
        }

        public void ResetOrbit() => mapping.ResetOrbit(cal);

        public void SaveCalibration() => store.Save(cal);

        public void ClearCalibration() => store.Clear();
    }
}
