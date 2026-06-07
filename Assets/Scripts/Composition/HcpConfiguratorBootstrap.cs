using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Hcp.Presentation.Domain;
using Hcp.Presentation.Application;
using Hcp.Presentation.Infrastructure;

namespace Hcp.Composition
{
    // Configurator scene (HCP OFF). Orbital camera, scene selector, gizmo + numeric UI to
    // author each scene's spatial layout (monitor / cameras / user). The selected scene is
    // loaded additively as a visual reference (its own camera/HCP disabled). Edits persist
    // per scene via the JSON store; the launcher applies them at runtime.
    public class HcpConfiguratorBootstrap : MonoBehaviour
    {
        public SpatialLayoutConfig spatialConfig;
        public SceneCatalog sceneCatalog;
        public PanelSettings uiPanelSettings;
        public VisualTreeAsset spatialEditorUxml;

        // Editor visuals (proxy + gizmo) live on this layer so the preview camera excludes them.
        private const int EditorLayer = 30;

        private SpatialLayoutService _service;
        private Transform _screenCenter;
        private string _currentScene;
        private Scene _refScene;
        private bool _refLoaded;

        private void Awake()
        {
            // Reference scenes loaded for context must not start HCP / bind the OpenSee port.
            HcpBootstrap.AutoRunSuppressed = true;
        }

        private void OnDestroy()
        {
            HcpBootstrap.AutoRunSuppressed = false;
        }

        private void Start()
        {
            // Orbital editor camera (no head tracking, no off-axis).
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                if (camGO.GetComponent<AudioListener>() == null) camGO.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.01f;
            var orbit = cam.gameObject.GetComponent<OrbitCameraView>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCameraView>();
            orbit.Initialize(Vector3.zero, 1.8f);

            _screenCenter = new GameObject("ScreenCenter").transform;

            // Scene to edit: from the menu's "Editar" button, else the first in the catalog.
            string first = (sceneCatalog != null && sceneCatalog.scenes.Count > 0) ? sceneCatalog.scenes[0] : null;
            if (!string.IsNullOrEmpty(HcpSession.SceneToEdit) &&
                sceneCatalog != null && sceneCatalog.scenes.Contains(HcpSession.SceneToEdit))
            {
                first = HcpSession.SceneToEdit;
            }
            HcpSession.SceneToEdit = null;
            var seed = spatialConfig != null ? spatialConfig.ToSpatialLayout() : SpatialLayout.Defaults();
            ISpatialLayoutStore store = new JsonFileSpatialLayoutStore(first);
            _service = new SpatialLayoutService(seed, store);
            _currentScene = first;

            // Preview camera: off-axis view from the user reference (what Play mode shows),
            // rendered to a RenderTexture and displayed bottom-left. Excludes editor visuals.
            var previewEye = new GameObject("PreviewEye").transform;
            var rt = new RenderTexture(384, 216, 16) { name = "HcpPreviewRT" };
            rt.Create();
            var pcamGO = new GameObject("PreviewCamera");
            var pcam = pcamGO.AddComponent<Camera>();
            pcam.clearFlags = CameraClearFlags.Skybox;
            pcam.nearClipPlane = 0.01f;
            pcam.cullingMask = ~(1 << EditorLayer);
            pcam.targetTexture = rt;
            var previewOff = pcamGO.AddComponent<OffAxisCameraView>();
            previewOff.Initialize(_screenCenter, previewEye, seed.monitorWidth, seed.monitorHeight, 0.01f, 100f);
            previewOff.enableOffAxis = true;

            // Proxy (cube + markers) drives the monitor + preview eye/size; gizmo edits it.
            var proxyGO = new GameObject("SpatialProxy");
            var proxy = proxyGO.AddComponent<SpatialProxyView>();
            proxy.visualLayer = EditorLayer;
            proxy.Initialize(_service, _screenCenter, previewOff, previewEye);

            var gizmoGO = new GameObject("SpatialGizmo");
            var gizmo = gizmoGO.AddComponent<RuntimeGizmoView>();
            gizmo.handleLayer = EditorLayer;
            gizmo.Initialize(cam, _service, proxy);

            BuildPreview(rt);

            if (uiPanelSettings != null && spatialEditorUxml != null)
            {
                var editorGO = new GameObject("SpatialEditor");
                var uidoc = editorGO.AddComponent<UIDocument>();
                uidoc.panelSettings = uiPanelSettings;
                uidoc.visualTreeAsset = spatialEditorUxml;
                var editor = editorGO.AddComponent<SpatialEditorView>();
                editor.Initialize(_service, gizmo);
            }

            BuildSelector();

            if (uiPanelSettings != null)
            {
                var escGO = new GameObject("EscMenu");
                escGO.AddComponent<EscapeMenuView>().Initialize(uiPanelSettings, HcpSession.MenuScene);
            }

            if (!string.IsNullOrEmpty(first)) LoadReference(first);
        }

