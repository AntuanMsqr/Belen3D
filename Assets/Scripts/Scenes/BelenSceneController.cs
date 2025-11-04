using System.Collections;
using UnityEngine;

namespace Belen.Scenes
{
    public enum BelenScene
    {
        Anunciacion = 0,
        CaminoABelen = 1,
        Nacimiento = 2,
        AnuncioPastores = 3,
        AdoracionReyes = 4,
    }

    // Lightweight scene flow controller: cycles through 5 tableau states, triggers audio crossfades.
    public class BelenSceneController : MonoBehaviour
    {
        [System.Serializable]
        public class SceneDef
        {
            public string name;
            public float duration = 22f;
            public AudioClip music;
            public GameObject[] roots; // root objects to enable for this scene
        }

        public SceneDef[] scenes = new SceneDef[5];
        public Belen.Audio.AudioCrossfader crossfader;
        public float introFade = 1.0f;
        public float betweenFade = 1.0f;

        private int _index;

        private void Start()
        {
            // Ensure exactly five scenes
            if (scenes == null || scenes.Length == 0)
            {
                scenes = new SceneDef[5];
            }
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (true)
            {
                Activate(_index);
                var def = scenes[_index];
                if (crossfader != null && def.music != null)
                {
                    if (_index == 0) crossfader.PlayImmediate(def.music, 1f);
                    else crossfader.CrossfadeTo(def.music, 1f);
                }

                float dur = Mathf.Max(1f, def.duration);
                yield return new WaitForSeconds(dur);

                _index = (_index + 1) % scenes.Length;
            }
        }

        private void Activate(int i)
        {
            for (int s = 0; s < scenes.Length; s++)
            {
                var def = scenes[s];
                if (def?.roots == null) continue;
                bool on = (s == i);
                foreach (var go in def.roots)
                {
                    if (go != null) go.SetActive(on);
                }
            }
        }
    }
}

