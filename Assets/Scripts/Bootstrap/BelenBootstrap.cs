using UnityEngine;
using UnityEngine.UI;
using Belen.Rendering;
using Belen.Tracking;
using Belen.Tracking.Sources;
using Belen.Interaction;
using Belen.UI;
using Belen.Audio;
using Belen.Scenes;

// Optional runtime bootstrap that builds a minimal rig if no OffAxisCamera is found.
public class BelenBootstrap : MonoBehaviour
{
    public bool buildIfMissing = true;

    void Start()
    {
        if (!buildIfMissing) return;
        if (FindObjectOfType<OffAxisCamera>() != null) return;

        // Head pivot and camera
        var headPivot = new GameObject("HeadPivot").transform;
        headPivot.position = new Vector3(0f, 0.2f, 0.6f);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(headPivot, false);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.01f;

        // Screen center
        var screenCenter = new GameObject("ScreenCenter").transform;
        screenCenter.position = Vector3.zero;
        screenCenter.rotation = Quaternion.identity;

        var offaxis = camGO.AddComponent<OffAxisCamera>();
        offaxis.screenCenter = screenCenter;
        offaxis.screenWidth = 0.6f;
        offaxis.screenHeight = 0.34f;
        offaxis.eyeTransform = headPivot;
        offaxis.overrideCameraMatrices = false;
        offaxis.enabled = false;

        // Tracking source + manager
        var srcGO = new GameObject("KeyboardHeadPoseEmulator");
        var src = srcGO.AddComponent<KeyboardHeadPoseEmulator>();
        src.position = headPivot.position;

        var managerGO = new GameObject("FaceTrackerManager");
        var manager = managerGO.AddComponent<FaceTrackerManager>();
        manager.sourceBehaviour = src;
        manager.cameraPivot = headPivot;
        manager.motionMode = FaceTrackerManager.MotionMode.OrbitTarget;
        manager.orbitTarget = screenCenter;
        manager.orbitBaseDistance = 1.2f;
        manager.yawDegreesPerMeter = 350f;
        manager.pitchDegreesPerMeter = 350f;
        manager.dollyMetersPerMeter = 1.0f;
        manager.pitchClamp = new Vector2(-30f, 30f);
        manager.distanceClamp = new Vector2(0.3f, 3.0f);

        // Presence + UI
        var presenceGO = new GameObject("PresenceDetector");
        var presence = presenceGO.AddComponent<PresenceDetector>();
        presence.sourceBehaviour = src;

        var canvasGO = new GameObject("PromptCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        var cg = canvasGO.AddComponent<CanvasGroup>();
        var prompt = canvasGO.AddComponent<UIPromptController>();
        prompt.presence = presence;

        var textGO = new GameObject("PromptText");
        textGO.transform.SetParent(canvasGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 36;
        txt.color = new Color(1f, 1f, 1f, 0.95f);
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.text = "Muévete frente a la pantalla\npara descubrir el belén";
        var rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Audio crossfader
        var audioGO = new GameObject("Audio");
        audioGO.AddComponent<AudioCrossfader>();

        // Scene flow roots (empty placeholders)
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
    }
}
