using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Learns the neutral head position used as the orbit/parallax origin.
    // Mirrors the legacy FaceTrackerManager behavior: either average over a window
    // (autoNeutral) or snap to the first stable pose (snapNeutralOnStart).
    public sealed class NeutralLearningService
    {
        private readonly IClock clock;

        private bool learning;
        private bool snapped;
        private double startTs;
        private float sumX, sumY, sumZ;
        private int count;

        public bool IsLearning => learning;

        public NeutralLearningService(IClock clock)
        {
            this.clock = clock;
        }

        public void Reset()
        {
            learning = false;
            snapped = false;
            count = 0;
            sumX = sumY = sumZ = 0f;
        }

        public void Process(in HeadPose filtered, ref CalibrationData cal)
        {
            if (cal.autoNeutral && !learning)
            {
                Begin();
            }

            if (learning)
            {
                Accumulate(filtered, ref cal);
            }
            else if (cal.snapNeutralOnStart && !snapped)
            {
                cal.neutralZ = filtered.position.z;
                cal.neutralXY = new Vector2(filtered.position.x, filtered.position.y);
                cal.neutralEuler = filtered.eulerDegrees;
                snapped = true;
            }
        }

        private void Begin()
        {
            learning = true;
            sumX = sumY = sumZ = 0f;
            count = 0;
            startTs = clock.NowSeconds;
            snapped = true; // prevent snap once auto-learning has started
        }

        private void Accumulate(in HeadPose pose, ref CalibrationData cal)
        {
            if (count == 0) startTs = clock.NowSeconds;
            sumX += pose.position.x;
            sumY += pose.position.y;
            sumZ += pose.position.z;
            count++;

            if (clock.NowSeconds - startTs >= Mathf.Max(0.1f, cal.autoNeutralDuration))
            {
                float denom = Mathf.Max(1, count);
                cal.neutralZ = sumZ / denom;
                cal.neutralXY = new Vector2(sumX / denom, sumY / denom);
                learning = false;
            }
        }
    }
}
