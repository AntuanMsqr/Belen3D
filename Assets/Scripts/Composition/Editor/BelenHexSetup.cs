using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Belen.HeadTracking.Application;
using Belen.HeadTracking.Infrastructure;

namespace Belen.Composition.Editor
{
    // No-MCP setup helper: creates the HeadTrackingConfig asset and a clean BelenHex scene
    // wired with the composition root. Run from the Belen menu.
    public static class BelenHexSetup
    {
        private const string ConfigPath = "Assets/Settings/HeadTrackingConfig.asset";
        private const string ScenePath = "Assets/Scenes/BelenHex.unity";

        [MenuItem("Belen/Setup HeadTracking Hex Scene")]
        public static void SetupScene()
        {
            var config = GetOrCreateConfig();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Main Camera in-scene so the Game view renders in edit mode; the Bootstrap reuses it.
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.01f;
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0f, 0.2f, 0.6f);

            var bootstrapGO = new GameObject("Bootstrap");
            var bootstrap = bootstrapGO.AddComponent<BelenHeadTrackingBootstrap>();
            bootstrap.config = config;

            // Content so head motion is visible.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DemoCube";
            cube.transform.position = new Vector3(0f, 0f, -1f);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[BelenHexSetup] Scene saved at {ScenePath} with config {ConfigPath}. Press Play to test.");
        }

        [MenuItem("Belen/Create HeadTracking Config")]
        public static HeadTrackingConfig GetOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HeadTrackingConfig>(ConfigPath);
            if (existing != null) return existing;

            Directory.CreateDirectory("Assets/Settings");
            var config = ScriptableObject.CreateInstance<HeadTrackingConfig>();
            config.source = SourceKind.OpenSeeFaceBox;
            config.autoLaunchTracker = true;
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BelenHexSetup] Created config at {ConfigPath}");
            return config;
        }
    }
}
