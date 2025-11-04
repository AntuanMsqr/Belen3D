using UnityEngine;
using Belen.Tracking.Filters;

namespace Belen.Tracking
{
    // Bridges an IHeadPoseSource to a target camera transform with smoothing.
    public class FaceTrackerManager : MonoBehaviour
    {
        [Header("Source")]
        public MonoBehaviour sourceBehaviour; // must implement IHeadPoseSource

        [Header("Output")]
        public Transform cameraPivot; // pivot to position/orient (e.g., parent of Camera)
        [Header("Output Options")]
        [Tooltip("Apply head rotation to the camera pivot in Direct mode.")]
        public bool applyHeadRotation = true;

        [Header("Smoothing")]
        public HeadPoseExponentialFilter filter = new HeadPoseExponentialFilter();

        [Header("Gains")]
        public Vector3 positionOffset = Vector3.zero; // additional offset in meters
        public Vector3 rotationOffset = Vector3.zero; // additional euler degrees

        [Header("Distance Response")]
        [Tooltip("Enable scaling of Z distance around a neutral point to exaggerate or reduce in/out motion.")]
        public bool useDistanceGain = false;
        [Tooltip("Neutral head distance in meters (around which scaling is applied).")]
        public float neutralZ = 0.6f;
        [Tooltip("Multiplier for (z - neutralZ). 1 = natural, >1 exaggerates, <1 reduces.")]
        public float distanceGain = 1.0f;
        [Tooltip("Clamp applied Z in meters [min,max].")]
        public Vector2 zClamp = new Vector2(0.2f, 2.0f);

        [Header("Auto Neutral")]
        [Tooltip("If enabled, learns neutralZ over a short window at start.")]
        public bool autoNeutral = false;
        [Tooltip("Seconds to average Z for neutral calibration.")]
        public float autoNeutralDuration = 2.0f;
        [Tooltip("Snap neutral XY/Z from the first received pose when autoNeutral is off.")]
        public bool snapNeutralOnStart = true;

        private bool _learningNeutral;
        private double _learnStartTs;
        private float _sumZ, _sumX, _sumY;
        private int _countZ;
        private bool _snappedOnce;

        [Header("Camera Motion Mode")]
        public MotionMode motionMode = MotionMode.Direct;
        public Transform orbitTarget;
        public float orbitBaseDistance = 1.2f;
        public float yawDegreesPerMeter = 400f;
        public float pitchDegreesPerMeter = 400f;
        public float dollyMetersPerMeter = 1.0f;
        public Vector2 pitchClamp = new Vector2(-45f, 45f);
        public Vector2 distanceClamp = new Vector2(0.3f, 3.0f);

        [Tooltip("Neutral X/Y for orbit mode (meters in tracker space)")]
        public Vector2 neutralXY = Vector2.zero;

        public enum MotionMode { Direct, OrbitTarget }

        // Exposed last filtered pose for calibration UI
        public Vector3 lastFilteredPosition { get; private set; }
        public Vector3 lastFilteredEuler { get; private set; }

        [Header("Orbit Tuning")]
        public bool orbitKeepHorizon = true;
        public Vector2 yawClamp = new Vector2(-60f, 60f);
        public float deadzoneX = 0.02f; // meters
        public float deadzoneY = 0.02f; // meters
        [Tooltip("Nonlinear response exponent (>1 = more around extremes, <1 = more around center)")]
        public float responseExponent = 1.0f;
        [Tooltip("Seconds for SmoothDamp of yaw/pitch/distance")]
        public float orbitSmoothTime = 0.12f;
        [Tooltip("Composition offsets (degrees) added to yaw/pitch to shift framing")]
        public Vector2 compositionOffsetDeg = Vector2.zero; // x=yaw, y=pitch

        [Header("Orbit Start")]
        [Tooltip("Apply starting yaw/pitch/distance at enable when in Orbit mode.")]
        public bool applyOrbitStartOnEnable = true;
        public float orbitStartYaw = 0f;
        public float orbitStartPitch = 0f;
        public float orbitStartDistance = 1.2f;

