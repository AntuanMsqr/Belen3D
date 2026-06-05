using UnityEngine;
using Belen.HeadTracking.Domain;

namespace Belen.HeadTracking.Infrastructure
{
    // ICalibrationStore backed by PlayerPrefs.
    // Keeps the legacy FaceTrackerManager prefix and key names so existing user
    // calibrations keep loading after the migration.
    public sealed class PlayerPrefsCalibrationStore : ICalibrationStore
    {
        private const string Prefix = "Belen/FaceTracker/";

        public bool TryLoad(ref CalibrationData d)
        {
            if (!PlayerPrefs.HasKey(Prefix + "positionOffsetX") &&
                !PlayerPrefs.HasKey(Prefix + "motionMode") &&
                !PlayerPrefs.HasKey(Prefix + "neutralZ"))
            {
                return false; // nothing persisted
            }

            if (PlayerPrefs.HasKey(Prefix + "positionOffsetX"))
                d.positionOffset = new Vector3(
                    PlayerPrefs.GetFloat(Prefix + "positionOffsetX", d.positionOffset.x),
                    PlayerPrefs.GetFloat(Prefix + "positionOffsetY", d.positionOffset.y),
                    PlayerPrefs.GetFloat(Prefix + "positionOffsetZ", d.positionOffset.z));
            if (PlayerPrefs.HasKey(Prefix + "rotationOffsetX"))
                d.rotationOffset = new Vector3(
                    PlayerPrefs.GetFloat(Prefix + "rotationOffsetX", d.rotationOffset.x),
                    PlayerPrefs.GetFloat(Prefix + "rotationOffsetY", d.rotationOffset.y),
                    PlayerPrefs.GetFloat(Prefix + "rotationOffsetZ", d.rotationOffset.z));

            if (PlayerPrefs.HasKey(Prefix + "posAlpha")) d.positionAlpha = PlayerPrefs.GetFloat(Prefix + "posAlpha", d.positionAlpha);
            if (PlayerPrefs.HasKey(Prefix + "rotAlpha")) d.rotationAlpha = PlayerPrefs.GetFloat(Prefix + "rotAlpha", d.rotationAlpha);

            if (PlayerPrefs.HasKey(Prefix + "useDistanceGain")) d.useDistanceGain = PlayerPrefs.GetInt(Prefix + "useDistanceGain", d.useDistanceGain ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(Prefix + "neutralZ")) d.neutralZ = PlayerPrefs.GetFloat(Prefix + "neutralZ", d.neutralZ);
            if (PlayerPrefs.HasKey(Prefix + "distanceGain")) d.distanceGain = PlayerPrefs.GetFloat(Prefix + "distanceGain", d.distanceGain);
            if (PlayerPrefs.HasKey(Prefix + "zMin") || PlayerPrefs.HasKey(Prefix + "zMax"))
                d.zClamp = new Vector2(PlayerPrefs.GetFloat(Prefix + "zMin", d.zClamp.x), PlayerPrefs.GetFloat(Prefix + "zMax", d.zClamp.y));

            if (PlayerPrefs.HasKey(Prefix + "autoNeutral")) d.autoNeutral = PlayerPrefs.GetInt(Prefix + "autoNeutral", d.autoNeutral ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(Prefix + "autoNeutralDuration")) d.autoNeutralDuration = PlayerPrefs.GetFloat(Prefix + "autoNeutralDuration", d.autoNeutralDuration);
            if (PlayerPrefs.HasKey(Prefix + "neutralX")) d.neutralXY.x = PlayerPrefs.GetFloat(Prefix + "neutralX", d.neutralXY.x);
            if (PlayerPrefs.HasKey(Prefix + "neutralY")) d.neutralXY.y = PlayerPrefs.GetFloat(Prefix + "neutralY", d.neutralXY.y);

            if (PlayerPrefs.HasKey(Prefix + "motionMode")) d.motionMode = (MotionMode)PlayerPrefs.GetInt(Prefix + "motionMode", (int)d.motionMode);
            if (PlayerPrefs.HasKey(Prefix + "orbitBaseDistance")) d.orbitBaseDistance = PlayerPrefs.GetFloat(Prefix + "orbitBaseDistance", d.orbitBaseDistance);
            if (PlayerPrefs.HasKey(Prefix + "yawDegPerM")) d.yawDegreesPerMeter = PlayerPrefs.GetFloat(Prefix + "yawDegPerM", d.yawDegreesPerMeter);
            if (PlayerPrefs.HasKey(Prefix + "pitchDegPerM")) d.pitchDegreesPerMeter = PlayerPrefs.GetFloat(Prefix + "pitchDegPerM", d.pitchDegreesPerMeter);
            if (PlayerPrefs.HasKey(Prefix + "dollyPerM")) d.dollyMetersPerMeter = PlayerPrefs.GetFloat(Prefix + "dollyPerM", d.dollyMetersPerMeter);
            if (PlayerPrefs.HasKey(Prefix + "pitchMin") || PlayerPrefs.HasKey(Prefix + "pitchMax"))
                d.pitchClamp = new Vector2(PlayerPrefs.GetFloat(Prefix + "pitchMin", d.pitchClamp.x), PlayerPrefs.GetFloat(Prefix + "pitchMax", d.pitchClamp.y));
            if (PlayerPrefs.HasKey(Prefix + "distMin") || PlayerPrefs.HasKey(Prefix + "distMax"))
                d.distanceClamp = new Vector2(PlayerPrefs.GetFloat(Prefix + "distMin", d.distanceClamp.x), PlayerPrefs.GetFloat(Prefix + "distMax", d.distanceClamp.y));
            if (PlayerPrefs.HasKey(Prefix + "keepHorizon")) d.orbitKeepHorizon = PlayerPrefs.GetInt(Prefix + "keepHorizon", d.orbitKeepHorizon ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(Prefix + "yawMin") || PlayerPrefs.HasKey(Prefix + "yawMax"))
                d.yawClamp = new Vector2(PlayerPrefs.GetFloat(Prefix + "yawMin", d.yawClamp.x), PlayerPrefs.GetFloat(Prefix + "yawMax", d.yawClamp.y));
            if (PlayerPrefs.HasKey(Prefix + "deadzoneX")) d.deadzoneX = PlayerPrefs.GetFloat(Prefix + "deadzoneX", d.deadzoneX);
            if (PlayerPrefs.HasKey(Prefix + "deadzoneY")) d.deadzoneY = PlayerPrefs.GetFloat(Prefix + "deadzoneY", d.deadzoneY);
            if (PlayerPrefs.HasKey(Prefix + "responseExp")) d.responseExponent = PlayerPrefs.GetFloat(Prefix + "responseExp", d.responseExponent);
            if (PlayerPrefs.HasKey(Prefix + "smoothTime")) d.orbitSmoothTime = PlayerPrefs.GetFloat(Prefix + "smoothTime", d.orbitSmoothTime);
            if (PlayerPrefs.HasKey(Prefix + "compYaw") || PlayerPrefs.HasKey(Prefix + "compPitch"))
                d.compositionOffsetDeg = new Vector2(PlayerPrefs.GetFloat(Prefix + "compYaw", d.compositionOffsetDeg.x), PlayerPrefs.GetFloat(Prefix + "compPitch", d.compositionOffsetDeg.y));
            if (PlayerPrefs.HasKey(Prefix + "applyOrbitStart")) d.applyOrbitStartOnEnable = PlayerPrefs.GetInt(Prefix + "applyOrbitStart", d.applyOrbitStartOnEnable ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(Prefix + "orbitStartYaw")) d.orbitStartYaw = PlayerPrefs.GetFloat(Prefix + "orbitStartYaw", d.orbitStartYaw);
            if (PlayerPrefs.HasKey(Prefix + "orbitStartPitch")) d.orbitStartPitch = PlayerPrefs.GetFloat(Prefix + "orbitStartPitch", d.orbitStartPitch);
            if (PlayerPrefs.HasKey(Prefix + "orbitStartDist")) d.orbitStartDistance = PlayerPrefs.GetFloat(Prefix + "orbitStartDist", d.orbitStartDistance);

            return true;
        }

