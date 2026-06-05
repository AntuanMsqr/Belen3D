using UnityEngine;

namespace Belen.Narrative.Infrastructure
{
    // Crossfades between ambient tracks using two AudioSources. View (Infra).
    [RequireComponent(typeof(AudioSource))]
    public class AudioCrossfaderView : MonoBehaviour
    {
        public float fadeDuration = 2.0f;

        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _active;
        private float _fadeTime;
        private bool _isFading;

        private void Awake()
        {
            _a = GetComponent<AudioSource>();
            _a.playOnAwake = false;
            _a.loop = true;
            _b = gameObject.AddComponent<AudioSource>();
            _b.playOnAwake = false;
            _b.loop = true;
            _active = _a;
        }

        public void PlayImmediate(AudioClip clip, float volume = 1f)
        {
            _a.Stop(); _b.Stop();
            _active = _a;
            _a.clip = clip; _a.volume = volume; _a.Play();
            _b.clip = null; _b.volume = 0f;
            _isFading = false; _fadeTime = 0f;
        }

        public void CrossfadeTo(AudioClip clip, float volume = 1f)
        {
            var next = _active == _a ? _b : _a;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _isFading = true;
            _fadeTime = 0f;
        }

        private void Update()
        {
            if (!_isFading) return;
            _fadeTime += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTime / Mathf.Max(0.01f, fadeDuration));
            var from = _active;
            var to = _active == _a ? _b : _a;
            to.volume = t;
            from.volume = 1f - t;
            if (t >= 1f)
            {
                from.Stop();
                _active = to;
                _isFading = false;
            }
        }
    }
}
