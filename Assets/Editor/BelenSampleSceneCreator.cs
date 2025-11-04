using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Belen.Rendering;
using Belen.Tracking;
using Belen.Tracking.Sources;
using Belen.Interaction;
using Belen.UI;
using Belen.Audio;
using Belen.Scenes;

public static class BelenSampleSceneCreator
{
    [MenuItem("Belen/Create Sample Scene")]
    public static void CreateSampleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Head pivot and camera
        var headPivot = new GameObject("HeadPivot").transform;
        headPivot.position = new Vector3(0f, 0.2f, 0.6f);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(headPivot, false);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.01f;

        // Basic lighting to ensure primitives are visible
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = Color.white;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Screen center
        var screenCenter = new GameObject("ScreenCenter").transform;
        screenCenter.position = Vector3.zero;
        screenCenter.rotation = Quaternion.identity; // forward +Z towards viewer

        // Off-axis
        var offaxis = camGO.AddComponent<OffAxisCamera>();
        offaxis.screenCenter = screenCenter;
        // Attach a screen preset helper (defaults to 27" 16:9)
        var preset = camGO.AddComponent<OffAxisScreenPreset>();
        preset.target = offaxis;
        preset.preset = OffAxisScreenPreset.Preset.Inch27_16x9;
        preset.autoApply = true;
        offaxis.eyeTransform = headPivot;
        // Default to normal perspective (disable off-axis override); orbit mode will handle camera motion
        offaxis.overrideCameraMatrices = false;
        offaxis.enabled = false;

        // Tracking source (keyboard) + manager
        var srcGO = new GameObject("KeyboardHeadPoseEmulator");
        var src = srcGO.AddComponent<KeyboardHeadPoseEmulator>();
        src.position = headPivot.position;

        // OpenSeeFace receiver + adapter
        var osfGO = new GameObject("OpenSeeReceiver");
        var osf = osfGO.AddComponent<OpenSee.OpenSee>();
        osf.listenAddress = "127.0.0.1";
        osf.listenPort = 11573;
        var osfSrc = osfGO.AddComponent<Belen.Tracking.Sources.OpenSeeHeadPoseSource>();
        osfSrc.openSee = osf;
        // Default head pose tuning for stronger depth perception
        osfSrc.positionScale = 1.0f;
        osfSrc.axisScale = new Vector3(1f, 1f, 1.4f);
        osfSrc.invertZ = true; // typical for viewer-in-front
        // Enable Z estimation from face size to improve in/out motion
        osfSrc.estimateZFromFaceSize = true;
        osfSrc.neutralDepthMeters = 0.6f; // ~60 cm
        osfSrc.zBlend = 0.8f;
        osfSrc.zClamp = new Vector2(0.2f, 2.5f);
        osfSrc.zSmoothTime = 0.12f;

        var managerGO = new GameObject("FaceTrackerManager");
        var manager = managerGO.AddComponent<FaceTrackerManager>();
        // Default to OpenSee; keep keyboard emulator present but disabled
        manager.sourceBehaviour = osfSrc;
        manager.cameraPivot = headPivot;
        srcGO.SetActive(false);

        // Configure camera motion: Orbit mode by default
        manager.motionMode = FaceTrackerManager.MotionMode.OrbitTarget;
        manager.orbitTarget = screenCenter;
        manager.orbitBaseDistance = 1.2f;
        manager.yawDegreesPerMeter = 350f;
        manager.pitchDegreesPerMeter = 350f;
        manager.dollyMetersPerMeter = 1.0f;
        manager.pitchClamp = new Vector2(-30f, 30f);
        manager.distanceClamp = new Vector2(0.3f, 3.0f);
        // Set an initial framing for orbit mode (slight top-down, comfortable distance)
        manager.applyOrbitStartOnEnable = true;
        manager.orbitStartYaw = 0f;
        manager.orbitStartPitch = -10f;
        manager.orbitStartDistance = 1.4f;

        // Presence + UI
        var presenceGO = new GameObject("PresenceDetector");
        var presence = presenceGO.AddComponent<PresenceDetector>();
        presence.sourceBehaviour = src;

