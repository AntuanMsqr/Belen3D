using UnityEngine;
using Belen.Tracking;
using Belen.Tracking.Sources;
using Belen.Rendering;

namespace Belen.DebugUI
{
    // Minimal IMGUI calibration panel. Toggle with F1. Adjusts flips, offsets, smoothing live.
    public class CalibrationPanel : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.F1;
        public KeyCode toggleSourceKey = KeyCode.F2;
        public bool visible = true;

        public FaceTrackerManager tracker;
        public MonoBehaviour sourceBehaviour; // active
        public OffAxisCamera offAxis;
        public Belen.Rendering.SimpleParallaxCamera simpleParallax;
        public bool simpleParallaxOnly = true;

        // Optional known sources for quick switching
        public MonoBehaviour openSeeSource; // OpenSeeHeadPoseSource
        public MonoBehaviour keyboardSource; // KeyboardHeadPoseEmulator

        private Rect _win = new Rect(10, 120, 520, 560);
        private Vector2 _scroll;

        void Reset()
        {
            if (tracker == null) tracker = FindObjectOfType<FaceTrackerManager>();
            if (offAxis == null) offAxis = FindObjectOfType<OffAxisCamera>();
            if (sourceBehaviour == null && tracker != null) sourceBehaviour = tracker.sourceBehaviour;
            if (simpleParallax == null) simpleParallax = FindObjectOfType<Belen.Rendering.SimpleParallaxCamera>();

            // Try auto-detect known sources
            if (openSeeSource == null)
            {
                var osf = FindObjectOfType<OpenSeeHeadPoseSource>();
                if (osf != null) openSeeSource = osf;
            }
            if (keyboardSource == null)
            {
                var kb = FindObjectOfType<KeyboardHeadPoseEmulator>();
                if (kb != null) keyboardSource = kb;
            }
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) visible = !visible;
                if (kb.f2Key.wasPressedThisFrame) ToggleSource();
            }
#else
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
            if (Input.GetKeyDown(toggleSourceKey)) ToggleSource();
