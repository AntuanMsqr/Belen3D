namespace Belen.HeadTracking.Domain
{
    // How head motion maps to the camera rig.
    public enum MotionMode
    {
        Direct,      // head pose drives the camera pivot local transform directly
        OrbitTarget  // head deltas orbit the camera around a target
    }
}