        var canvasGO = new GameObject("PromptCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        var cg = canvasGO.AddComponent<CanvasGroup>();
        var prompt = canvasGO.AddComponent<UIPromptController>();
        prompt.presence = presence;

        // Text (legacy UI)
        var textGO = new GameObject("PromptText");
        textGO.transform.SetParent(canvasGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 36;
        txt.color = new Color(1f, 1f, 1f, 0.95f);
        Font builtin = null;
        try { builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (builtin == null)
        {
            try { builtin = Font.CreateDynamicFontFromOSFont("Arial", 36); } catch { }
        }
        if (builtin != null) txt.font = builtin;
        txt.text = "Muévete frente a la pantalla\npara descubrir el belén";
        var rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Also add a TextMeshPro prompt for better text rendering (and remove legacy Text)
        try
        {
            var tmpGO = new GameObject("PromptTextTMP");
            tmpGO.transform.SetParent(canvasGO.transform, false);
            var tmp = tmpGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 42;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);
            tmp.text = "Muévete frente a la pantalla\npara descubrir el belén";
            var rtTmp = tmp.rectTransform;
            rtTmp.anchorMin = Vector2.zero;
            rtTmp.anchorMax = Vector2.one;
            rtTmp.offsetMin = Vector2.zero;
            rtTmp.offsetMax = Vector2.zero;
            var legacy = GameObject.Find("PromptText");
            if (legacy != null) Object.DestroyImmediate(legacy);
        }
        catch { /* TextMeshPro may be missing; legacy Text remains */ }

        // Audio crossfader
        var audioGO = new GameObject("Audio");
        var crossfader = audioGO.AddComponent<AudioCrossfader>();

        // Scene flow roots
        string[] names = { "Anunciacion", "CaminoABelen", "Nacimiento", "AnuncioPastores", "AdoracionReyes" };
        var flowGO = new GameObject("SceneFlow");
        var flow = flowGO.AddComponent<BelenSceneController>();
        flow.scenes = new BelenSceneController.SceneDef[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var root = new GameObject(names[i]);
            var def = new BelenSceneController.SceneDef
            {
                name = names[i],
                duration = 22f,
                music = null,
                roots = new[] { root }
            };
            flow.scenes[i] = def;
        }
        flow.crossfader = crossfader;

        // Frame a simple reference cube grid to visualize depth (optional)
        for (int z = 1; z <= 3; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"RefCube_{x}_{z}";
                cube.transform.position = new Vector3(x * 0.3f, 0.0f, 0.2f * z);
                cube.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                cube.transform.SetParent(GameObject.Find(names[Mathf.Min(z - 1, names.Length - 1)]).transform);
            }
        }

        // Debug + Calibration
        var debugGO = new GameObject("DebugUI");
        var overlay = debugGO.AddComponent<Belen.DebugUI.DebugOverlay>();
        overlay.tracker = manager;
        overlay.offAxis = offaxis;
        overlay.headPivot = headPivot;
        var calib = debugGO.AddComponent<Belen.DebugUI.CalibrationPanel>();
        calib.tracker = manager;
        calib.offAxis = offaxis;
        calib.sourceBehaviour = manager.sourceBehaviour;
        var autosave = debugGO.AddComponent<Belen.DebugUI.CalibrationAutoSaver>();
        autosave.tracker = manager;
        autosave.saveDelay = 1.0f;

        // Always-on reference: a small plane and colored cubes
        var refRoot = new GameObject("Reference");
        // Ground plane (~2m x 2m)
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "RefPlane";
        plane.transform.SetParent(refRoot.transform, false);
        plane.transform.position = new Vector3(0f, -0.05f, 0.6f);
        plane.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

        // Colored cubes
        var colors = new[] { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };
        var positions = new[]
        {
            new Vector3(-0.45f, 0.04f, 0.5f),
            new Vector3(-0.225f, 0.04f, 0.5f),
            new Vector3(0.0f, 0.04f, 0.5f),
            new Vector3(0.225f, 0.04f, 0.5f),
            new Vector3(0.45f, 0.04f, 0.5f),
            new Vector3(0.0f, 0.14f, 0.35f),
        };
        for (int i = 0; i < positions.Length; i++)
        {
            var cgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cgo.name = $"ColorCube_{i}";
            cgo.transform.SetParent(refRoot.transform, false);
            cgo.transform.position = positions[i];
            cgo.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            var mr = cgo.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            var col = colors[i % colors.Length];
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            mr.sharedMaterial = mat;
        }
        // Save scene
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        string path = "Assets/Scenes/BelenSample.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Belén Sample", "Sample scene created at\n" + path, "OK");
    }
}
