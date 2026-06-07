using Hcp.Narrative.Domain;

namespace Hcp.Narrative.Application
{
    // Advances the tableau timeline. Plain class; ticked by a View.
    public sealed class NarrativeService
    {
        private readonly SceneTimeline timeline;

        public int CurrentIndex => timeline.CurrentIndex;
        public int Count => timeline.Count;

        public NarrativeService(float[] durations)
        {
            timeline = new SceneTimeline(durations);
        }

        public bool Tick(float deltaTime, out int sceneIndex, out bool isFirst)
        {
            return timeline.Tick(deltaTime, out sceneIndex, out isFirst);
        }
    }
}