        private float _yawCur, _pitchCur, _distCur;
        private float _yawVel, _pitchVel, _distVel;

        private IHeadPoseSource _source;
        private bool _warned;

        private void Awake()
        {
            _source = sourceBehaviour as IHeadPoseSource;
            if (_source == null)
            {
                Debug.LogError("[FaceTrackerManager] sourceBehaviour must implement IHeadPoseSource");
            }
            if (orbitTarget == null)
            {
                var off = FindObjectOfType<Belen.Rendering.OffAxisCamera>();
                if (off != null) orbitTarget = off.screenCenter;
            }
        }

        private void OnEnable()
        {
            _source?.Start();
            // Load persisted calibration if present
            try { LoadCalibration(); } catch { }

            // Initialize orbit starting pose if requested
            if (motionMode == MotionMode.OrbitTarget && applyOrbitStartOnEnable)
            {
                _yawCur = orbitStartYaw;
                _pitchCur = orbitStartPitch;
                _distCur = Mathf.Clamp(orbitStartDistance > 0 ? orbitStartDistance : orbitBaseDistance, distanceClamp.x, distanceClamp.y);
                if (orbitTarget != null && cameraPivot != null)
                {
                    var rot = Quaternion.Euler(_pitchCur, _yawCur, 0f);
                    var offset = rot * new Vector3(0, 0, -_distCur);
                    var camPos = orbitTarget.position + offset;
                    cameraPivot.position = camPos;
                    var up = orbitKeepHorizon ? Vector3.up : cameraPivot.up;
                    cameraPivot.rotation = Quaternion.LookRotation(orbitTarget.position - camPos, up);
                }
            }
        }

        private void OnDisable()
        {
            _source?.Stop();
        }

        private void Update()
        {
            if (_source == null || cameraPivot == null)
            {
                if (!_warned)
                {
                    Debug.LogWarning("[FaceTrackerManager] Missing source or cameraPivot");
                    _warned = true;
                }
                return;
            }

            if (_source.TryGetLatest(out var pose))
            {
                var filtered = filter != null ? filter.Filter(pose) : pose;
                lastFilteredPosition = filtered.position;
                lastFilteredEuler = filtered.eulerDegrees;
                if (autoNeutral && !_learningNeutral)
                {
                    StartNeutralLearning(autoNeutralDuration);
                }
                if (_learningNeutral)
                {
                    AccumulateNeutral(filtered);
                }
                else if (snapNeutralOnStart && !_snappedOnce)
                {
                    // Snap neutral XY/Z to the first stable filtered pose
                    neutralZ = filtered.position.z;
                    neutralXY = new Vector2(filtered.position.x, filtered.position.y);
                    _snappedOnce = true;
                }
                ApplyPose(filtered);
            }
        }

