using UnityEngine;
using Belen.HeadTracking.Domain;
using Belen.HeadTracking.Application;

namespace Belen.HeadTracking.Infrastructure
{
    // Inspector-editable tuning + wiring for the HeadTracking context.
    // The Bootstrap reads this and injects plain data into the Application services.
    [CreateAssetMenu(menuName = "Belen/HeadTracking Config", fileName = "HeadTrackingConfig")]
    public class HeadTrackingConfig : ScriptableObject
    {
        [Header("Source")]
        public SourceKind source = SourceKind.OpenSeeFaceBox;
        [Tooltip("OpenSee UDP listen address/port (used for OpenSee* sources).")]
        public string openSeeListenAddress = "127.0.0.1";
        public int openSeeListenPort = 11573;
        [Tooltip("Invert horizontal head motion (webcam is usually not mirrored).")]
        public bool invertX = true;
        [Tooltip("Invert vertical head motion.")]
        public bool invertY = false;
        [Tooltip("Invert depth (toward/away) head motion.")]
        public bool invertZ = false;
        [Tooltip("Scale applied to OpenSee 3D translation (meters).")]
        public float positionScale = 1f;

        [Header("Face Tracker Process (auto-launch)")]
        public bool autoLaunchTracker = true;
        [Tooltip("Path to facetracker.exe, relative to the project root (or absolute).")]
        public string trackerExeRelativePath = "Tools/OpenSeeFace/Binary/facetracker.exe";
        public string trackerArguments = "-c 0 -v 3 -P 1";
        [Tooltip("Show the tracker's console window (useful while debugging).")]
        public bool trackerShowWindow = true;

        [Header("Motion")]
        public MotionMode motionMode = MotionMode.Direct;
        public bool applyHeadRotation = true;

        [Header("Direct offsets")]
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Smoothing")]
        [Range(0f, 1f)] public float positionAlpha = 0.2f;
        [Range(0f, 1f)] public float rotationAlpha = 0.2f;

        [Header("Distance response (Direct)")]
        public bool useDistanceGain = false;
        public float neutralZ = 0.6f;
        public float distanceGain = 1.0f;
        public Vector2 zClamp = new Vector2(0.2f, 2.0f);

        [Header("Neutral learning")]
        public bool autoNeutral = false;
        public float autoNeutralDuration = 2.0f;
        public bool snapNeutralOnStart = true;
        public Vector2 neutralXY = Vector2.zero;

        [Header("Orbit tuning")]
        public float orbitBaseDistance = 1.2f;
        public float yawDegreesPerMeter = 400f;
        public float pitchDegreesPerMeter = 400f;
        public float dollyMetersPerMeter = 1.0f;
        public Vector2 pitchClamp = new Vector2(-45f, 45f);
        public Vector2 distanceClamp = new Vector2(0.3f, 3.0f);
        public bool orbitKeepHorizon = true;
        public Vector2 yawClamp = new Vector2(-60f, 60f);
        public float deadzoneX = 0.02f;
        public float deadzoneY = 0.02f;
        public float responseExponent = 1.0f;
        public float orbitSmoothTime = 0.12f;
        public Vector2 compositionOffsetDeg = Vector2.zero;

        [Header("Orbit start")]
        public bool applyOrbitStartOnEnable = true;
        public float orbitStartYaw = 0f;
        public float orbitStartPitch = 0f;
        public float orbitStartDistance = 1.2f;

        public CalibrationData ToCalibrationData()
        {
            return new CalibrationData
            {
                positionOffset = positionOffset,
                rotationOffset = rotationOffset,
                applyHeadRotation = applyHeadRotation,
                positionAlpha = positionAlpha,
                rotationAlpha = rotationAlpha,
                useDistanceGain = useDistanceGain,
                neutralZ = neutralZ,
                distanceGain = distanceGain,
                zClamp = zClamp,
                autoNeutral = autoNeutral,
                autoNeutralDuration = autoNeutralDuration,
                snapNeutralOnStart = snapNeutralOnStart,
                neutralXY = neutralXY,
                motionMode = motionMode,
                orbitBaseDistance = orbitBaseDistance,
                yawDegreesPerMeter = yawDegreesPerMeter,
                pitchDegreesPerMeter = pitchDegreesPerMeter,
                dollyMetersPerMeter = dollyMetersPerMeter,
                pitchClamp = pitchClamp,
                distanceClamp = distanceClamp,
                orbitKeepHorizon = orbitKeepHorizon,
                yawClamp = yawClamp,
                deadzoneX = deadzoneX,
                deadzoneY = deadzoneY,
                responseExponent = responseExponent,
                orbitSmoothTime = orbitSmoothTime,
                compositionOffsetDeg = compositionOffsetDeg,
                applyOrbitStartOnEnable = applyOrbitStartOnEnable,
                orbitStartYaw = orbitStartYaw,
                orbitStartPitch = orbitStartPitch,
                orbitStartDistance = orbitStartDistance
            };
        }
    }
}
