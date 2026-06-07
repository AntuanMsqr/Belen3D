using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Filters a raw head pose and updates neutral calibration.
    public sealed class HeadPoseProcessingService
    {
        private readonly HeadPoseExponentialFilter filter;
        private readonly NeutralLearningService neutral;

        public HeadPoseProcessingService(HeadPoseExponentialFilter filter, NeutralLearningService neutral)
        {
            this.filter = filter;
            this.neutral = neutral;
        }

        public void Reset()
        {
            filter.Reset();
            neutral.Reset();
        }

        public HeadPose Process(in HeadPose raw, ref CalibrationData cal)
        {
            // Keep the filter in sync with persisted/edited alphas.
            this.filter.positionAlpha = cal.positionAlpha;
            this.filter.rotationAlpha = cal.rotationAlpha;

            var filtered = this.filter.Filter(raw);
            neutral.Process(filtered, ref cal);
            return filtered;
        }
    }
}