        private void ApplyPose(HeadPose pose)
        {
            if (motionMode == MotionMode.Direct)
            {
                var pos = pose.position + positionOffset;
                if (useDistanceGain)
                {
                    float dz = pos.z - neutralZ;
                    pos.z = Mathf.Clamp(neutralZ + dz * distanceGain, zClamp.x, zClamp.y);
                }
                var eul = pose.eulerDegrees + rotationOffset;
                cameraPivot.localPosition = pos;
                if (applyHeadRotation)
                    cameraPivot.localRotation = Quaternion.Euler(eul);
                else
                    cameraPivot.localRotation = Quaternion.identity;
                return;
            }

            if (motionMode == MotionMode.OrbitTarget && orbitTarget != null)
            {
                // Map head deltas to orbit yaw/pitch and dolly
                float dx = (pose.position.x - neutralXY.x);
                float dy = (pose.position.y - neutralXY.y);
                float dz = (pose.position.z - neutralZ);

                dx = ApplyDeadzone(dx, deadzoneX);
                dy = ApplyDeadzone(dy, deadzoneY);
                if (!Mathf.Approximately(responseExponent, 1f))
                {
                    dx = Mathf.Sign(dx) * Mathf.Pow(Mathf.Abs(dx), Mathf.Max(0.01f, responseExponent));
                    dy = Mathf.Sign(dy) * Mathf.Pow(Mathf.Abs(dy), Mathf.Max(0.01f, responseExponent));
                }

                float yawTarget = Mathf.Clamp(dx * yawDegreesPerMeter + compositionOffsetDeg.x, yawClamp.x, yawClamp.y);
                float pitchTarget = Mathf.Clamp(-dy * pitchDegreesPerMeter + compositionOffsetDeg.y, pitchClamp.x, pitchClamp.y);
                float distTarget = Mathf.Clamp(orbitBaseDistance + dz * dollyMetersPerMeter, distanceClamp.x, distanceClamp.y);

                _yawCur = Mathf.SmoothDampAngle(_yawCur, yawTarget, ref _yawVel, orbitSmoothTime);
                _pitchCur = Mathf.SmoothDampAngle(_pitchCur, pitchTarget, ref _pitchVel, orbitSmoothTime);
                _distCur = Mathf.SmoothDamp(_distCur <= 0 ? distTarget : _distCur, distTarget, ref _distVel, orbitSmoothTime);

                var rot = Quaternion.Euler(_pitchCur, _yawCur, 0f);
                var offset = rot * new Vector3(0, 0, -_distCur);
                var camPos = orbitTarget.position + offset;
                cameraPivot.position = camPos;
                var up = orbitKeepHorizon ? Vector3.up : cameraPivot.up;
                cameraPivot.rotation = Quaternion.LookRotation(orbitTarget.position - camPos, up);
                return;
            }
        }

        private static float ApplyDeadzone(float v, float dz)
        {
            float a = Mathf.Abs(v);
            if (a <= dz) return 0f;
            return Mathf.Sign(v) * (a - dz);
        }

        private void AccumulateNeutral(HeadPose pose)
        {
            if (_countZ == 0)
            {
                _learnStartTs = Time.realtimeSinceStartupAsDouble;
            }
            _sumZ += pose.position.z;
            _sumX += pose.position.x;
            _sumY += pose.position.y;
            _countZ++;
            double elapsed = Time.realtimeSinceStartupAsDouble - _learnStartTs;
            if (elapsed >= Mathf.Max(0.1f, autoNeutralDuration))
            {
                float denom = Mathf.Max(1, _countZ);
                neutralZ = _sumZ / denom;
                neutralXY = new Vector2(_sumX / denom, _sumY / denom);
                StopNeutralLearning();
            }
        }

        public void StartNeutralLearning(float duration)
        {
            autoNeutralDuration = duration;
            _learningNeutral = true;
            _sumZ = 0f; _sumX = 0f; _sumY = 0f; _countZ = 0;
            _learnStartTs = Time.realtimeSinceStartupAsDouble;
            _snappedOnce = true; // prevent snap after auto learning starts
        }

        public void StopNeutralLearning()
        {
            _learningNeutral = false;
        }

        public void SetNeutralNow(float z)
        {
            neutralZ = z;
            _learningNeutral = false;
            _snappedOnce = true;
        }

        public void SetNeutralXY(float x, float y)
        {
            neutralXY = new Vector2(x, y);
            _snappedOnce = true;
        }

        // Persistence
        private const string PrefPrefix = "Belen/FaceTracker/";

