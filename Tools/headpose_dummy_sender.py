#!/usr/bin/env python3
import json, math, socket, time

ADDR = ("127.0.0.1", 11573)
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

print("Sending dummy head pose to %s:%d (Ctrl+C to quit)" % ADDR)
start = time.time()
try:
    while True:
        t = time.time() - start
        # gentle circular motion + small nod
        x = 0.05 * math.sin(t * 0.6)
        y = 0.02 * math.sin(t * 0.9)
        z = 0.6 + 0.03 * math.cos(t * 0.5)
        pitch = 5.0 * math.sin(t * 0.7)
        yaw = 8.0 * math.sin(t * 0.5)
        roll = 3.0 * math.cos(t * 0.4)
        msg = {"pos": [x, y, z], "rot": [pitch, yaw, roll], "ts": time.time()}
        data = json.dumps(msg).encode("utf-8")
        sock.sendto(data, ADDR)
        time.sleep(1/60)
except KeyboardInterrupt:
    pass

