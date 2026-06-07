using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Application
{
    // Which head-pose source the rig should use.
    public enum SourceKind
    {
        OpenSeePose,    // OpenSee 3D translation/rotation
        OpenSeeFaceBox, // OpenSee 2D face box -> pose
        UdpPose,        // generic JSON/CSV UDP head pose
        UdpFaceBox,     // generic 2D face box over UDP
        Keyboard        // keyboard emulator (no tracker)
    }

    // Plain settings the Bootstrap maps from the Infrastructure ScriptableObject.
    public struct HeadTrackingSettings
    {
        public SourceKind source;
        public CalibrationData calibration;

        public static HeadTrackingSettings Defaults()
        {
            return new HeadTrackingSettings
            {
                source = SourceKind.OpenSeeFaceBox,
                calibration = CalibrationData.Defaults()
            };
        }
    }
}
