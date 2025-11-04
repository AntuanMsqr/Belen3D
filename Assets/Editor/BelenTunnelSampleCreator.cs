using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Belen.Rendering;
using Belen.Tracking;
using Belen.Tracking.Sources;

public static class BelenTunnelSampleCreator
{
    [MenuItem("Belen/Create Tunnel Sample Scene")]
    public static void CreateTunnelSample()
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

        // Off-axis HCP (Direct)
        var offaxis = camGO.AddComponent<OffAxisCamera>();
        offaxis.screenCenter = screenCenter;
        offaxis.screenWidth = 0.6f;   // 27" ~0.597
        offaxis.screenHeight = 0.34f; // ~0.336
        offaxis.eyeTransform = headPivot;
        offaxis.overrideCameraMatrices = true;
        offaxis.enabled = true;

        // OpenSeeFace receiver + adapter with depth helpers
        var osfGO = new GameObject("OpenSeeReceiver");
        var osf = osfGO.AddComponent<OpenSee.OpenSee>();
        osf.listenAddress = "127.0.0.1";
        osf.listenPort = 11573;
        var osfSrc = osfGO.AddComponent<OpenSeeHeadPoseSource>();
        osfSrc.openSee = osf;
        osfSrc.positionScale = 1.0f;
        osfSrc.axisScale = new Vector3(1f, 1f, 1.6f);
        osfSrc.invertZ = true;
        osfSrc.estimateZFromFaceSize = true;
        osfSrc.neutralDepthMeters = 0.6f;
        osfSrc.zBlend = 0.85f;
        osfSrc.zClamp = new Vector2(0.2f, 2.5f);
        osfSrc.zSmoothTime = 0.12f;

        // Face tracker (Direct)
        var managerGO = new GameObject("FaceTrackerManager");
        var manager = managerGO.AddComponent<FaceTrackerManager>();
        manager.sourceBehaviour = osfSrc;
        manager.cameraPivot = headPivot;
        manager.motionMode = FaceTrackerManager.MotionMode.Direct;

        // Tunnel geometry parented to ScreenCenter (extends into -forward)
        var tunnelRoot = new GameObject("TunnelRoot").transform;
        tunnelRoot.SetParent(screenCenter, false);
        tunnelRoot.localPosition = Vector3.zero;
        tunnelRoot.localRotation = Quaternion.identity;

        float w = 1.2f; // default if offaxis not set yet
        float h = 0.68f;
        try { w = Mathf.Max(0.1f, offaxis.screenWidth * 2.0f); h = Mathf.Max(0.1f, offaxis.screenHeight * 2.0f); } catch { }
        float thickness = 0.01f;
        var lineMat = MakeUnlit(new Color(0.55f, 0.55f, 0.65f));

        for (int i = 1; i <= 32; i++)
        {
            float z = -i * 0.25f; // into screen
            CreateFrame(tunnelRoot, new Vector3(0, 0, z), w, h, thickness, lineMat);
        }

        // Target spheres at different depths
        var redMat = MakeUnlit(new Color(1f, 0.2f, 0.2f));
        float[] depths = { -0.5f, -1.0f, -1.8f, -2.6f };
        Vector3[] offsets = { new Vector3(-0.25f, -0.1f, 0), new Vector3(0.3f, 0.15f, 0), new Vector3(-0.35f, 0.22f, 0), new Vector3(0.15f, -0.2f, 0) };
        for (int i = 0; i < depths.Length; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = $"Target_{i}";
            s.transform.SetParent(tunnelRoot, false);
            s.transform.localPosition = new Vector3(offsets[i].x, offsets[i].y, depths[i]);
            s.transform.localScale = Vector3.one * (0.06f + 0.04f * i);
            var mr = s.GetComponent<MeshRenderer>();
            mr.sharedMaterial = redMat;
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
        string path = "Assets/Scenes/BelenTunnelSample.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Belén Tunnel Sample", "Tunnel sample scene created at\n" + path, "OK");
    }

    static void CreateFrame(Transform parent, Vector3 localCenter, float width, float height, float thickness, Material mat)
    {
        float hw = width * 0.5f;
        float hh = height * 0.5f;
        // Four thin cubes: top, bottom, left, right
        void Bar(Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter + pos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }
        Bar(new Vector3(0, hh, 0), new Vector3(width, thickness, thickness));     // top
        Bar(new Vector3(0, -hh, 0), new Vector3(width, thickness, thickness));    // bottom
        Bar(new Vector3(-hw, 0, 0), new Vector3(thickness, height, thickness));   // left
        Bar(new Vector3(hw, 0, 0), new Vector3(thickness, height, thickness));    // right
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

