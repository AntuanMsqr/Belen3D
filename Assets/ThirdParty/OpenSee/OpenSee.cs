using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace OpenSee
{
    // Minimal import of OpenSee UDP receiver to read OpenSeeFace packets directly in Unity.
    public class OpenSee : MonoBehaviour
    {
        [Header("UDP server settings")]
        public string listenAddress = "127.0.0.1";
        public int listenPort = 11573;

        private const int nPoints = 68;
        // Must match OpenSeeFace packet layout exactly (see Tools/OpenSeeFace/Unity/OpenSee.cs)
        private const int packetFrameSize = 8 + 4 + 2 * 4 + 2 * 4 + 1 + 4 + 4 * 4 + 3 * 4 + 3 * 4 + 4 * 68 + 4 * 2 * 68 + 4 * 3 * 70 + 4 * 14;

        [Header("Tracking data")]
        public int receivedPackets = 0;
        public OpenSeeData[] trackingData = null;
        public bool listening { get; private set; } = false;

        [Serializable]
        public class OpenSeeData
        {
            public double time;
            public int id;
            public Vector2 cameraResolution;
            public float rightEyeOpen;
            public float leftEyeOpen;
            public Quaternion rightGaze;
            public Quaternion leftGaze;
            public bool got3DPoints;
            public float fit3DError;
            public Vector3 rotation;
            public Vector3 translation;
            public Quaternion rawQuaternion;
            public Vector3 rawEuler;
            public float[] confidence;
            public Vector2[] points;
            public Vector3[] points3D;

            [Serializable]
            public class OpenSeeFeatures
            {
                public float EyeLeft, EyeRight;
                public float EyebrowSteepnessLeft, EyebrowUpDownLeft, EyebrowQuirkLeft;
                public float EyebrowSteepnessRight, EyebrowUpDownRight, EyebrowQuirkRight;
                public float MouthCornerUpDownLeft, MouthCornerInOutLeft;
                public float MouthCornerUpDownRight, MouthCornerInOutRight;
                public float MouthOpen, MouthWide;
            }

            public OpenSeeFeatures features;

            public OpenSeeData()
            {
                confidence = new float[nPoints];
                points = new Vector2[nPoints];
                points3D = new Vector3[nPoints + 2];
            }

            private static float ReadFloat(byte[] b, ref int o) { float v = BitConverter.ToSingle(b, o); o += 4; return v; }
            private static Quaternion ReadQuaternion(byte[] b, ref int o) { var q = new Quaternion(ReadFloat(b, ref o), ReadFloat(b, ref o), ReadFloat(b, ref o), ReadFloat(b, ref o)); return q; }
            private static Vector3 ReadVector3(byte[] b, ref int o) { return new Vector3(ReadFloat(b, ref o), -ReadFloat(b, ref o), ReadFloat(b, ref o)); }
            private static Vector2 ReadVector2(byte[] b, ref int o) { return new Vector2(ReadFloat(b, ref o), ReadFloat(b, ref o)); }

            public void ReadFromPacket(byte[] b, int o)
            {
                time = BitConverter.ToDouble(b, o); o += 8;
                id = BitConverter.ToInt32(b, o); o += 4;
                cameraResolution = ReadVector2(b, ref o);
                rightEyeOpen = ReadFloat(b, ref o);
                leftEyeOpen = ReadFloat(b, ref o);

                byte got3D = b[o++];
                got3DPoints = (got3D != 0);

                fit3DError = ReadFloat(b, ref o);
                rawQuaternion = ReadQuaternion(b, ref o);
                rawEuler = ReadVector3(b, ref o);

                rotation = rawEuler;
                rotation.z = (rotation.z - 90) % 360;
                rotation.x = -(rotation.x + 180) % 360;

                float x = ReadFloat(b, ref o);
                float y = ReadFloat(b, ref o);
                float z = ReadFloat(b, ref o);
                translation = new Vector3(-y, x, -z);

                for (int i = 0; i < nPoints; i++) confidence[i] = ReadFloat(b, ref o);
                for (int i = 0; i < nPoints; i++) points[i] = ReadVector2(b, ref o);
                for (int i = 0; i < nPoints + 2; i++) points3D[i] = ReadVector3(b, ref o);

                rightGaze = Quaternion.identity;
                leftGaze = Quaternion.identity;

                features = new OpenSeeFeatures();
                features.EyeLeft = ReadFloat(b, ref o);
                features.EyeRight = ReadFloat(b, ref o);
                features.EyebrowSteepnessLeft = ReadFloat(b, ref o);
                features.EyebrowUpDownLeft = ReadFloat(b, ref o);
                features.EyebrowQuirkLeft = ReadFloat(b, ref o);
                features.EyebrowSteepnessRight = ReadFloat(b, ref o);
                features.EyebrowUpDownRight = ReadFloat(b, ref o);
                features.EyebrowQuirkRight = ReadFloat(b, ref o);
                features.MouthCornerUpDownLeft = ReadFloat(b, ref o);
                features.MouthCornerInOutLeft = ReadFloat(b, ref o);
                features.MouthCornerUpDownRight = ReadFloat(b, ref o);
                features.MouthCornerInOutRight = ReadFloat(b, ref o);
                features.MouthOpen = ReadFloat(b, ref o);
                features.MouthWide = ReadFloat(b, ref o);
            }
        }

        private Dictionary<int, OpenSeeData> _dataMap;
        private Socket _socket;
        private byte[] _buffer;
        private Thread _thread;
        private volatile bool _stop;

        public OpenSeeData GetOpenSeeData(int faceId)
        {
            if (_dataMap == null) return null;
            _dataMap.TryGetValue(faceId, out var d);
            return d;
        }

        private void Start()
        {
            if (_dataMap == null) _dataMap = new Dictionary<int, OpenSeeData>();
            _buffer = new byte[65535];
            if (_socket == null)
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPAddress.TryParse(listenAddress, out var ip);
                if (ip == null) ip = IPAddress.Loopback;
                _socket.Bind(new IPEndPoint(ip, listenPort));
                _socket.ReceiveTimeout = 15;
            }
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
        }

        private void Update()
        {
            if (_thread != null && !_thread.IsAlive) Start();
        }

        private void OnDestroy()
        {
            StopReceiver();
        }

        private void OnApplicationQuit()
        {
            StopReceiver();
        }

        private void StopReceiver()
        {
            if (_thread != null)
            {
                _stop = true;
                try { _thread.Join(100); } catch { }
                _stop = false;
                _thread = null;
            }
            try { _socket?.Close(); } catch { }
            _socket = null;
        }

        private void ReceiveLoop()
        {
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            listening = true;
            while (!_stop)
            {
                try
                {
                    int received = _socket.ReceiveFrom(_buffer, SocketFlags.None, ref remote);
                    if (received < 1 || received % packetFrameSize != 0) continue;
                    receivedPackets++;
                    for (int offset = 0; offset < received; offset += packetFrameSize)
                    {
                        var d = new OpenSeeData();
                        d.ReadFromPacket(_buffer, offset);
                        _dataMap[d.id] = d;
                    }
                    trackingData = new OpenSeeData[_dataMap.Count];
                    _dataMap.Values.CopyTo(trackingData, 0);
                }
                catch { }
            }
        }
    }
}
