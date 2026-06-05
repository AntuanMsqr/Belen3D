using Belen.Narrative.Domain;

namespace Belen.Narrative.Application
{
    // Advances the tableau timeline. Plain class; ticked by a View.
    public sealed class NarrativeService
    {
        private readonly SceneTimeline _timeline;

        public int CurrentIndex => _timeline.CurrentIndex;
        public int Count => _timeline.Count;

        public NarrativeService(float[] durations)
        {
            _timeline = new SceneTimeline(durations);
        }

        public bool Tick(float deltaTime, out int sceneIndex, out bool isFirst)
        {
            return _timeline.Tick(deltaTime, out sceneIndex, out isFirst);
        }
    }
}
