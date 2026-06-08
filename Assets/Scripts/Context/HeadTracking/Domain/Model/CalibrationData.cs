using UnityEngine;

namespace Hcp.HeadTracking.Domain
{
    // Plain, mutable tuning + calibration state for head tracking.
    // Mirrors every field that the legacy FaceTrackerManager persisted to PlayerPrefs,
    // so an Infrastructure store can round-trip it without engine types leaking into Application.
    public struct CalibrationData
    {
        // Direct-mode offsets
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public bool applyHeadRotation;

        // Smoothing filter
        public float positionAlpha;
        public float rotationAlpha;

        // Distance response (Direct)
        public bool useDistanceGain;
        public float neutralZ;
        public float distanceGain;
        public Vector2 zClamp;

        // Neutral learning
        public bool autoNeutral;
        public float autoNeutralDuration;
        public bool snapNeutralOnStart;
        public Vector2 neutralXY;
        public Vector3 neutralEuler;

        // Motion mode
        public MotionMode motionMode;

        // Orbit tuning
        public float orbitBaseDistance;
        public float yawDegreesPerMeter;
        public float pitchDegreesPerMeter;
        public float dollyMetersPerMeter;
        public Vector2 pitchClamp;
        public Vector2 distanceClamp;
        public bool orbitKeepHorizon;
        public Vector2 yawClamp;
        public float deadzoneX;
        public float deadzoneY;
        public float responseExponent;
        public float orbitSmoothTime;
        public Vector2 compositionOffsetDeg;

        // Orbit start
        public bool applyOrbitStartOnEnable;
        public float orbitStartYaw;
        public float orbitStartPitch;
        public float orbitStartDistance;

        public static CalibrationData Defaults()
        {
            return new CalibrationData
            {
                positionOffset = Vector3.zero,
                rotationOffset = Vector3.zero,
                applyHeadRotation = true,

                positionAlpha = 0.2f,
                rotationAlpha = 0.2f,

                useDistanceGain = false,
                neutralZ = 0.6f,
                distanceGain = 5.0f,
                zClamp = new Vector2(0.2f, 2.0f),

                autoNeutral = false,
                autoNeutralDuration = 2.0f,
                snapNeutralOnStart = true,
                neutralXY = Vector2.zero,

                motionMode = MotionMode.Direct,

                orbitBaseDistance = 1.2f,
                yawDegreesPerMeter = 400f,
                pitchDegreesPerMeter = 400f,
                dollyMetersPerMeter = 1.0f,
                pitchClamp = new Vector2(-45f, 45f),
                distanceClamp = new Vector2(0.3f, 3.0f),
                orbitKeepHorizon = true,
                yawClamp = new Vector2(-60f, 60f),
                deadzoneX = 0.02f,
                deadzoneY = 0.02f,
                responseExponent = 1.0f,
                orbitSmoothTime = 0.12f,
                compositionOffsetDeg = Vector2.zero,

                applyOrbitStartOnEnable = true,
                orbitStartYaw = 0f,
                orbitStartPitch = 0f,
                orbitStartDistance = 1.2f
            };
        }
    }
}
