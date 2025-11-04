#!/usr/bin/env python3
"""
Webcam head-pose to Unity (UDP JSON) using MediaPipe FaceMesh + OpenCV solvePnP.

Install deps (Windows):
  py -m pip install opencv-python mediapipe numpy

Run:
  py Tools/mediapipe_headpose_udp.py --cam 0 --host 127.0.0.1 --port 11573

Adjust scale/offset as needed to match your physical setup.
"""
import argparse, json, socket, time
from math import atan2, asin, copysign, pi

import cv2
import numpy as np
import mediapipe as mp


def euler_from_rotmat(R):
    # XYZ (pitch, yaw, roll) from rotation matrix (right-handed)
    sy = -R[2, 0]
    cy = (1.0 - sy * sy) ** 0.5
    singular = cy < 1e-6
    if not singular:
        pitch = atan2(R[2, 1], R[2, 2])  # x
        yaw = asin(sy)                    # y
        roll = atan2(R[1, 0], R[0, 0])   # z
    else:
        pitch = atan2(-R[1, 2], R[1, 1])
        yaw = asin(sy)
        roll = 0.0
    return np.degrees([pitch, yaw, roll])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--cam', type=int, default=0)
    ap.add_argument('--host', type=str, default='127.0.0.1')
    ap.add_argument('--port', type=int, default=11573)
    ap.add_argument('--show', action='store_true', help='Show debug window')
    ap.add_argument('--neutral_z', type=float, default=0.6, help='Baseline distance in meters')
    ap.add_argument('--z_scale', type=float, default=1.0, help='Scale depth changes')
    args = ap.parse_args()

    addr = (args.host, args.port)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    cap = cv2.VideoCapture(args.cam)
    if not cap.isOpened():
        raise SystemExit('Cannot open camera index %d' % args.cam)

    mp_face = mp.solutions.face_mesh
    face = mp_face.FaceMesh(static_image_mode=False,
                            refine_landmarks=True,
                            max_num_faces=1,
                            min_detection_confidence=0.5,
                            min_tracking_confidence=0.5)

    # 3D model points (approx meters) for: nose tip, chin, left eye corner, right eye corner, left mouth, right mouth
    model_points = np.array([
        [0.0, 0.0, 0.0],          # Nose tip
        [0.0, -0.090, -0.010],    # Chin
        [-0.035, 0.030, -0.030],  # Left eye left corner
        [0.035, 0.030, -0.030],   # Right eye right corner
        [-0.028, -0.028, -0.020], # Left Mouth corner
        [0.028, -0.028, -0.020],  # Right mouth corner
    ], dtype=np.float32)

    # FaceMesh landmark indices for the above points
    IDX_NOSE_TIP = 1
    IDX_CHIN = 152
    IDX_LEFT_EYE = 33
    IDX_RIGHT_EYE = 263
    IDX_LEFT_MOUTH = 61
    IDX_RIGHT_MOUTH = 291

    last_ts = time.time()

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                break
            h, w = frame.shape[:2]
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            res = face.process(rgb)

            if not res.multi_face_landmarks:
                if args.show:
                    cv2.imshow('headpose', frame)
                    if cv2.waitKey(1) & 0xFF == 27:
                        break
                continue

            lm = res.multi_face_landmarks[0].landmark
            image_points = np.array([
                [lm[IDX_NOSE_TIP].x * w, lm[IDX_NOSE_TIP].y * h],
                [lm[IDX_CHIN].x * w, lm[IDX_CHIN].y * h],
                [lm[IDX_LEFT_EYE].x * w, lm[IDX_LEFT_EYE].y * h],
                [lm[IDX_RIGHT_EYE].x * w, lm[IDX_RIGHT_EYE].y * h],
                [lm[IDX_LEFT_MOUTH].x * w, lm[IDX_LEFT_MOUTH].y * h],
                [lm[IDX_RIGHT_MOUTH].x * w, lm[IDX_RIGHT_MOUTH].y * h],
            ], dtype=np.float32)

            # Approx camera intrinsics
            focal = w
            center = (w / 2.0, h / 2.0)
            camera_matrix = np.array([
                [focal, 0, center[0]],
                [0, focal, center[1]],
                [0, 0, 1]
            ], dtype=np.float32)
            dist_coeffs = np.zeros((4, 1), dtype=np.float32)

            ok, rvec, tvec = cv2.solvePnP(model_points, image_points, camera_matrix, dist_coeffs, flags=cv2.SOLVEPNP_ITERATIVE)
            if not ok:
                continue

            R, _ = cv2.Rodrigues(rvec)
            pitch, yaw, roll = euler_from_rotmat(R)

            # Position in camera coords (meters, approx). tvec is model origin (nose) position.
            tx, ty, tz = tvec.flatten().astype(float)
            # Adjust baseline and scaling to keep depth around neutral_z
            tz_m = max(0.2, args.neutral_z + args.z_scale * (tz - 0.5))  # rough centering
            pos = [float(tx), float(-ty), float(tz_m)]
            rot = [float(pitch), float(yaw), float(roll)]

            msg = {"pos": pos, "rot": rot, "ts": time.time()}
            data = json.dumps(msg).encode('utf-8')
            sock.sendto(data, addr)

            if args.show:
                # Draw nose point and axes
                nose_pt = tuple(image_points[0].astype(int))
                cv2.circle(frame, nose_pt, 3, (0, 255, 0), -1)
                cv2.putText(frame, f"p:{pitch:.1f} y:{yaw:.1f} r:{roll:.1f}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0,255,0), 2)
                cv2.imshow('headpose', frame)
                if cv2.waitKey(1) & 0xFF == 27:
                    break
    finally:
        cap.release()
        cv2.destroyAllWindows()


if __name__ == '__main__':
    main()

