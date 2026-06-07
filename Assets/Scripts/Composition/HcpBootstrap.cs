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

        private HeadTrackingController controller;
        private ITrackerProcess tracker;
        private bool stopped;

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

            // --- Apply this scene's saved spatial layout (authored in the Configurator) ---
            // Loaded BEFORE the controller so the head-tracking neutral can be aligned to the
            // authored user reference -> Play matches the Configurator preview exactly.
            var seed = spatialConfig != null ? spatialConfig.ToSpatialLayout() : SpatialLayout.Defaults();
            ISpatialLayoutStore spatialStore = new JsonFileSpatialLayoutStore(SceneManager.GetActiveScene().name);
            var spatialLayout = new SpatialLayoutService(seed, spatialStore).Current;

            screenCenter.SetPositionAndRotation(spatialLayout.monitorPosition, Quaternion.Euler(spatialLayout.monitorEuler));
            if (offAxis != null)
            {
                offAxis.screenWidth = spatialLayout.monitorWidth;
                offAxis.screenHeight = spatialLayout.monitorHeight;
            }
            // Neutral viewpoint: head pivot at the user reference (monitor-relative).
            headPivot.position = screenCenter.TransformPoint(spatialLayout.userReferencePosition);

            // "Ventana" (HCP window) Scene-view gizmo: screen rect + neutral eye + frustum.
            // Mirrors the Configurator so the authored window is visible while exploring.
            if (enableOffAxis)
            {
                var frame = screenCenter.gameObject.AddComponent<OffAxisFrameGizmo>();
                frame.screenWidth = spatialLayout.monitorWidth;
                frame.screenHeight = spatialLayout.monitorHeight;
                frame.eyeLocalPosition = spatialLayout.userReferencePosition;
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
            controller = new HeadTrackingController(source, processing, mapping, store, cfg.ToCalibrationData());

            // --- View that pumps the controller and applies the result to the Transform ---
            var rig = headPivot.gameObject.AddComponent<CameraRigView>();
            rig.Initialize(controller, headPivot, screenCenter);

            controller.Start();

            // Re-assert the authored neutral AFTER Start (which overlays persisted PlayerPrefs
            // calibration). The per-scene user reference must win so the neutral camera position
            // equals the Configurator preview; otherwise a stale persisted positionOffset/orbit
            // neutral places the camera elsewhere.
            var cal = controller.Calibration;
            AlignNeutralToUserReference(ref cal, screenCenter, spatialLayout.userReferencePosition);
            controller.SetCalibration(cal);
            controller.ResetOrbit();

            // --- Diagnostics ---
            if (enableAutosave)
            {
                var saver = gameObject.AddComponent<CalibrationAutoSaverView>();
                saver.Initialize(controller);
            }
            if (enableOverlay && uiPanelSettings != null && overlayUxml != null)
            {
                var overlayGO = new GameObject("DebugOverlay");
                var uidoc = overlayGO.AddComponent<UIDocument>();
                uidoc.panelSettings = uiPanelSettings;
                uidoc.visualTreeAsset = overlayUxml;
                var overlay = overlayGO.AddComponent<DebugOverlayView>();
                overlay.Initialize(controller, headPivot, screenCenter);
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
                tracker = new FaceTrackerProcessAdapter(exe, cfg.trackerArguments, cfg.trackerShowWindow);
                tracker.Start();
            }
        }

        // Sets the head-tracking neutral so that, with no head motion, the camera eye sits at
        // the authored user reference (world = screenCenter.TransformPoint(userRef)). The off-axis
        // projection depends only on eye POSITION, so we only need to match that — rotation is
        // driven by the projection matrices, not the transform.
        //  - Direct: the eye world position at neutral is exactly positionOffset (headPivot is a
        //    scene root, so localPosition == world position).
        //  - Orbit:  the steady-state eye = orbitTarget(=screenCenter) + Euler(pitch,yaw)*(0,0,-dist),
        //    where neutral yaw/pitch come from compositionOffsetDeg and dist from orbitBaseDistance.
        private static void AlignNeutralToUserReference(ref CalibrationData cal, Transform screenCenter, Vector3 userRefLocal)
        {
            Vector3 worldEye = screenCenter.TransformPoint(userRefLocal);

            if (cal.motionMode == MotionMode.Direct)
            {
                cal.positionOffset = worldEye;
                return;
            }

            Vector3 fromTarget = worldEye - screenCenter.position;
            float dist = fromTarget.magnitude;
            if (dist < 1e-4f) return;

            var rot = Quaternion.LookRotation(-fromTarget.normalized, Vector3.up);
            float yaw = NormalizeAngle(rot.eulerAngles.y);
            float pitch = NormalizeAngle(rot.eulerAngles.x);

            cal.orbitBaseDistance = dist;
            cal.orbitStartDistance = dist;
            cal.orbitStartYaw = yaw;
            cal.orbitStartPitch = pitch;
            // Neutral (dx=dy=0) steady-state yaw/pitch come from the composition offset.
            cal.compositionOffsetDeg = new Vector2(yaw, pitch);
        }

        private static float NormalizeAngle(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
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
            if (stopped) return;
            stopped = true;
            try { tracker?.Stop(); } catch { /* ignore */ }
            try { controller?.Stop(); } catch { /* ignore */ }
        }
    }
}
