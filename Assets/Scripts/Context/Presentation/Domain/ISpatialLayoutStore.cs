namespace Hcp.Presentation.Domain
{
    // Port: persistence for the spatial layout. Implemented in Infrastructure
    // (e.g. a JSON file) so Application never touches engine storage APIs.
    public interface ISpatialLayoutStore
    {
        // Overlays any persisted values onto the provided instance (seeded from config).
        // Returns true if a stored layout was applied.
        bool TryLoad(SpatialLayout layout);
        void Save(SpatialLayout layout);
        void Clear();
    }
}