        public void SaveCalibration(string prefix = PrefPrefix)
        {
            PlayerPrefs.SetFloat(prefix + "positionOffsetX", positionOffset.x);
            PlayerPrefs.SetFloat(prefix + "positionOffsetY", positionOffset.y);
            PlayerPrefs.SetFloat(prefix + "positionOffsetZ", positionOffset.z);
            PlayerPrefs.SetFloat(prefix + "rotationOffsetX", rotationOffset.x);
            PlayerPrefs.SetFloat(prefix + "rotationOffsetY", rotationOffset.y);
            PlayerPrefs.SetFloat(prefix + "rotationOffsetZ", rotationOffset.z);
            PlayerPrefs.SetFloat(prefix + "posAlpha", filter?.positionAlpha ?? 0.2f);
            PlayerPrefs.SetFloat(prefix + "rotAlpha", filter?.rotationAlpha ?? 0.2f);
            PlayerPrefs.SetInt(prefix + "useDistanceGain", useDistanceGain ? 1 : 0);
            PlayerPrefs.SetFloat(prefix + "neutralZ", neutralZ);
            PlayerPrefs.SetFloat(prefix + "distanceGain", distanceGain);
            PlayerPrefs.SetFloat(prefix + "zMin", zClamp.x);
            PlayerPrefs.SetFloat(prefix + "zMax", zClamp.y);
            PlayerPrefs.SetInt(prefix + "autoNeutral", autoNeutral ? 1 : 0);
            PlayerPrefs.SetFloat(prefix + "autoNeutralDuration", autoNeutralDuration);
            PlayerPrefs.SetFloat(prefix + "neutralX", neutralXY.x);
            PlayerPrefs.SetFloat(prefix + "neutralY", neutralXY.y);
            PlayerPrefs.SetInt(prefix + "motionMode", (int)motionMode);
            PlayerPrefs.SetFloat(prefix + "orbitBaseDistance", orbitBaseDistance);
            PlayerPrefs.SetFloat(prefix + "yawDegPerM", yawDegreesPerMeter);
            PlayerPrefs.SetFloat(prefix + "pitchDegPerM", pitchDegreesPerMeter);
            PlayerPrefs.SetFloat(prefix + "dollyPerM", dollyMetersPerMeter);
            PlayerPrefs.SetFloat(prefix + "pitchMin", pitchClamp.x);
            PlayerPrefs.SetFloat(prefix + "pitchMax", pitchClamp.y);
            PlayerPrefs.SetFloat(prefix + "distMin", distanceClamp.x);
            PlayerPrefs.SetFloat(prefix + "distMax", distanceClamp.y);
            PlayerPrefs.SetInt(prefix + "keepHorizon", orbitKeepHorizon ? 1 : 0);
            PlayerPrefs.SetFloat(prefix + "yawMin", yawClamp.x);
            PlayerPrefs.SetFloat(prefix + "yawMax", yawClamp.y);
            PlayerPrefs.SetFloat(prefix + "deadzoneX", deadzoneX);
            PlayerPrefs.SetFloat(prefix + "deadzoneY", deadzoneY);
            PlayerPrefs.SetFloat(prefix + "responseExp", responseExponent);
            PlayerPrefs.SetFloat(prefix + "smoothTime", orbitSmoothTime);
            PlayerPrefs.SetFloat(prefix + "compYaw", compositionOffsetDeg.x);
            PlayerPrefs.SetFloat(prefix + "compPitch", compositionOffsetDeg.y);
            PlayerPrefs.SetInt(prefix + "applyOrbitStart", applyOrbitStartOnEnable ? 1 : 0);
            PlayerPrefs.SetFloat(prefix + "orbitStartYaw", orbitStartYaw);
            PlayerPrefs.SetFloat(prefix + "orbitStartPitch", orbitStartPitch);
            PlayerPrefs.SetFloat(prefix + "orbitStartDist", orbitStartDistance);
            PlayerPrefs.Save();
        }

