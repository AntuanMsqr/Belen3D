using UnityEngine;

namespace Hcp.Narrative.Infrastructure
{
    // Crossfades between ambient tracks using two AudioSources. View (Infra).
    [RequireComponent(typeof(AudioSource))]
    public class AudioCrossfaderView : MonoBehaviour
    {
        public float fadeDuration = 2.0f;

        private AudioSource a;
        private AudioSource b;
        private AudioSource active;
        private float fadeTime;
        private bool isFading;

        private void Awake()
        {
            a = GetComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = true;
            b = gameObject.AddComponent<AudioSource>();
            b.playOnAwake = false;
            b.loop = true;
            active = a;
        }

        public void PlayImmediate(AudioClip clip, float volume = 1f)
        {
            a.Stop(); b.Stop();
            active = a;
            a.clip = clip; a.volume = volume; a.Play();
            b.clip = null; b.volume = 0f;
            isFading = false; fadeTime = 0f;
        }

        public void CrossfadeTo(AudioClip clip, float volume = 1f)
        {
            var next = active == a ? b : a;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            isFading = true;
            fadeTime = 0f;
        }

        private void Update()
        {
            if (!isFading) return;
            fadeTime += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTime / Mathf.Max(0.01f, fadeDuration));
            var from = active;
            var to = active == a ? b : a;
            to.volume = t;
            from.volume = 1f - t;
            if (t >= 1f)
            {
                from.Stop();
                active = to;
                isFading = false;
            }
        }
    }
}
