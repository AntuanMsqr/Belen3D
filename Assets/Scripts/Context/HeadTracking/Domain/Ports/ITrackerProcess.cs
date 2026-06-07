namespace Hcp.HeadTracking.Domain
{
    // Port: the external face-tracker process (e.g. OpenSeeFace facetracker.exe).
    // Implemented in Infrastructure with System.Diagnostics.Process.
    public interface ITrackerProcess
    {
        void Start();
        void Stop();
        bool IsRunning { get; }
    }
}
