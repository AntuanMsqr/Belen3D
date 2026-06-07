using System;
using UnityEngine;
using Hcp.Narrative.Application;

namespace Hcp.Narrative.Infrastructure
{
    // View: drives the tableau flow. Holds the scene roots + audio, pumps NarrativeService,
    // and on each scene change activates the right roots and crossfades the music.
    public class SceneFlowView : MonoBehaviour
    {
        [Serializable]
        public class SceneDef
        {
            public string name;
            public float duration = 22f;
            public AudioClip music;
            public GameObject[] roots;
        }

        public SceneDef[] scenes = new SceneDef[0];
        public AudioCrossfaderView crossfader;

        private NarrativeService service;

        private void Start()
        {
            var durations = new float[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                durations[i] = Mathf.Max(1f, scenes[i]?.duration ?? 22f);
            service = new NarrativeService(durations);
        }

        private void Update()
        {
            if (service == null || scenes.Length == 0) return;
            if (service.Tick(Time.deltaTime, out int index, out bool isFirst))
            {
                Activate(index);
                var def = scenes[index];
                if (crossfader != null && def?.music != null)
                {
                    if (isFirst) crossfader.PlayImmediate(def.music, 1f);
                    else crossfader.CrossfadeTo(def.music, 1f);
                }
            }
        }

        private void Activate(int active)
        {
            for (int s = 0; s < scenes.Length; s++)
            {
                var def = scenes[s];
                if (def?.roots == null) continue;
                bool on = (s == active);
                foreach (var go in def.roots)
                    if (go != null) go.SetActive(on);
            }
        }
    }
}
