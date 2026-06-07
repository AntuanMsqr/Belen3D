using UnityEngine;

namespace Hcp.Narrative.Domain
{
    // Pure tableau timeline: cycles through N scenes by duration. No engine services.
    public sealed class SceneTimeline
    {
        private readonly float[] durations;
        private int index;
        private float elapsed;
        private bool started;

        public int CurrentIndex => index;
        public int Count => durations?.Length ?? 0;

        public SceneTimeline(float[] durations)
        {
            this.durations = durations;
        }

        // Returns true when a scene becomes active (first activation or after a duration elapses).
        public bool Tick(float deltaTime, out int index, out bool isFirst)
        {
            index = this.index;
            isFirst = false;
            if (durations == null || durations.Length == 0) return false;

            if (!started)
            {
                started = true;
                elapsed = 0f;
                isFirst = true;
                index = this.index;
                return true;
            }

            elapsed += deltaTime;
            if (elapsed >= Mathf.Max(1f, durations[this.index]))
            {
                elapsed = 0f;
                this.index = (this.index + 1) % durations.Length;
                index = this.index;
                return true;
            }
            return false;
        }
    }

    // Pure fade policy for the idle prompt.
    public static class PromptFade
    {
        public static float Step(float alpha, bool present, float speed, float deltaTime)
        {
            float target = present ? 0f : 1f;
            return Mathf.MoveTowards(alpha, target, speed * deltaTime);
        }
    }
}