        public void LoadCalibration(string prefix = PrefPrefix)
        {
            if (PlayerPrefs.HasKey(prefix + "positionOffsetX"))
            {
                positionOffset = new Vector3(
                    PlayerPrefs.GetFloat(prefix + "positionOffsetX", positionOffset.x),
                    PlayerPrefs.GetFloat(prefix + "positionOffsetY", positionOffset.y),
                    PlayerPrefs.GetFloat(prefix + "positionOffsetZ", positionOffset.z)
                );
            }
            if (PlayerPrefs.HasKey(prefix + "rotationOffsetX"))
            {
                rotationOffset = new Vector3(
                    PlayerPrefs.GetFloat(prefix + "rotationOffsetX", rotationOffset.x),
                    PlayerPrefs.GetFloat(prefix + "rotationOffsetY", rotationOffset.y),
                    PlayerPrefs.GetFloat(prefix + "rotationOffsetZ", rotationOffset.z)
                );
            }
            if (filter != null)
            {
                if (PlayerPrefs.HasKey(prefix + "posAlpha"))
                    filter.positionAlpha = PlayerPrefs.GetFloat(prefix + "posAlpha", filter.positionAlpha);
                if (PlayerPrefs.HasKey(prefix + "rotAlpha"))
                    filter.rotationAlpha = PlayerPrefs.GetFloat(prefix + "rotAlpha", filter.rotationAlpha);
            }
            if (PlayerPrefs.HasKey(prefix + "useDistanceGain"))
                useDistanceGain = PlayerPrefs.GetInt(prefix + "useDistanceGain", useDistanceGain ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(prefix + "neutralZ"))
                neutralZ = PlayerPrefs.GetFloat(prefix + "neutralZ", neutralZ);
            if (PlayerPrefs.HasKey(prefix + "distanceGain"))
                distanceGain = PlayerPrefs.GetFloat(prefix + "distanceGain", distanceGain);
            if (PlayerPrefs.HasKey(prefix + "zMin") || PlayerPrefs.HasKey(prefix + "zMax"))
                zClamp = new Vector2(
                    PlayerPrefs.GetFloat(prefix + "zMin", zClamp.x),
                    PlayerPrefs.GetFloat(prefix + "zMax", zClamp.y)
                );
            if (PlayerPrefs.HasKey(prefix + "autoNeutral"))
                autoNeutral = PlayerPrefs.GetInt(prefix + "autoNeutral", autoNeutral ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(prefix + "autoNeutralDuration"))
                autoNeutralDuration = PlayerPrefs.GetFloat(prefix + "autoNeutralDuration", autoNeutralDuration);
            if (PlayerPrefs.HasKey(prefix + "neutralX"))
                neutralXY.x = PlayerPrefs.GetFloat(prefix + "neutralX", neutralXY.x);
            if (PlayerPrefs.HasKey(prefix + "neutralY"))
                neutralXY.y = PlayerPrefs.GetFloat(prefix + "neutralY", neutralXY.y);
            if (PlayerPrefs.HasKey(prefix + "motionMode"))
                motionMode = (MotionMode)PlayerPrefs.GetInt(prefix + "motionMode", (int)motionMode);
            if (PlayerPrefs.HasKey(prefix + "orbitBaseDistance"))
                orbitBaseDistance = PlayerPrefs.GetFloat(prefix + "orbitBaseDistance", orbitBaseDistance);
            if (PlayerPrefs.HasKey(prefix + "yawDegPerM"))
                yawDegreesPerMeter = PlayerPrefs.GetFloat(prefix + "yawDegPerM", yawDegreesPerMeter);
            if (PlayerPrefs.HasKey(prefix + "pitchDegPerM"))
                pitchDegreesPerMeter = PlayerPrefs.GetFloat(prefix + "pitchDegPerM", pitchDegreesPerMeter);
            if (PlayerPrefs.HasKey(prefix + "dollyPerM"))
                dollyMetersPerMeter = PlayerPrefs.GetFloat(prefix + "dollyPerM", dollyMetersPerMeter);
            if (PlayerPrefs.HasKey(prefix + "pitchMin") || PlayerPrefs.HasKey(prefix + "pitchMax"))
                pitchClamp = new Vector2(
                    PlayerPrefs.GetFloat(prefix + "pitchMin", pitchClamp.x),
                    PlayerPrefs.GetFloat(prefix + "pitchMax", pitchClamp.y)
                );
            if (PlayerPrefs.HasKey(prefix + "distMin") || PlayerPrefs.HasKey(prefix + "distMax"))
                distanceClamp = new Vector2(
                    PlayerPrefs.GetFloat(prefix + "distMin", distanceClamp.x),
                    PlayerPrefs.GetFloat(prefix + "distMax", distanceClamp.y)
                );
            if (PlayerPrefs.HasKey(prefix + "keepHorizon"))
                orbitKeepHorizon = PlayerPrefs.GetInt(prefix + "keepHorizon", orbitKeepHorizon ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(prefix + "yawMin") || PlayerPrefs.HasKey(prefix + "yawMax"))
                yawClamp = new Vector2(
                    PlayerPrefs.GetFloat(prefix + "yawMin", yawClamp.x),
                    PlayerPrefs.GetFloat(prefix + "yawMax", yawClamp.y)
                );
            if (PlayerPrefs.HasKey(prefix + "deadzoneX")) deadzoneX = PlayerPrefs.GetFloat(prefix + "deadzoneX", deadzoneX);
            if (PlayerPrefs.HasKey(prefix + "deadzoneY")) deadzoneY = PlayerPrefs.GetFloat(prefix + "deadzoneY", deadzoneY);
            if (PlayerPrefs.HasKey(prefix + "responseExp")) responseExponent = PlayerPrefs.GetFloat(prefix + "responseExp", responseExponent);
            if (PlayerPrefs.HasKey(prefix + "smoothTime")) orbitSmoothTime = PlayerPrefs.GetFloat(prefix + "smoothTime", orbitSmoothTime);
            if (PlayerPrefs.HasKey(prefix + "compYaw") || PlayerPrefs.HasKey(prefix + "compPitch"))
                compositionOffsetDeg = new Vector2(
                    PlayerPrefs.GetFloat(prefix + "compYaw", compositionOffsetDeg.x),
                    PlayerPrefs.GetFloat(prefix + "compPitch", compositionOffsetDeg.y)
                );
            if (PlayerPrefs.HasKey(prefix + "applyOrbitStart"))
                applyOrbitStartOnEnable = PlayerPrefs.GetInt(prefix + "applyOrbitStart", applyOrbitStartOnEnable ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(prefix + "orbitStartYaw")) orbitStartYaw = PlayerPrefs.GetFloat(prefix + "orbitStartYaw", orbitStartYaw);
            if (PlayerPrefs.HasKey(prefix + "orbitStartPitch")) orbitStartPitch = PlayerPrefs.GetFloat(prefix + "orbitStartPitch", orbitStartPitch);
            if (PlayerPrefs.HasKey(prefix + "orbitStartDist")) orbitStartDistance = PlayerPrefs.GetFloat(prefix + "orbitStartDist", orbitStartDistance);
        }

