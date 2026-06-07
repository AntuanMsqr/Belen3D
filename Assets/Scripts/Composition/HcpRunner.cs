using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Hcp.HeadTracking.Infrastructure;
using Hcp.Presentation.Infrastructure;

namespace Hcp.Composition
{
    // Persists across scene loads (spawned by the launch menu). When a content scene loads,
    // it checks whether that scene already brings an HcpBootstrap; if not, it creates one so
    // the scene runs with the head-coupled off-axis camera + that scene's saved layout.
    public class HcpRunner : MonoBehaviour
    {
        public HeadTrackingConfig config;
        public SpatialLayoutConfig spatialConfig;
        public PanelSettings uiPanelSettings;
        public VisualTreeAsset overlayUxml;

        // Create (or reuse) the persistent runner with the given assets.
        public static HcpRunner Ensure(HeadTrackingConfig config, SpatialLayoutConfig spatialConfig,
                                       PanelSettings panel, VisualTreeAsset overlayUxml)
        {
            var existing = FindFirstByType();
            if (existing == null)
            {
                var go = new GameObject("HcpRunner");
                DontDestroyOnLoad(go);
                existing = go.AddComponent<HcpRunner>();
            }
            existing.config = config;
            existing.spatialConfig = spatialConfig;
            existing.uiPanelSettings = panel;
            existing.overlayUxml = overlayUxml;
            return existing;
        }

        private static HcpRunner FindFirstByType()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<HcpRunner>();
#else
            return Object.FindObjectOfType<HcpRunner>();
#endif
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;

        private void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            // Never start HCP in the menu or configurator scenes.
            if (scene.name == HcpSession.MenuScene || scene.name == HcpSession.ConfiguratorScene) return;
            EnsureHcpInScene();
        }

        private void EnsureHcpInScene()
        {
            // Scene already configured to run HCP? Leave it alone.
#if UNITY_2023_1_OR_NEWER
            var present = Object.FindFirstObjectByType<HcpBootstrap>();
#else
            var present = Object.FindObjectOfType<HcpBootstrap>();
#endif
            if (present != null) return;

            var go = new GameObject("HcpBootstrap");
            var bootstrap = go.AddComponent<HcpBootstrap>();
            bootstrap.config = config;
            bootstrap.spatialConfig = spatialConfig;
            bootstrap.enableOffAxis = true;
            bootstrap.uiPanelSettings = uiPanelSettings;
            bootstrap.overlayUxml = overlayUxml;
        }
    }
}
