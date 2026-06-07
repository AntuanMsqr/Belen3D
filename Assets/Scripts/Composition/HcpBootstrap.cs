using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Hcp.HeadTracking.Domain;
using Hcp.HeadTracking.Application;
using Hcp.HeadTracking.Infrastructure;
using Hcp.Presentation.Domain;
using Hcp.Presentation.Application;
using Hcp.Presentation.Infrastructure;
using Hcp.Diagnostics.Infrastructure;

namespace Hcp.Composition
{
    // Composition root for the HeadTracking context.
    // The ONLY MonoBehaviour that lives in the scene: it builds the rig in code,
    // wires the plain Application services to Infrastructure adapters/Views, auto-launches
    // the external face tracker, and tears it all down on quit.
    public class HcpBootstrap : MonoBehaviour
    {
        [Tooltip("Tuning + source + tracker settings. If null, defaults are used.")]
        public HeadTrackingConfig config;

        [Header("Presentation (off-axis / HCP)")]
        public bool enableOffAxis = false;
        public float screenWidthMeters = 0.6f;
        public float screenHeightMeters = 0.34f;

        [Header("Spatial layout (per-scene, applied at runtime)")]
        [Tooltip("Default monitor/camera/user layout if no saved config exists for the scene.")]
        public SpatialLayoutConfig spatialConfig;

        [Header("Diagnostics")]
        public bool enableAutosave = true;
        public bool enableOverlay = true;
        [Tooltip("Shared UI Toolkit PanelSettings (assigned by HCP > Setup Hex Scene).")]
        public PanelSettings uiPanelSettings;
        [Tooltip("Overlay UXML (assigned by HCP > Setup Hex Scene).")]
        public VisualTreeAsset overlayUxml;

        private HeadTrackingController _controller;
        private ITrackerProcess _tracker;
        private bool _stopped;

        // Set by the Configurator while a scene is loaded as visual reference, so that
        // scene's own bootstrap does NOT spin up head tracking / the OpenSee UDP socket
        // (which would clash on the listen port). Reset when leaving the Configurator.
        public static bool AutoRunSuppressed;

