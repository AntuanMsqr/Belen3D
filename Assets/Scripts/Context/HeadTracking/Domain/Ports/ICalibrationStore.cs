namespace Hcp.HeadTracking.Domain
{
    // Port: persistence for calibration/tuning. Implemented in Infrastructure
    // (e.g. PlayerPrefs) so Application never touches engine storage APIs.
    public interface ICalibrationStore
    {
        // Overlays any persisted values onto the provided base (seeded from config).
        // Returns true if at least one stored value was applied.
        bool TryLoad(ref CalibrationData data);
        void Save(in CalibrationData data);
        void Clear();
    }
}
