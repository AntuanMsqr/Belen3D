using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Maps a filtered head pose to a CameraTarget (plain data — never a Transform).
    // Holds the orbit smoothing state that used to live on FaceTrackerManager.
    public sealed class CameraMappingService
    {
        private float _yawCur, _pitchCur, _distCur;
        private float _yawVel, _pitchVel, _distVel;

        // Reproduces FaceTrackerManager.applyOrbitStartOnEnable initialization.
        public void ResetOrbit(in CalibrationData cal)
        {
            _yawCur = cal.orbitStartYaw;
            _pitchCur = cal.orbitStartPitch;
            _distCur = Mathf.Clamp(cal.orbitStartDistance > 0 ? cal.orbitStartDistance : cal.orbitBaseDistance,
                                   cal.distanceClamp.x, cal.distanceClamp.y);
            _yawVel = _pitchVel = _distVel = 0f;
        }

        public CameraTarget Map(in HeadPose pose, in CalibrationData cal, float deltaTime)
        {
            if (cal.motionMode == MotionMode.Direct)
                return MapDirect(pose, cal);

            return MapOrbit(pose, cal, deltaTime);
        }

        private static CameraTarget MapDirect(in HeadPose pose, in CalibrationData cal)
        {
            // Position relative to the learned neutral, around a base camera position (positionOffset).
            // At neutral the camera sits at positionOffset looking along rotationOffset; head motion
            // shifts it by the delta from neutral, so the camera moves like the viewer's real head.
            var neutralPos = new Vector3(cal.neutralXY.x, cal.neutralXY.y, cal.neutralZ);
            var delta = pose.position - neutralPos;
            if (cal.useDistanceGain)
                delta.z = Mathf.Clamp(delta.z * cal.distanceGain, cal.zClamp.x - 1f, cal.zClamp.y);
            var pos = cal.positionOffset + delta;

            // Base orientation (rotationOffset, e.g. look toward the scene) + head rotation delta.
            var rot = Quaternion.Euler(cal.rotationOffset);
            if (cal.applyHeadRotation)
                rot = rot * Quaternion.Euler(pose.eulerDegrees - cal.neutralEuler);

            return CameraTarget.Direct(pos, rot, true);
        }

        private CameraTarget MapOrbit(in HeadPose pose, in CalibrationData cal, float deltaTime)
        {
            float dx = pose.position.x - cal.neutralXY.x;
            float dy = pose.position.y - cal.neutralXY.y;
            float dz = pose.position.z - cal.neutralZ;

            dx = ApplyDeadzone(dx, cal.deadzoneX);
            dy = ApplyDeadzone(dy, cal.deadzoneY);
            if (!Mathf.Approximately(cal.responseExponent, 1f))
            {
                float e = Mathf.Max(0.01f, cal.responseExponent);
                dx = Mathf.Sign(dx) * Mathf.Pow(Mathf.Abs(dx), e);
                dy = Mathf.Sign(dy) * Mathf.Pow(Mathf.Abs(dy), e);
            }

            float yawTarget = Mathf.Clamp(dx * cal.yawDegreesPerMeter + cal.compositionOffsetDeg.x, cal.yawClamp.x, cal.yawClamp.y);
            float pitchTarget = Mathf.Clamp(-dy * cal.pitchDegreesPerMeter + cal.compositionOffsetDeg.y, cal.pitchClamp.x, cal.pitchClamp.y);
            float distTarget = Mathf.Clamp(cal.orbitBaseDistance + dz * cal.dollyMetersPerMeter, cal.distanceClamp.x, cal.distanceClamp.y);

            float smooth = cal.orbitSmoothTime;
            // Pass deltaTime explicitly: the default overload would read UnityEngine.Time, banned in Application.
            _yawCur = Mathf.SmoothDampAngle(_yawCur, yawTarget, ref _yawVel, smooth, Mathf.Infinity, deltaTime);
            _pitchCur = Mathf.SmoothDampAngle(_pitchCur, pitchTarget, ref _pitchVel, smooth, Mathf.Infinity, deltaTime);
            float distStart = _distCur <= 0 ? distTarget : _distCur;
            _distCur = Mathf.SmoothDamp(distStart, distTarget, ref _distVel, smooth, Mathf.Infinity, deltaTime);

            return CameraTarget.Orbit(_yawCur, _pitchCur, _distCur, cal.orbitKeepHorizon);
        }

        private static float ApplyDeadzone(float v, float dz)
        {
            float a = Mathf.Abs(v);
            if (a <= dz) return 0f;
            return Mathf.Sign(v) * (a - dz);
        }
    }
}