        private void Start()
        {
            if (AutoRunSuppressed)
            {
                // Reference instance inside the Configurator: stay inert.
                gameObject.SetActive(false);
                return;
            }

            var cfg = config != null ? config : ScriptableObject.CreateInstance<HeadTrackingConfig>();

            // --- Rig (built in code; scene only holds this Bootstrap + art) ---
            var headPivot = new GameObject("HeadPivot").transform;
            headPivot.position = new Vector3(0f, 0.2f, 0.6f);

            // Reuse a Main Camera already in the scene (so it renders in edit mode);
            // only create one as a fallback.
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                if (camGO.GetComponent<AudioListener>() == null) camGO.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.01f;
            cam.transform.SetParent(headPivot, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            var screenCenter = new GameObject("ScreenCenter").transform;
            screenCenter.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Off-axis (Head-Coupled Perspective): camera renders the screen plane as a window,
            // skewing the frustum by the head (eye) position.
            OffAxisCameraView offAxis = null;
            if (enableOffAxis)
            {
                offAxis = cam.gameObject.AddComponent<OffAxisCameraView>();
                offAxis.Initialize(screenCenter, headPivot, screenWidthMeters, screenHeightMeters,
                                   cam.nearClipPlane, cam.farClipPlane);
            }

            // --- OpenSee UDP receiver (only for OpenSee* sources) ---
            // Reuse an existing receiver to avoid a second bind on the same UDP port.
            OpenSee.OpenSee openSee = null;
            if (cfg.source == SourceKind.OpenSeePose || cfg.source == SourceKind.OpenSeeFaceBox)
            {
#if UNITY_2023_1_OR_NEWER
                openSee = Object.FindFirstObjectByType<OpenSee.OpenSee>();
#else
                openSee = Object.FindObjectOfType<OpenSee.OpenSee>();
#endif
                if (openSee == null)
                {
                    var osGO = new GameObject("OpenSeeReceiver");
                    openSee = osGO.AddComponent<OpenSee.OpenSee>();
                    openSee.listenAddress = cfg.openSeeListenAddress;
                    openSee.listenPort = cfg.openSeeListenPort;
                }
            }

            // --- Source adapter via Router ---
            var srcGO = new GameObject("HeadPoseSource");
            var source = HeadPoseSourceRouter.Create(cfg.source, srcGO, openSee,
                cfg.invertX, cfg.invertY, cfg.invertZ, cfg.positionScale);

            // --- Application services (plain C#) ---
            IClock clock = new UnityClock();
            var filter = new HeadPoseExponentialFilter();
            var neutral = new NeutralLearningService(clock);
            var processing = new HeadPoseProcessingService(filter, neutral);
            var mapping = new CameraMappingService();
            ICalibrationStore store = new PlayerPrefsCalibrationStore();
            _controller = new HeadTrackingController(source, processing, mapping, store, cfg.ToCalibrationData());

            // --- View that pumps the controller and applies the result to the Transform ---
            var rig = headPivot.gameObject.AddComponent<CameraRigView>();
            rig.Initialize(_controller, headPivot, screenCenter);

            _controller.Start();

            // --- Diagnostics ---
            if (enableAutosave)
            {
                var saver = gameObject.AddComponent<CalibrationAutoSaverView>();
                saver.Initialize(_controller);
            }
            if (enableOverlay && uiPanelSettings != null && overlayUxml != null)
            {
                var overlayGO = new GameObject("DebugOverlay");
                var uidoc = overlayGO.AddComponent<UIDocument>();
                uidoc.panelSettings = uiPanelSettings;
                uidoc.visualTreeAsset = overlayUxml;
                var overlay = overlayGO.AddComponent<DebugOverlayView>();
                overlay.Initialize(_controller, headPivot, screenCenter);
            }

            // --- Apply this scene's saved spatial layout (authored in the Configurator) ---
            {
                var seed = spatialConfig != null ? spatialConfig.ToSpatialLayout() : SpatialLayout.Defaults();
                ISpatialLayoutStore spatialStore = new JsonFileSpatialLayoutStore(SceneManager.GetActiveScene().name);
                var spatial = new SpatialLayoutService(seed, spatialStore);
                var layout = spatial.Current;

                screenCenter.SetPositionAndRotation(layout.monitorPosition, Quaternion.Euler(layout.monitorEuler));
                if (offAxis != null)
                {
                    offAxis.screenWidth = layout.monitorWidth;
                    offAxis.screenHeight = layout.monitorHeight;
                }
                // Neutral viewpoint: head pivot at the user reference (monitor-relative).
                headPivot.position = screenCenter.TransformPoint(layout.userReferencePosition);
            }

            // --- Esc overlay: back to main menu from any scene ---
            if (uiPanelSettings != null)
            {
                var escGO = new GameObject("EscMenu");
                escGO.AddComponent<EscapeMenuView>().Initialize(uiPanelSettings, HcpSession.MenuScene);
            }

            // --- Auto-launch the external tracker ---
            if (cfg.autoLaunchTracker)
            {
                string exe = ResolvePath(cfg.trackerExeRelativePath);
                _tracker = new FaceTrackerProcessAdapter(exe, cfg.trackerArguments, cfg.trackerShowWindow);
                _tracker.Start();
            }
        }

        private static string ResolvePath(string relativeOrAbsolute)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolute)) return relativeOrAbsolute;
            if (Path.IsPathRooted(relativeOrAbsolute)) return relativeOrAbsolute;
            // Project root = parent of Assets/ in the Editor; player dir in a build.
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(root, relativeOrAbsolute));
        }

        private void OnApplicationQuit() => StopAll();
        private void OnDestroy() => StopAll();

        private void StopAll()
        {
            if (_stopped) return;
            _stopped = true;
            try { _tracker?.Stop(); } catch { /* ignore */ }
            try { _controller?.Stop(); } catch { /* ignore */ }
        }
    }
}
