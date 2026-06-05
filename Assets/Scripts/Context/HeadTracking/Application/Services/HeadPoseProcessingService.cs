using Belen.HeadTracking.Domain;

namespace Belen.HeadTracking.Application
{
    // Filters a raw head pose and updates neutral calibration.
    public sealed class HeadPoseProcessingService
    {
        private readonly HeadPoseExponentialFilter _filter;
        private readonly NeutralLearningService _neutral;

        public HeadPoseProcessingService(HeadPoseExponentialFilter filter, NeutralLearningService neutral)
        {
            _filter = filter;
            _neutral = neutral;
        }

        public void Reset()
        {
            _filter.Reset();
            _neutral.Reset();
        }

        public HeadPose Process(in HeadPose raw, ref CalibrationData cal)
        {
            // Keep the filter in sync with persisted/edited alphas.
            _filter.positionAlpha = cal.positionAlpha;
            _filter.rotationAlpha = cal.rotationAlpha;

            var filtered = _filter.Filter(raw);
            _neutral.Process(filtered, ref cal);
            return filtered;
        }
    }
}
