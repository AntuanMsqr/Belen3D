using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Hcp.HeadTracking.Application;
using Hcp.HeadTracking.Infrastructure;
using Hcp.Presentation.Infrastructure;

namespace Hcp.Composition.Editor
{
    // Editor setup for the HCP flow: builds the Menu + Configurator scenes, a demo content
    // scene, the shared config/panel/catalog assets, and keeps the scene catalog + Build
    // Settings in sync with Assets/Scenes. Run from the HCP menu.
    public static class HcpSceneSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string ConfigPath = SettingsDir + "/HeadTrackingConfig.asset";
        private const string SpatialConfigPath = SettingsDir + "/SpatialLayoutConfig.asset";
        private const string PanelSettingsPath = SettingsDir + "/HcpPanelSettings.asset";
        private const string CatalogPath = SettingsDir + "/SceneCatalog.asset";

        private const string ScenesDir = "Assets/Scenes";
        private const string MenuScenePath = ScenesDir + "/HcpMenu.unity";
        private const string ConfiguratorScenePath = ScenesDir + "/HcpConfigurator.unity";
        private const string DemoScenePath = ScenesDir + "/HcpMain.unity";

        private const string OverlayUxmlPath = "Assets/Scripts/Context/Diagnostics/Infrastructure/UI/overlay.uxml";
        private const string SpatialUxmlPath = "Assets/Scripts/Context/Presentation/Infrastructure/UI/spatial-editor.uxml";

        [MenuItem("HCP/Setup All (Menu + Configurator + Demo)")]
        public static void SetupAll()
        {
            GetOrCreateConfig();
            GetOrCreateSpatialConfig();
            GetOrCreatePanelSettings();
            GetOrCreateCatalog();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null) CreateDemoScene();
            CreateConfiguratorScene();
            CreateMenuScene();

            RefreshScenes();
            EditorSceneManager.OpenScene(MenuScenePath);
            Debug.Log("[HcpSceneSetup] Setup complete. Open HcpMenu and press Play.");
        }

        // Adds a run-mode HcpBootstrap to the currently open scene (e.g. a legacy scene) so it
        // runs head-coupled off-axis with that scene's saved layout. Saves the scene.
        [MenuItem("HCP/Add HCP To Current Scene")]
        public static void AddHcpToCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[HcpSceneSetup] No valid active scene.");
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindFirstObjectByType<HcpBootstrap>();
#else
            var existing = Object.FindObjectOfType<HcpBootstrap>();
#endif
            if (existing != null)
            {
                Debug.Log($"[HcpSceneSetup] '{scene.name}' already has an HcpBootstrap.");
                return;
            }

            if (Camera.main == null) AddCamera(new Vector3(0f, 0.2f, 0.6f));

            var go = new GameObject("HcpBootstrap");
            var b = go.AddComponent<HcpBootstrap>();
            b.config = GetOrCreateConfig();
            b.spatialConfig = GetOrCreateSpatialConfig();
            b.enableOffAxis = true;
            b.uiPanelSettings = GetOrCreatePanelSettings();
            b.overlayUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OverlayUxmlPath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RefreshScenes();
            Debug.Log($"[HcpSceneSetup] Added HCP to '{scene.name}'. Configure it in HcpConfigurator, then launch from HcpMenu.");
        }

        [MenuItem("HCP/Refresh Scenes (catalog + build settings)")]
        public static void RefreshScenes()
        {
            var catalog = GetOrCreateCatalog();
            var paths = AssetDatabase.FindAssets("t:SceneAsset", new[] { ScenesDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".unity"))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // Catalog = content scenes (exclude Menu + Configurator).
            catalog.scenes = paths
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != "HcpMenu" && n != "HcpConfigurator")
                .ToList();
            EditorUtility.SetDirty(catalog);

            // Build Settings = all scenes (so any can be loaded by name at runtime).
            EditorBuildSettings.scenes = paths
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"[HcpSceneSetup] Catalog: {catalog.scenes.Count} scenes. Build Settings: {paths.Count}.");
        }

        // --- Scene builders ---------------------------------------------------------

        private static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLight();
            AddCamera(new Vector3(0f, 0.2f, 0.6f));
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DemoCube";
            cube.transform.position = new Vector3(0f, 0f, -1f);
            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, DemoScenePath);
        }

        private static void CreateConfiguratorScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLight();
            AddCamera(new Vector3(0f, 0.5f, -1.8f));

            var go = new GameObject("Configurator");
            var b = go.AddComponent<HcpConfiguratorBootstrap>();
            b.spatialConfig = GetOrCreateSpatialConfig();
            b.sceneCatalog = GetOrCreateCatalog();
            b.uiPanelSettings = GetOrCreatePanelSettings();
            b.spatialEditorUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SpatialUxmlPath);

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, ConfiguratorScenePath);
        }

        private static void CreateMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(Vector3.zero);

            var go = new GameObject("Menu");
            var b = go.AddComponent<HcpMenuBootstrap>();
            b.config = GetOrCreateConfig();
            b.spatialConfig = GetOrCreateSpatialConfig();
            b.catalog = GetOrCreateCatalog();
            b.uiPanelSettings = GetOrCreatePanelSettings();
            b.overlayUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OverlayUxmlPath);

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void AddLight()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static Camera AddCamera(Vector3 pos)
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.01f;
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = pos;
            return cam;
        }

        // --- Asset helpers ----------------------------------------------------------

        [MenuItem("HCP/Create HeadTracking Config")]
        public static HeadTrackingConfig GetOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HeadTrackingConfig>(ConfigPath);
            if (existing != null) return existing;
            Directory.CreateDirectory(SettingsDir);
            var config = ScriptableObject.CreateInstance<HeadTrackingConfig>();
            config.source = SourceKind.OpenSeeFaceBox;
            config.autoLaunchTracker = true;
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        public static SpatialLayoutConfig GetOrCreateSpatialConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SpatialLayoutConfig>(SpatialConfigPath);
            if (existing != null) return existing;
            Directory.CreateDirectory(SettingsDir);
            var cfg = ScriptableObject.CreateInstance<SpatialLayoutConfig>();
            AssetDatabase.CreateAsset(cfg, SpatialConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        public static SceneCatalog GetOrCreateCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SceneCatalog>(CatalogPath);
            if (existing != null) return existing;
            Directory.CreateDirectory(SettingsDir);
            var cat = ScriptableObject.CreateInstance<SceneCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
            AssetDatabase.SaveAssets();
            return cat;
        }

        // Shared PanelSettings for all UI Toolkit screens. Needs a ThemeStyleSheet to render;
        // reuse one from the project if present.
        public static PanelSettings GetOrCreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            var found = AssetDatabase.FindAssets("t:PanelSettings");
            if (found.Length > 0)
                return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(found[0]));

            Directory.CreateDirectory(SettingsDir);
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            var themeGuids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (themeGuids.Length > 0)
                ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themeGuids[0]));
            else
                Debug.LogWarning("[HcpSceneSetup] No ThemeStyleSheet found. Assign a theme to " + PanelSettingsPath +
                                 " (Create > UI Toolkit > Panel Settings Asset auto-creates one) or the UI won't render.");
            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return ps;
        }
    }
}