        public void Save(in CalibrationData d)
        {
            PlayerPrefs.SetFloat(Prefix + "positionOffsetX", d.positionOffset.x);
            PlayerPrefs.SetFloat(Prefix + "positionOffsetY", d.positionOffset.y);
            PlayerPrefs.SetFloat(Prefix + "positionOffsetZ", d.positionOffset.z);
            PlayerPrefs.SetFloat(Prefix + "rotationOffsetX", d.rotationOffset.x);
            PlayerPrefs.SetFloat(Prefix + "rotationOffsetY", d.rotationOffset.y);
            PlayerPrefs.SetFloat(Prefix + "rotationOffsetZ", d.rotationOffset.z);
            PlayerPrefs.SetFloat(Prefix + "posAlpha", d.positionAlpha);
            PlayerPrefs.SetFloat(Prefix + "rotAlpha", d.rotationAlpha);
            PlayerPrefs.SetInt(Prefix + "useDistanceGain", d.useDistanceGain ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "neutralZ", d.neutralZ);
            PlayerPrefs.SetFloat(Prefix + "distanceGain", d.distanceGain);
            PlayerPrefs.SetFloat(Prefix + "zMin", d.zClamp.x);
            PlayerPrefs.SetFloat(Prefix + "zMax", d.zClamp.y);
            PlayerPrefs.SetInt(Prefix + "autoNeutral", d.autoNeutral ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "autoNeutralDuration", d.autoNeutralDuration);
            PlayerPrefs.SetFloat(Prefix + "neutralX", d.neutralXY.x);
            PlayerPrefs.SetFloat(Prefix + "neutralY", d.neutralXY.y);
            PlayerPrefs.SetInt(Prefix + "motionMode", (int)d.motionMode);
            PlayerPrefs.SetFloat(Prefix + "orbitBaseDistance", d.orbitBaseDistance);
            PlayerPrefs.SetFloat(Prefix + "yawDegPerM", d.yawDegreesPerMeter);
            PlayerPrefs.SetFloat(Prefix + "pitchDegPerM", d.pitchDegreesPerMeter);
            PlayerPrefs.SetFloat(Prefix + "dollyPerM", d.dollyMetersPerMeter);
            PlayerPrefs.SetFloat(Prefix + "pitchMin", d.pitchClamp.x);
            PlayerPrefs.SetFloat(Prefix + "pitchMax", d.pitchClamp.y);
            PlayerPrefs.SetFloat(Prefix + "distMin", d.distanceClamp.x);
            PlayerPrefs.SetFloat(Prefix + "distMax", d.distanceClamp.y);
            PlayerPrefs.SetInt(Prefix + "keepHorizon", d.orbitKeepHorizon ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "yawMin", d.yawClamp.x);
            PlayerPrefs.SetFloat(Prefix + "yawMax", d.yawClamp.y);
            PlayerPrefs.SetFloat(Prefix + "deadzoneX", d.deadzoneX);
            PlayerPrefs.SetFloat(Prefix + "deadzoneY", d.deadzoneY);
            PlayerPrefs.SetFloat(Prefix + "responseExp", d.responseExponent);
            PlayerPrefs.SetFloat(Prefix + "smoothTime", d.orbitSmoothTime);
            PlayerPrefs.SetFloat(Prefix + "compYaw", d.compositionOffsetDeg.x);
            PlayerPrefs.SetFloat(Prefix + "compPitch", d.compositionOffsetDeg.y);
            PlayerPrefs.SetInt(Prefix + "applyOrbitStart", d.applyOrbitStartOnEnable ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "orbitStartYaw", d.orbitStartYaw);
            PlayerPrefs.SetFloat(Prefix + "orbitStartPitch", d.orbitStartPitch);
            PlayerPrefs.SetFloat(Prefix + "orbitStartDist", d.orbitStartDistance);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            string[] keys =
            {
                "positionOffsetX","positionOffsetY","positionOffsetZ",
                "rotationOffsetX","rotationOffsetY","rotationOffsetZ",
                "posAlpha","rotAlpha","useDistanceGain","neutralZ","distanceGain","zMin","zMax",
                "autoNeutral","autoNeutralDuration","neutralX","neutralY","motionMode",
                "orbitBaseDistance","yawDegPerM","pitchDegPerM","dollyPerM","pitchMin","pitchMax",
                "distMin","distMax","keepHorizon","yawMin","yawMax","deadzoneX","deadzoneY",
                "responseExp","smoothTime","compYaw","compPitch",
                "applyOrbitStart","orbitStartYaw","orbitStartPitch","orbitStartDist"
            };
            foreach (var k in keys) PlayerPrefs.DeleteKey(Prefix + k);
            PlayerPrefs.Save();
        }
    }
}