        public void ClearCalibration(string prefix = PrefPrefix)
        {
            string[] keys =
            {
                "positionOffsetX","positionOffsetY","positionOffsetZ",
                "rotationOffsetX","rotationOffsetY","rotationOffsetZ",
                "posAlpha","rotAlpha","useDistanceGain","neutralZ","distanceGain","zMin","zMax","autoNeutral","autoNeutralDuration",
                "neutralX","neutralY","motionMode","orbitBaseDistance","yawDegPerM","pitchDegPerM","dollyPerM","pitchMin","pitchMax","distMin","distMax",
                "keepHorizon","yawMin","yawMax","deadzoneX","deadzoneY","responseExp","smoothTime","compYaw","compPitch"
            };
            foreach (var k in keys) PlayerPrefs.DeleteKey(prefix + k);
        }

        // Orbit helpers
        public void SetOrbitStartFromCurrent()
        {
            orbitStartYaw = _yawCur;
            orbitStartPitch = _pitchCur;
            orbitStartDistance = _distCur > 0 ? _distCur : orbitBaseDistance;
        }

        public void ApplyOrbitStartNow()
        {
            if (motionMode != MotionMode.OrbitTarget || orbitTarget == null || cameraPivot == null) return;
            _yawCur = orbitStartYaw;
            _pitchCur = orbitStartPitch;
            _distCur = Mathf.Clamp(orbitStartDistance > 0 ? orbitStartDistance : orbitBaseDistance, distanceClamp.x, distanceClamp.y);
            var rot = Quaternion.Euler(_pitchCur, _yawCur, 0f);
            var offset = rot * new Vector3(0, 0, -_distCur);
            var camPos = orbitTarget.position + offset;
            cameraPivot.position = camPos;
            var up = orbitKeepHorizon ? Vector3.up : cameraPivot.up;
            cameraPivot.rotation = Quaternion.LookRotation(orbitTarget.position - camPos, up);
        }
    }
}