#endif
        }

        void OnGUI()
        {
            if (!visible) return;
            _win = GUI.Window(9871, _win, DrawWindow, "Belén Calibration (F1)");
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            _scroll = GUILayout.BeginScrollView(_scroll);
            // If focusing only on SimpleParallax settings, show minimalist panel
            if (simpleParallaxOnly && simpleParallax != null)
            {
                GUILayout.Label("Simple Parallax Camera");
                var sp = simpleParallax;
                sp.invertX = GUILayout.Toggle(sp.invertX, "invertX (derecha = derecha)");
                sp.invertY = GUILayout.Toggle(sp.invertY, "invertY (arriba = arriba)");
                sp.useZ = GUILayout.Toggle(sp.useZ, "usar Z (acercar/alejar)");
                sp.gainXY.x = FloatField("gainX", sp.gainXY.x);
                sp.gainXY.y = FloatField("gainY", sp.gainXY.y);
                sp.zGain = FloatField("zGain", sp.zGain);
                GUILayout.Label("Clamp XY (m)");
                sp.clampX = new Vector2(FloatField("clampX.min", sp.clampX.x), FloatField("clampX.max", sp.clampX.y));
                sp.clampY = new Vector2(FloatField("clampY.min", sp.clampY.x), FloatField("clampY.max", sp.clampY.y));
                GUILayout.Label("Clamp Z (m)");
                sp.clampZ = new Vector2(FloatField("clampZ.min", sp.clampZ.x), FloatField("clampZ.max", sp.clampZ.y));
                sp.focusDepth = FloatField("focusDepth", sp.focusDepth);
                sp.smooth = GUILayout.Toggle(sp.smooth, "smooth");
                sp.posSmoothTimeXY = FloatField("posSmoothXY", sp.posSmoothTimeXY);
                sp.posSmoothTimeZ = FloatField("posSmoothZ", sp.posSmoothTimeZ);
                sp.rotSmoothTime = FloatField("rotSmooth", sp.rotSmoothTime);
                sp.deadzoneX = FloatField("deadzoneX", sp.deadzoneX);
                sp.deadzoneY = FloatField("deadzoneY", sp.deadzoneY);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            // Source switching
            GUILayout.Label("Source");
            using (new GUILayout.HorizontalScope())
            {
                bool usingOpenSee = tracker != null && tracker.sourceBehaviour == openSeeSource && openSeeSource != null;
                bool usingKeyboard = tracker != null && tracker.sourceBehaviour == keyboardSource && keyboardSource != null;
                GUI.enabled = openSeeSource != null;
                if (GUILayout.Button(usingOpenSee ? "OpenSee (active)" : "Use OpenSee"))
                {
                    SwitchSource(openSeeSource);
                }
                GUI.enabled = keyboardSource != null;
                if (GUILayout.Button(usingKeyboard ? "Keyboard (active)" : "Use Keyboard"))
                {
                    SwitchSource(keyboardSource);
                }
                GUI.enabled = true;
            }

            if (tracker != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Camera Motion");
                using (new GUILayout.HorizontalScope())
                {
                    bool isDirect = tracker.motionMode == FaceTrackerManager.MotionMode.Direct;
                    if (GUILayout.Button(isDirect ? "Direct (active)" : "Use Direct"))
                        tracker.motionMode = FaceTrackerManager.MotionMode.Direct;
                    bool isOrbit = tracker.motionMode == FaceTrackerManager.MotionMode.OrbitTarget;
                    if (GUILayout.Button(isOrbit ? "Orbit (active)" : "Use Orbit"))
                        tracker.motionMode = FaceTrackerManager.MotionMode.OrbitTarget;
                }
                if (tracker.motionMode == FaceTrackerManager.MotionMode.OrbitTarget)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Preset: Cinematic")) ApplyOrbitPresetCinematic();
                        if (GUILayout.Button("Preset: Reactive")) ApplyOrbitPresetReactive();
                        if (GUILayout.Button("Flip 180°"))
                        {
                            // Quick toggle to correct cameras that start facing backwards
                            tracker.compositionOffsetDeg = new Vector2(NormDeg(tracker.compositionOffsetDeg.x + 180f), tracker.compositionOffsetDeg.y);
                            tracker.SaveCalibration();
                        }
                    }
                    tracker.applyOrbitStartOnEnable = GUILayout.Toggle(tracker.applyOrbitStartOnEnable, "applyStartOnEnable");
                    tracker.orbitStartYaw = FloatField("startYaw", tracker.orbitStartYaw);
                    tracker.orbitStartPitch = FloatField("startPitch", tracker.orbitStartPitch);
                    tracker.orbitStartDistance = FloatField("startDist", tracker.orbitStartDistance);
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Set Start = Current"))
                        {
                            tracker.SetOrbitStartFromCurrent();
                            tracker.SaveCalibration();
                        }
                        if (GUILayout.Button("Apply Start Now"))
                        {
                            tracker.ApplyOrbitStartNow();
                            tracker.SaveCalibration();
                        }
                    }
                    tracker.orbitBaseDistance = FloatField("baseDist", tracker.orbitBaseDistance);
                    tracker.yawDegreesPerMeter = FloatField("yawDeg/m", tracker.yawDegreesPerMeter);
                    tracker.pitchDegreesPerMeter = FloatField("pitchDeg/m", tracker.pitchDegreesPerMeter);
                    tracker.dollyMetersPerMeter = FloatField("dolly m/m", tracker.dollyMetersPerMeter);
                    tracker.orbitKeepHorizon = GUILayout.Toggle(tracker.orbitKeepHorizon, "keepHorizon");
                    tracker.responseExponent = FloatField("responseExp", tracker.responseExponent);
                    tracker.orbitSmoothTime = FloatField("smoothTime", tracker.orbitSmoothTime);
                    tracker.deadzoneX = FloatField("deadzoneX", tracker.deadzoneX);
                    tracker.deadzoneY = FloatField("deadzoneY", tracker.deadzoneY);
                    var ycl = FloatField("yawMin", tracker.yawClamp.x);
                    var ycr = FloatField("yawMax", tracker.yawClamp.y);
                    tracker.yawClamp = new Vector2(Mathf.Min(ycl, ycr), Mathf.Max(ycl, ycr));
                    var coYaw = FloatField("compYaw", tracker.compositionOffsetDeg.x);
                    var coPitch = FloatField("compPitch", tracker.compositionOffsetDeg.y);
                    tracker.compositionOffsetDeg = new Vector2(coYaw, coPitch);
                    var pmin = FloatField("pitchMin", tracker.pitchClamp.x);
                    var pmax = FloatField("pitchMax", tracker.pitchClamp.y);
                    tracker.pitchClamp = new Vector2(Mathf.Min(pmin, pmax), Mathf.Max(pmin, pmax));
                    var dmin = FloatField("distMin", tracker.distanceClamp.x);
                    var dmax = FloatField("distMax", tracker.distanceClamp.y);
                    tracker.distanceClamp = new Vector2(Mathf.Min(dmin, dmax), Mathf.Max(dmin, dmax));
                }

                GUILayout.Label("Smoothing");
                var filter = tracker.filter;
                float pa = GUILayout.HorizontalSlider(filter.positionAlpha, 0f, 1f);
                GUILayout.Label($"positionAlpha: {pa:F2}");
                float ra = GUILayout.HorizontalSlider(filter.rotationAlpha, 0f, 1f);
                GUILayout.Label($"rotationAlpha: {ra:F2}");
                filter.positionAlpha = pa; filter.rotationAlpha = ra;

                GUILayout.Space(6);
                GUILayout.Label("Offsets");
                var posOff = tracker.positionOffset;
                posOff.x = FloatField("pos.x", posOff.x);
                posOff.y = FloatField("pos.y", posOff.y);
                posOff.z = FloatField("pos.z", posOff.z);
                tracker.positionOffset = posOff;
                var rotOff = tracker.rotationOffset;
                rotOff.x = FloatField("rot.x", rotOff.x);
                rotOff.y = FloatField("rot.y", rotOff.y);
                rotOff.z = FloatField("rot.z", rotOff.z);
                tracker.rotationOffset = rotOff;

                GUILayout.Space(6);
                GUILayout.Label("Distance Gain");
                bool useDG = GUILayout.Toggle(tracker.useDistanceGain, "useDistanceGain");
                tracker.useDistanceGain = useDG;
                tracker.neutralZ = FloatField("neutralZ", tracker.neutralZ);
                tracker.distanceGain = FloatField("distanceGain", tracker.distanceGain);
                // Show clamp but keep simple inputs
                var minZ = FloatField("zMin", tracker.zClamp.x);
                var maxZ = FloatField("zMax", tracker.zClamp.y);
                tracker.zClamp = new Vector2(Mathf.Min(minZ, maxZ), Mathf.Max(minZ, maxZ));

                GUILayout.Space(6);
                GUILayout.Label("Auto Neutral");
                tracker.autoNeutral = GUILayout.Toggle(tracker.autoNeutral, "autoNeutral");
                tracker.autoNeutralDuration = FloatField("autoDuration", tracker.autoNeutralDuration);
                tracker.snapNeutralOnStart = GUILayout.Toggle(tracker.snapNeutralOnStart, "snapNeutralOnStart");
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set neutral now"))
                    {
                        Vector3 last = tracker.lastFilteredPosition;
                        tracker.SetNeutralNow(last.z);
                        tracker.SetNeutralXY(last.x, last.y);
                    }
                    if (GUILayout.Button("Start auto"))
                    {
                        tracker.StartNeutralLearning(tracker.autoNeutralDuration);
                    }
                    if (GUILayout.Button("Stop auto"))
                    {
                        tracker.StopNeutralLearning();
                    }
                }
            }

            if (sourceBehaviour is UdpHeadPoseReceiver udp)
            {
                GUILayout.Space(6);
                GUILayout.Label("UDP Source");
                udp.positionScale = FloatField("positionScale", udp.positionScale);
                bool invertZ = GUILayout.Toggle(udp.invertZ, "invertZ");
                bool swapYZ = GUILayout.Toggle(udp.swapYZ, "swapYZ");
                udp.invertZ = invertZ; udp.swapYZ = swapYZ;
                GUILayout.Label($"Port: {udp.listenPort}");
            }
            else if (sourceBehaviour is OpenSeeHeadPoseSource osf)
            {
                GUILayout.Space(6);
                GUILayout.Label("OpenSee Source");
                osf.positionScale = FloatField("positionScale", osf.positionScale);
                // Per-axis scaling
                osf.axisScale.x = FloatField("axisScale.x", osf.axisScale.x);
                osf.axisScale.y = FloatField("axisScale.y", osf.axisScale.y);
                osf.axisScale.z = FloatField("axisScale.z", osf.axisScale.z);
                osf.swapYZ = GUILayout.Toggle(osf.swapYZ, "swapYZ");
                osf.invertX = GUILayout.Toggle(osf.invertX, "invertX");
                osf.invertY = GUILayout.Toggle(osf.invertY, "invertY");
                osf.invertZ = GUILayout.Toggle(osf.invertZ, "invertZ");
                osf.positionOffset.x = FloatField("posOff.x", osf.positionOffset.x);
                osf.positionOffset.y = FloatField("posOff.y", osf.positionOffset.y);
                osf.positionOffset.z = FloatField("posOff.z", osf.positionOffset.z);

                GUILayout.Space(4);
                GUILayout.Label("Z from Face Size");
                osf.estimateZFromFaceSize = GUILayout.Toggle(osf.estimateZFromFaceSize, "estimateZFromFaceSize");
                osf.neutralDepthMeters = FloatField("neutralDepth(m)", osf.neutralDepthMeters);
                osf.neutralFacePixels = FloatField("neutralFace(px)", osf.neutralFacePixels);
                GUILayout.Label($"currentFace(px): {osf.GetLastMeasuredFacePixels():F1}");
                osf.zBlend = FloatField("zBlend 0..1", osf.zBlend);
                osf.zSmoothTime = FloatField("zSmooth(s)", osf.zSmoothTime);
                var zmin = FloatField("zClampMin", osf.zClamp.x);
                var zmax = FloatField("zClampMax", osf.zClamp.y);
                osf.zClamp = new Vector2(Mathf.Min(zmin, zmax), Mathf.Max(zmin, zmax));
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Calibrate face size now"))
                    {
                        osf.CalibrateNeutralFaceSizeFromLast();
                        SaveSourceSettings();
                    }
                }
            }

            if (offAxis != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Off-Axis Screen Size (m)");
                offAxis.screenWidth = FloatField("width", offAxis.screenWidth);
                offAxis.screenHeight = FloatField("height", offAxis.screenHeight);

                // Off-axis toggle
                bool oa = offAxis.enabled && offAxis.overrideCameraMatrices;
                bool newOa = GUILayout.Toggle(oa, "Enable Off-Axis (HCP)");
                if (newOa != oa)
                {
                    offAxis.enabled = newOa;
                    offAxis.overrideCameraMatrices = newOa;
                    var cam = Camera.main;
                    if (cam != null && newOa)
                    {
                        cam.orthographic = false;
                    }
                    if (tracker != null)
                    {
                        tracker.motionMode = newOa ? FaceTrackerManager.MotionMode.Direct : FaceTrackerManager.MotionMode.OrbitTarget;
                    }
                    // If enabling HCP, prefer OpenSee source for best depth response
                    if (newOa && openSeeSource != null && tracker != null && tracker.sourceBehaviour != openSeeSource)
                    {
                        SwitchSource(openSeeSource);
                    }
                }
                // Flip screen axes if parallax feels inverted
                offAxis.flipScreenRight = GUILayout.Toggle(offAxis.flipScreenRight, "flipScreenRight (invert screen right)");
                offAxis.flipScreenUp = GUILayout.Toggle(offAxis.flipScreenUp, "flipScreenUp (invert screen up)");
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Flip Screen Handedness (Z+180°)"))
                    {
                        // Safer than flipScreenRight: rotate the screen rectangle around its own normal
                        var sc = offAxis.screenCenter;
                        if (sc != null)
                        {
                            sc.rotation = Quaternion.AngleAxis(180f, sc.forward) * sc.rotation;
                            // After rotating, re-center the off-axis setup to avoid drift
                            CenterOffAxis();
                        }
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Center Now"))
                    {
                        CenterOffAxis();
                    }
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Persistence");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    tracker?.SaveCalibration();
                    SaveSourceSettings();
                }
                if (GUILayout.Button("Load"))
                {
                    tracker?.LoadCalibration();
                    LoadSourceSettings();
                }
                if (GUILayout.Button("Clear"))
                {
                    tracker?.ClearCalibration();
                    ClearSourceSettings();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void SwitchSource(MonoBehaviour target)
        {
            if (tracker == null || target == null) return;
            tracker.sourceBehaviour = target;
            sourceBehaviour = target;
            // Toggle GO activity for clarity
            if (keyboardSource != null && keyboardSource != target)
                keyboardSource.gameObject.SetActive(false);
            if (openSeeSource != null && openSeeSource != target)
                openSeeSource.gameObject.SetActive(false);
            if (!target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
        }

        void ToggleSource()
        {
            if (tracker == null) return;
            if (tracker.sourceBehaviour == openSeeSource && keyboardSource != null)
            {
                SwitchSource(keyboardSource);
            }
            else if (tracker.sourceBehaviour == keyboardSource && openSeeSource != null)
            {
                SwitchSource(openSeeSource);
            }
            else if (openSeeSource != null)
            {
                SwitchSource(openSeeSource);
            }
            else if (keyboardSource != null)
            {
                SwitchSource(keyboardSource);
            }
        }

        void ApplyOrbitPresetCinematic()
        {
            if (tracker == null) return;
            tracker.orbitBaseDistance = 1.6f;
            tracker.yawDegreesPerMeter = 220f;
            tracker.pitchDegreesPerMeter = 220f;
            tracker.dollyMetersPerMeter = 0.6f;
            tracker.orbitSmoothTime = 0.22f;
            tracker.responseExponent = 1.2f;
            tracker.deadzoneX = 0.02f;
            tracker.deadzoneY = 0.02f;
            tracker.orbitKeepHorizon = true;
            tracker.yawClamp = new Vector2(-45f, 45f);
            tracker.pitchClamp = new Vector2(-20f, 20f);
            tracker.distanceClamp = new Vector2(0.6f, 2.5f);
            tracker.compositionOffsetDeg = new Vector2(5f, -2f);
            // Persist right away
            try { tracker.SaveCalibration(); } catch { }
            PlayerPrefs.Save();
        }

        void ApplyOrbitPresetReactive()
        {
            if (tracker == null) return;
            tracker.orbitBaseDistance = 1.2f;
            tracker.yawDegreesPerMeter = 480f;
            tracker.pitchDegreesPerMeter = 420f;
            tracker.dollyMetersPerMeter = 1.2f;
            tracker.orbitSmoothTime = 0.08f;
            tracker.responseExponent = 1.0f;
            tracker.deadzoneX = 0.01f;
            tracker.deadzoneY = 0.01f;
            tracker.orbitKeepHorizon = true;
            tracker.yawClamp = new Vector2(-70f, 70f);
            tracker.pitchClamp = new Vector2(-35f, 35f);
            tracker.distanceClamp = new Vector2(0.3f, 3.0f);
            tracker.compositionOffsetDeg = new Vector2(0f, 0f);
            // Persist right away
            try { tracker.SaveCalibration(); } catch { }
            PlayerPrefs.Save();
        }

        void CenterOffAxis()
        {
            if (offAxis == null || tracker == null || tracker.cameraPivot == null) return;
            var sc = offAxis.screenCenter;
            if (sc == null) return;

            // Ensure HCP active and Direct mode
            offAxis.enabled = true;
            offAxis.overrideCameraMatrices = true;
            tracker.motionMode = FaceTrackerManager.MotionMode.Direct;

            Vector3 head = tracker.cameraPivot.position;
            Vector3 up = Vector3.up;
            Vector3 forward = head - sc.position;
            // Project forward onto horizontal plane to keep screen upright
            Vector3 fProj = Vector3.ProjectOnPlane(forward, up);
            if (fProj.sqrMagnitude > 1e-6f) forward = fProj;
            else if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            sc.rotation = Quaternion.LookRotation(forward.normalized, up);
            // Set neutral from current filtered pose and place screen accordingly
            var last = tracker.lastFilteredPosition;
            float z = last.z;
            if (z < 0.05f) z = 0.6f; // reasonable default if unset
            tracker.SetNeutralNow(z);
            tracker.SetNeutralXY(last.x, last.y);
            sc.position = head - sc.forward * tracker.neutralZ;

            // Persist calibration and off-axis size
            try { tracker.SaveCalibration(); } catch { }
            try { SaveSourceSettings(); } catch { }

            // If OpenSee source is present, sync its neutral depth and face size
            if (sourceBehaviour is OpenSeeHeadPoseSource osf)
            {
                osf.neutralDepthMeters = tracker.neutralZ;
                osf.CalibrateNeutralFaceSizeFromLast();
                SaveSourceSettings();
            }
        }

        float FloatField(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            string s = GUILayout.TextField(value.ToString("F3"), GUILayout.Width(80));
            GUILayout.EndHorizontal();
            if (float.TryParse(s, out float v)) return v; else return value;
        }

        static float NormDeg(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        // Persist minimal source settings
        const string PrefUdp = "Belen/Udp/";
        const string PrefOsf = "Belen/OpenSee/";
        const string PrefOffAxis = "Belen/OffAxis/";

        void SaveSourceSettings()
        {
            if (sourceBehaviour is UdpHeadPoseReceiver udp)
            {
                PlayerPrefs.SetInt(PrefUdp + "invertZ", udp.invertZ ? 1 : 0);
                PlayerPrefs.SetInt(PrefUdp + "swapYZ", udp.swapYZ ? 1 : 0);
                PlayerPrefs.SetFloat(PrefUdp + "positionScale", udp.positionScale);
            }
            else if (sourceBehaviour is OpenSeeHeadPoseSource osf)
            {
                PlayerPrefs.SetFloat(PrefOsf + "positionScale", osf.positionScale);
                PlayerPrefs.SetFloat(PrefOsf + "posOffX", osf.positionOffset.x);
                PlayerPrefs.SetFloat(PrefOsf + "posOffY", osf.positionOffset.y);
                PlayerPrefs.SetFloat(PrefOsf + "posOffZ", osf.positionOffset.z);
            }
            if (offAxis != null)
            {
                PlayerPrefs.SetFloat(PrefOffAxis + "width", offAxis.screenWidth);
                PlayerPrefs.SetFloat(PrefOffAxis + "height", offAxis.screenHeight);
            }
            PlayerPrefs.Save();
        }

        void LoadSourceSettings()
        {
            if (sourceBehaviour is UdpHeadPoseReceiver udp)
            {
                if (PlayerPrefs.HasKey(PrefUdp + "invertZ")) udp.invertZ = PlayerPrefs.GetInt(PrefUdp + "invertZ") != 0;
                if (PlayerPrefs.HasKey(PrefUdp + "swapYZ")) udp.swapYZ = PlayerPrefs.GetInt(PrefUdp + "swapYZ") != 0;
                if (PlayerPrefs.HasKey(PrefUdp + "positionScale")) udp.positionScale = PlayerPrefs.GetFloat(PrefUdp + "positionScale");
            }
            else if (sourceBehaviour is OpenSeeHeadPoseSource osf)
            {
                if (PlayerPrefs.HasKey(PrefOsf + "positionScale")) osf.positionScale = PlayerPrefs.GetFloat(PrefOsf + "positionScale");
                var hasX = PlayerPrefs.HasKey(PrefOsf + "posOffX");
                if (hasX)
                {
                    osf.positionOffset = new Vector3(
                        PlayerPrefs.GetFloat(PrefOsf + "posOffX", osf.positionOffset.x),
                        PlayerPrefs.GetFloat(PrefOsf + "posOffY", osf.positionOffset.y),
                        PlayerPrefs.GetFloat(PrefOsf + "posOffZ", osf.positionOffset.z)
                    );
                }
            }
            if (offAxis != null)
            {
                if (PlayerPrefs.HasKey(PrefOffAxis + "width")) offAxis.screenWidth = PlayerPrefs.GetFloat(PrefOffAxis + "width", offAxis.screenWidth);
                if (PlayerPrefs.HasKey(PrefOffAxis + "height")) offAxis.screenHeight = PlayerPrefs.GetFloat(PrefOffAxis + "height", offAxis.screenHeight);
            }
        }

        void ClearSourceSettings()
        {
            PlayerPrefs.DeleteKey(PrefUdp + "invertZ");
            PlayerPrefs.DeleteKey(PrefUdp + "swapYZ");
            PlayerPrefs.DeleteKey(PrefUdp + "positionScale");
            PlayerPrefs.DeleteKey(PrefOsf + "positionScale");
            PlayerPrefs.DeleteKey(PrefOsf + "posOffX");
            PlayerPrefs.DeleteKey(PrefOsf + "posOffY");
            PlayerPrefs.DeleteKey(PrefOsf + "posOffZ");
            PlayerPrefs.DeleteKey(PrefOffAxis + "width");
            PlayerPrefs.DeleteKey(PrefOffAxis + "height");
        }
    }
}
