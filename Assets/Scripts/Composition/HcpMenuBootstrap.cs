using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Hcp.HeadTracking.Infrastructure;
using Hcp.Presentation.Infrastructure;

namespace Hcp.Composition
{
    // Launch menu. Lists every scene in the catalog; picking one spawns the persistent
    // HcpRunner (camera + off-axis + head tracking + that scene's saved layout) and loads
    // the scene. Built programmatically so the button list follows the catalog.
    public class HcpMenuBootstrap : MonoBehaviour
    {
        public HeadTrackingConfig config;
        public SpatialLayoutConfig spatialConfig;
        public SceneCatalog catalog;
        public PanelSettings uiPanelSettings;
        public VisualTreeAsset overlayUxml;

        private void Start()
        {
            if (Camera.main == null)
            {
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                camGO.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camGO.AddComponent<AudioListener>();
            }

            if (uiPanelSettings == null) { Debug.LogWarning("[HcpMenu] No PanelSettings assigned."); return; }

            var go = new GameObject("MenuUI");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = uiPanelSettings;
            var root = doc.rootVisualElement;

            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.left = 0; box.style.top = 0; box.style.right = 0; box.style.bottom = 0;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            root.Add(box);

            var title = new Label("HCP — Selecciona escena");
            title.style.color = Color.white;
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            box.Add(title);

            if (catalog == null || catalog.scenes.Count == 0)
            {
                var empty = new Label("Catálogo vacío. Ejecuta HCP > Refresh Scenes.");
                empty.style.color = new Color(1f, 0.7f, 0.5f);
                box.Add(empty);
                return;
            }

            foreach (var scene in catalog.scenes)
            {
                string s = scene; // capture

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;

                var name = new Label(s) { style = { color = Color.white, width = 180, fontSize = 14 } };
                row.Add(name);

                var start = new Button(() => Launch(s)) { text = "Iniciar", style = { width = 90, height = 30 } };
                row.Add(start);

                var edit = new Button(() => Edit(s)) { text = "Editar", style = { width = 80, height = 30 } };
                row.Add(edit);

                box.Add(row);
            }

            var editorBtn = new Button(() => SceneManager.LoadScene(HcpSession.ConfiguratorScene, LoadSceneMode.Single))
            { text = "Abrir Configurador", style = { width = 200, height = 30, marginTop = 12 } };
            box.Add(editorBtn);
        }

        private void Launch(string scene)
        {
            HcpRunner.Ensure(config, spatialConfig, uiPanelSettings, overlayUxml);
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        private void Edit(string scene)
        {
            HcpSession.SceneToEdit = scene;
            SceneManager.LoadScene(HcpSession.ConfiguratorScene, LoadSceneMode.Single);
        }
    }
}
