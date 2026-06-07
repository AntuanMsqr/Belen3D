using System;
using UnityEngine;
using Hcp.HeadTracking.Domain;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hcp.HeadTracking.Infrastructure
{
    // Fallback input using keyboard for quick testing without a tracker.
    public class KeyboardHeadPoseEmulatorView : MonoBehaviour, IHeadPoseSource
    {
        [Header("Position (meters)")]
        public Vector3 position = new Vector3(0, 0.2f, 0.6f);
        public float moveSpeed = 0.3f;

        [Header("Rotation (degrees)")]
        public Vector3 euler = Vector3.zero; // pitch, yaw, roll
        public float rotSpeed = 30f;

        public event Action<HeadPose> OnPose;
        private HeadPose _latest;
        private bool _dirty;

        private void Update()
        {
            float dt = Time.deltaTime;

            Vector3 delta = Vector3.zero;
            Vector3 rdelta = Vector3.zero;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var k = Keyboard.current;
            if (k == null) return;

            if (k.aKey.isPressed) delta.x -= 1;
            if (k.dKey.isPressed) delta.x += 1;
            if (k.wKey.isPressed) delta.z += 1;
            if (k.sKey.isPressed) delta.z -= 1;
            if (k.rKey.isPressed) delta.y += 1;
            if (k.fKey.isPressed) delta.y -= 1;

            if (k.upArrowKey.isPressed) rdelta.x -= 1;
            if (k.downArrowKey.isPressed) rdelta.x += 1;
            if (k.leftArrowKey.isPressed) rdelta.y -= 1;
            if (k.rightArrowKey.isPressed) rdelta.y += 1;
            if (k.qKey.isPressed) rdelta.z += 1;
            if (k.eKey.isPressed) rdelta.z -= 1;
#else
            if (Input.GetKey(KeyCode.A)) delta.x -= 1;
            if (Input.GetKey(KeyCode.D)) delta.x += 1;
            if (Input.GetKey(KeyCode.W)) delta.z += 1;
            if (Input.GetKey(KeyCode.S)) delta.z -= 1;
            if (Input.GetKey(KeyCode.R)) delta.y += 1;
            if (Input.GetKey(KeyCode.F)) delta.y -= 1;

            if (Input.GetKey(KeyCode.UpArrow)) rdelta.x -= 1;
            if (Input.GetKey(KeyCode.DownArrow)) rdelta.x += 1;
            if (Input.GetKey(KeyCode.LeftArrow)) rdelta.y -= 1;
            if (Input.GetKey(KeyCode.RightArrow)) rdelta.y += 1;
            if (Input.GetKey(KeyCode.Q)) rdelta.z += 1;
            if (Input.GetKey(KeyCode.E)) rdelta.z -= 1;
#endif

            if (delta.sqrMagnitude > 0)
            {
                position += delta.normalized * moveSpeed * dt;
                _dirty = true;
            }

            if (rdelta.sqrMagnitude > 0)
            {
                euler += rdelta * rotSpeed * dt;
                _dirty = true;
            }

            if (_dirty)
            {
                _dirty = false;
                _latest = new HeadPose(position, euler, Time.realtimeSinceStartupAsDouble);
                OnPose?.Invoke(_latest);
            }
        }

        public bool TryGetLatest(out HeadPose pose)
        {
            pose = _latest;
            return true;
        }

        public void Start() { /* Mono: nothing */ }
        public void Stop() { /* Mono: nothing */ }
    }
}
