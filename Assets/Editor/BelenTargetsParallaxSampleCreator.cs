using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Belen.Rendering;
using Belen.Tracking;
using Belen.Tracking.Sources;

public static class BelenTargetsParallaxSampleCreator
{
    [MenuItem("Belen/Create Targets Parallax Sample Scene")]
    public static void CreateTargetsParallaxSample()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Head pivot and camera
        var headPivot = new GameObject("HeadPivot").transform;
        headPivot.position = new Vector3(0f, 0.2f, 0.6f);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(headPivot, false);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.06f);
        cam.nearClipPlane = 0.01f;

        // Screen center (plane forward points out toward viewer)
        var screenCenter = new GameObject("ScreenCenter").transform;
        screenCenter.position = Vector3.zero;
        screenCenter.rotation = Quaternion.identity;

        // Off-axis (kept but disabled by default)
        var offaxis = camGO.AddComponent<OffAxisCamera>();
        offaxis.screenCenter = screenCenter;
        offaxis.screenWidth = 0.6f;   // typical 27"
        offaxis.screenHeight = 0.34f;
        offaxis.eyeTransform = headPivot;
        offaxis.overrideCameraMatrices = false;
        offaxis.enabled = false;

        // Simple parallax alternative (active by default)
        var simple = camGO.AddComponent<SimpleParallaxCamera>();
        simple.screenCenter = screenCenter;
        simple.eyeTransform = headPivot;
        simple.gainXY = new Vector2(1f, 1f);
        simple.invertX = false; // set true if izquierda/derecha no coincide
        simple.invertY = false;
        simple.useZ = true;
        simple.zGain = 1.0f;
        simple.clampX = new Vector2(-0.5f, 0.5f);
        simple.clampY = new Vector2(-0.3f, 0.3f);
        simple.clampZ = new Vector2(0.2f, 3.0f);
        simple.focusDepth = 2.0f;
        simple.smooth = true;
        simple.posSmoothTimeXY = 0.12f;
        simple.posSmoothTimeZ = 0.18f;
        simple.rotSmoothTime = 0.12f;
        simple.deadzoneX = 0.003f;
        simple.deadzoneY = 0.003f;

        // OpenSeeFace receiver + FaceBox (no rotation) source
        var osfGO = new GameObject("OpenSeeReceiver");
        var osf = osfGO.AddComponent<OpenSee.OpenSee>();
        osf.listenAddress = "127.0.0.1";
        osf.listenPort = 11573;
        var osfSrc = osfGO.AddComponent<OpenSeeFaceBoxSource>();
        osfSrc.openSee = osf;
        osfSrc.widthMeters = 0.6f;
        osfSrc.heightMeters = 0.34f;
        try { osfSrc.widthMeters = Mathf.Max(0.1f, offaxis.screenWidth); osfSrc.heightMeters = Mathf.Max(0.1f, offaxis.screenHeight); } catch { }
        osfSrc.neutralDepthMeters = 0.6f;
        osfSrc.zBlend = 0.85f;
        osfSrc.zClamp = new Vector2(0.2f, 2.5f);
        osfSrc.zSmoothTime = 0.16f;
        osfSrc.autoNeutral = true;
        osfSrc.autoNeutralDuration = 1.0f;
        osfSrc.faceSizeClamp01 = new Vector2(0.06f, 0.6f);
        osfSrc.use3DZFallback = true;
        osfSrc.invertX = false;
        osfSrc.invertY = false;

        // Face tracker (Direct)
        var managerGO = new GameObject("FaceTrackerManager");
        var manager = managerGO.AddComponent<FaceTrackerManager>();
        manager.sourceBehaviour = osfSrc;
        manager.cameraPivot = headPivot;
        manager.motionMode = FaceTrackerManager.MotionMode.Direct;
        manager.applyHeadRotation = false; // window effect: ignore head rotation

        // Grid-tunnel room parented to ScreenCenter (extends into -forward)
        var roomRoot = new GameObject("TargetsRoom").transform;
        roomRoot.SetParent(screenCenter, false);
        roomRoot.localPosition = Vector3.zero;
        roomRoot.localRotation = Quaternion.identity;

        float w = 1.2f;
        float h = 0.68f;
        try { w = Mathf.Max(0.1f, offaxis.screenWidth * 2.0f); h = Mathf.Max(0.1f, offaxis.screenHeight * 2.0f); } catch { }
        float thickness = 0.01f;
        var lineMat = MakeUnlit(new Color(0.55f, 0.55f, 0.65f));

        // Only keep the outer frames: front and back boundaries
        int depthSteps = 24; float step = 0.25f;
        CreateFrame(roomRoot, new Vector3(0, 0, 0f), w, h, thickness, lineMat);
        CreateFrame(roomRoot, new Vector3(0, 0, -depthSteps * step), w, h, thickness, lineMat);

        // Targets (bullseyes) at different depths and offsets
        var white = MakeUnlit(new Color(1f, 1f, 1f));
        var red = MakeUnlit(new Color(1f, 0.25f, 0.25f));
        Vector3[] targetOffsets = {
            new Vector3(-0.25f, -0.10f, -0.35f),
            new Vector3( 0.30f,  0.15f, -0.90f),
            new Vector3(-0.35f,  0.22f, -1.60f),
            new Vector3( 0.12f, -0.18f, -2.40f),
            new Vector3( 0.00f,  0.00f, -3.20f),
        };
        float baseSize = 0.18f;
        for (int i = 0; i < targetOffsets.Length; i++)
        {
            float s = baseSize * (1.4f - 0.2f * i);
            CreateBullseye(roomRoot, targetOffsets[i], s, white, red);
        }

        // Debug UI autosave
        var debugGO = new GameObject("DebugUI");
        var calib = debugGO.AddComponent<Belen.DebugUI.CalibrationPanel>();
        calib.tracker = manager;
        calib.offAxis = offaxis;
        calib.sourceBehaviour = manager.sourceBehaviour;
        var autosave = debugGO.AddComponent<Belen.DebugUI.CalibrationAutoSaver>();
        autosave.tracker = manager;
        autosave.saveDelay = 1.0f;

        // Save scene
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        string path = "Assets/Scenes/BelenTargetsParallaxSample.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Belén Targets Parallax Sample", "Scene created at\n" + path, "OK");
    }

    static void CreateFrame(Transform parent, Vector3 localCenter, float width, float height, float thickness, Material mat)
    {
        float hw = width * 0.5f;
        float hh = height * 0.5f;
        void Bar(Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter + pos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }
        // Outline frame: horizontal (top/bottom) and vertical (left/right)
        Bar(new Vector3(0,  hh, 0), new Vector3(width,  thickness, thickness));   // top
        Bar(new Vector3(0, -hh, 0), new Vector3(width,  thickness, thickness));   // bottom
        Bar(new Vector3(-hw, 0, 0), new Vector3(thickness, height,    thickness)); // left
        Bar(new Vector3( hw, 0, 0), new Vector3(thickness, height,    thickness)); // right
    }

    // No spokes for this variant; only front/back frames

    static void CreateBullseye(Transform parent, Vector3 localPos, float radius, Material white, Material red)
    {
        var root = new GameObject($"Target@{localPos.z:F2}");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.identity;

        // Concentric cylinders laid flat (faces toward -Z, into the screen)
        float[] radii = { radius, radius * 0.72f, radius * 0.48f, radius * 0.20f };
        Material[] mats = { white, red, white, red };
        for (int i = 0; i < radii.Length; i++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.transform.SetParent(root.transform, false);
            c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            c.transform.localPosition = new Vector3(0, 0, i * 0.001f); // tiny offset to avoid z-fight
            c.transform.localScale = new Vector3(radii[i], 0.01f, radii[i]);
            var mr = c.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mats[i];
        }
    }

    static Material MakeUnlit(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (sh.name.Contains("Standard"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.8f);
        }
        return m;
    }
}
