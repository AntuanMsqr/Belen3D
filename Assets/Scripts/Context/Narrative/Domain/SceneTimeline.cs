using UnityEngine;

namespace Hcp.Narrative.Domain
{
    // Pure tableau timeline: cycles through N scenes by duration. No engine services.
    public sealed class SceneTimeline
    {
        private readonly float[] _durations;
        private int _index;
        private float _elapsed;
        private bool _started;

        public int CurrentIndex => _index;
        public int Count => _durations?.Length ?? 0;

        public SceneTimeline(float[] durations)
        {
            _durations = durations;
        }

        // Returns true when a scene becomes active (first activation or after a duration elapses).
        public bool Tick(float deltaTime, out int index, out bool isFirst)
        {
            index = _index;
            isFirst = false;
            if (_durations == null || _durations.Length == 0) return false;

            if (!_started)
            {
                _started = true;
                _elapsed = 0f;
                isFirst = true;
                index = _index;
                return true;
            }

            _elapsed += deltaTime;
            if (_elapsed >= Mathf.Max(1f, _durations[_index]))
            {
                _elapsed = 0f;
                _index = (_index + 1) % _durations.Length;
                index = _index;
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
