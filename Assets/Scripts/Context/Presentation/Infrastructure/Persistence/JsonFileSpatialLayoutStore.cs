using System;
using System.IO;
using UnityEngine;
using Hcp.Presentation.Domain;

namespace Hcp.Presentation.Infrastructure
{
    // ISpatialLayoutStore backed by a JSON file under persistentDataPath.
    // Chosen over PlayerPrefs because the layout is a structured aggregate with a
    // growing camera list (flat PlayerPrefs keys would be painful). FromJsonOverwrite
    // overlays persisted values onto the config-seeded instance in place.
    public class JsonFileSpatialLayoutStore : ISpatialLayoutStore
    {
        private readonly string _path;

        // Per-scene layout: file name keyed by scene so each scene has its own config.
        public JsonFileSpatialLayoutStore(string sceneKey = "default")
        {
            string safe = Sanitize(string.IsNullOrWhiteSpace(sceneKey) ? "default" : sceneKey);
            _path = Path.Combine(UnityEngine.Application.persistentDataPath, $"spatial-layout-{safe}.json");
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        public bool TryLoad(SpatialLayout layout)
        {
            if (layout == null) return false;
            try
            {
                if (!File.Exists(_path)) return false;
                var json = File.ReadAllText(_path);
                if (string.IsNullOrWhiteSpace(json)) return false;
                JsonUtility.FromJsonOverwrite(json, layout);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpatialLayout] load failed: {e.Message}");
                return false;
            }
        }

        public void Save(SpatialLayout layout)
        {
            if (layout == null) return;
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(layout, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpatialLayout] save failed: {e.Message}");
            }
        }

        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch (Exception e) { Debug.LogWarning($"[SpatialLayout] clear failed: {e.Message}"); }
        }
    }
}