        // --- Preview window (bottom-left) -------------------------------------------

        private void BuildPreview(RenderTexture rt)
        {
            if (uiPanelSettings == null) return;
            var go = new GameObject("PreviewUI");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = uiPanelSettings;
            var root = doc.rootVisualElement;

            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.left = 8; box.style.bottom = 8;
            box.style.paddingLeft = 6; box.style.paddingRight = 6;
            box.style.paddingTop = 4; box.style.paddingBottom = 4;
            box.style.backgroundColor = new Color(0, 0, 0, 0.72f);

            var label = new Label("Preview (cámara)");
            label.style.color = new Color(0.7f, 1f, 0.55f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 4;
            box.Add(label);

            var img = new VisualElement();
            img.style.width = 320;
            img.style.height = 180;
            img.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            box.Add(img);

            root.Add(box);
        }

        // --- Scene selector (code-built UIDocument, top-left) -----------------------

        private void BuildSelector()
        {
            if (uiPanelSettings == null || sceneCatalog == null) return;
            var go = new GameObject("SceneSelector");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = uiPanelSettings;
            var root = doc.rootVisualElement;

            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.left = 8; box.style.top = 8;
            box.style.paddingLeft = 8; box.style.paddingRight = 8;
            box.style.paddingTop = 6; box.style.paddingBottom = 6;
            box.style.backgroundColor = new Color(0, 0, 0, 0.72f);

            var label = new Label("Escena a configurar");
            label.style.color = new Color(0.7f, 1f, 0.55f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(label);

            var dd = new DropdownField { choices = new System.Collections.Generic.List<string>(sceneCatalog.scenes) };
            dd.index = string.IsNullOrEmpty(_currentScene) ? 0 : Mathf.Max(0, sceneCatalog.scenes.IndexOf(_currentScene));
            dd.RegisterValueChangedCallback(e => SelectScene(e.newValue));
            box.Add(dd);

            root.Add(box);
        }

        // --- Scene switching --------------------------------------------------------

        private void SelectScene(string scene)
        {
            if (string.IsNullOrEmpty(scene) || scene == _currentScene) return;

            _service.Save(); // persist current scene's edits first

            UnloadReference();

            var seed = spatialConfig != null ? spatialConfig.ToSpatialLayout() : SpatialLayout.Defaults();
            _service.Rebind(new JsonFileSpatialLayoutStore(scene), seed);
            _currentScene = scene;

            LoadReference(scene);
        }

        private void LoadReference(string scene)
        {
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (op == null) return; // scene not in build settings
            op.completed += _ =>
            {
                _refScene = SceneManager.GetSceneByName(scene);
                _refLoaded = _refScene.IsValid();
                NeutralizeReference(_refScene);
            };
        }

        private void UnloadReference()
        {
            if (_refLoaded && _refScene.IsValid())
                SceneManager.UnloadSceneAsync(_refScene);
            _refLoaded = false;
        }

        // Disable anything in the reference scene that would fight the configurator:
        // extra cameras, audio listeners, and any HCP bootstrap/runner.
        private void NeutralizeReference(Scene scene)
        {
            if (!scene.IsValid()) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                foreach (var a in root.GetComponentsInChildren<AudioListener>(true)) a.enabled = false;
                foreach (var b in root.GetComponentsInChildren<HcpBootstrap>(true)) b.gameObject.SetActive(false);
                foreach (var r in root.GetComponentsInChildren<HcpRunner>(true)) r.gameObject.SetActive(false);
            }
        }
    }
}
