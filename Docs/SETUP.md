Belén Digital Interactivo — Unity HCP Setup
==========================================

Overview
- Head-coupled perspective (HCP) using off-axis projection.
- Real-time head pose via UDP (JSON or CSV) or keyboard emulator.
- Scene flow across 5 tableaux with audio crossfades.

Folder Map
- Assets/Scripts/Tracking: head pose types, filters, sources, manager.
- Assets/Scripts/Rendering: off-axis camera projection.
- Assets/Scripts/Interaction: presence detection.
- Assets/Scripts/UI: prompt overlay controller.
- Assets/Scripts/Audio: ambient music crossfader.
- Assets/Scripts/Scenes: 5-scene flow controller.

1) Create the Camera Rig
- Add empty GameObject `HeadPivot` at world origin. This represents the viewer’s eye.
- Parent `Main Camera` under `HeadPivot` at local position (0,0,0) and zero rotation.
- Add `FaceTrackerManager` to any object (e.g., `HeadPivot`).
  - `sourceBehaviour`: choose one:
    - `UdpHeadPoseReceiver` (listens on `11573` by default)
    - `KeyboardHeadPoseEmulator` (WASD/R/F to move, arrows + Q/E to rotate)
  - `cameraPivot`: drag `HeadPivot`.
  - Tweak `positionOffset` and `rotationOffset` if needed.

2) Define the Screen Plane
- Create empty GameObject `ScreenCenter`. Position it where your physical display surface is in the scene.
- Orient it so:
  - `right` axis matches screen right.
  - `up` axis matches screen up.
  - `forward` axis points OUT of the screen, toward the viewer.
- Add `OffAxisCamera` to `Main Camera`.
  - `screenCenter`: drag `ScreenCenter`.
  - Set `screenWidth` and `screenHeight` to real display size in meters.
  - `eyeTransform`: drag `HeadPivot`.
  - Near/Far as desired.
  - Enable gizmos to see the screen rectangle and normal.

3) Presence UI Prompt
- Add a Canvas with a full-screen `CanvasGroup` for the instruction text: “Muévete frente a la pantalla para descubrir el belén”.
- Add `PresenceDetector` to any object.
  - `sourceBehaviour`: same source as FaceTrackerManager.
- Add `UIPromptController` to the `CanvasGroup` object and link `presence`.

4) Scene Flow (5 Tableaux)
- Create five root GameObjects: `Anunciacion`, `CaminoABelen`, `Nacimiento`, `AnuncioPastores`, `AdoracionReyes`.
- Add `BelenSceneController` to an empty `SceneFlow` object.
  - For each array element `scenes[i]`, set:
    - `name` (optional), `duration` (20–25s), `music` (looping clip), `roots` (list containing the corresponding root GameObject).
- Add `AudioCrossfader` to an Audio object and link to `BelenSceneController.crossfader`.

5) Tracking Input (UDP)
- The receiver accepts either JSON or CSV per datagram:
  - JSON: {"pos":[x,y,z],"rot":[pitch,yaw,roll],"ts":seconds}
  - CSV: x,y,z,pitch,yaw,roll,ts
- Coordinates:
  - Position in meters. `invertZ` is on by default to match a screen facing +Z viewer.
  - Rotation as degrees: x=pitch, y=yaw, z=roll.
  - Use `swapYZ`/`invertZ` to adapt to your tracker’s axes.

Quick Test Without Tracker
- Add `KeyboardHeadPoseEmulator` as source and move around to validate the HCP effect.

Optional: Dummy UDP Sender
- Run `python Tools/headpose_dummy_sender.py` to send a gentle sinusoidal head movement to port 11573.

Calibration Tips
- Place `ScreenCenter` exactly at the physical screen plane. Use real width/height.
- Start with the camera and scene centered on the screen. Move the head (HeadPivot) near Z ≈ 0.6–1.0 m in front of the screen.
- If depth looks inverted, toggle `invertZ` on the UDP source and re-test.

Performance Notes
- Target 60 fps: prefer baked lighting + a few dynamic lights.
- Use LODs and GPU instancing for repeated assets (animals, crowd, foliage).
- Keep volumetrics/particles modest and localized.

Integrating OpenSeeFace (suggested)
- Run OpenSeeFace’s facetracker and output head translation/rotation.
- Convert to JSON/CSV above (or adapt the receiver) and send via UDP to port 11573.
- Tune smoothing (`HeadPoseExponentialFilter.positionAlpha/rotationAlpha`) for stability.

