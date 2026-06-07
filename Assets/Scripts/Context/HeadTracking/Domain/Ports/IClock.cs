namespace Hcp.HeadTracking.Domain
{
    // Port: a monotonic time source, so Application logic stays free of UnityEngine.Time.
    public interface IClock
    {
        double NowSeconds { get; }
    }
}
