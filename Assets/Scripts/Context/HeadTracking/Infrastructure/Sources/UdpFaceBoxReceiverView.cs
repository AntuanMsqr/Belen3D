using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Infrastructure
{
    // Receives a 2D face rectangle over UDP and maps it to head pose without rotation.
    // JSON: {"cx":0..1,"cy":0..1,"size":0..1}  |  CSV: cx,cy,size
    // Optional pixel JSON: {"w":imgW,"h":imgH,"x":px,"y":py,"wbox":boxW,"hbox":boxH}
    public class UdpFaceBoxReceiverView : MonoBehaviour, IHeadPoseSource
    {
        [Header("Network")]
        public int listenPort = 11574;
        public bool useIPv6 = false;

        [Header("Mapping to meters")]
        public float widthMeters = 0.6f;
        public float heightMeters = 0.34f;

        [Header("Depth from box size")]
        public float neutralDepthMeters = 0.6f;
        public float neutralSizeNormalized = 0.25f;
        public Vector2 zClamp = new Vector2(0.2f, 2.5f);
        public float zSmoothTime = 0.12f;

        [Header("Options")]
        public bool invertX = false;
        public bool invertY = false;
        public bool logPackets = false;

        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;
        private readonly object _lock = new object();
        private bool _hasPose;
        private HeadPose _latest;
        private float _zSmoothed, _zVel;

        public event Action<HeadPose> OnPose;

        private void OnEnable() => Start();
        private void OnDisable() => Stop();

        public void Start()
        {
            if (_running) return;
            try
            {
                var endpoint = new IPEndPoint(useIPv6 ? IPAddress.IPv6Any : IPAddress.Any, listenPort);
                _client = useIPv6 ? new UdpClient(AddressFamily.InterNetworkV6) : new UdpClient();
                _client.ExclusiveAddressUse = false;
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _client.Client.Bind(endpoint);

                _running = true;
                _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "UdpFaceBoxReceiver" };
                _thread.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpFaceBoxReceiver] Failed to start: {e.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            _running = false;
            try { _client?.Close(); } catch { }
            _client = null;
            try { _thread?.Join(100); } catch { }
            _thread = null;
        }

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    var data = _client.Receive(ref remote);
                    if (data == null || data.Length == 0) continue;
                    var text = Encoding.UTF8.GetString(data);
                    if (logPackets) Debug.Log($"[UdpFaceBoxReceiver] {text}");

                    if (TryParse(text, out var pose))
                    {
                        lock (_lock)
                        {
                            _latest = pose;
                            _hasPose = true;
                        }
                        OnPose?.Invoke(pose);
                    }
                }
                catch (SocketException)
                {
                    if (!_running) break;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UdpFaceBoxReceiver] Error: {e.Message}");
                }
            }
        }

        public bool TryGetLatest(out HeadPose pose)
        {
            lock (_lock)
            {
                pose = _latest;
                var had = _hasPose;
                _hasPose = false;
                return had;
            }
        }

        private bool TryParse(string text, out HeadPose pose)
        {
            pose = default;
            try
            {
                float cx = 0.5f, cy = 0.5f, size = neutralSizeNormalized; // normalized 0..1
                if (text.StartsWith("{"))
                {
                    float GetFloat(string key, float def)
                    {
                        int i = text.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
                        if (i < 0) return def;
                        int colon = text.IndexOf(":", i, StringComparison.Ordinal);
                        if (colon < 0) return def;
                        int end = text.IndexOfAny(new[] { ',', '}', '\n', '\r' }, colon + 1);
                        string s = (end > 0 ? text.Substring(colon + 1, end - (colon + 1)) : text.Substring(colon + 1)).Trim();
                        if (float.TryParse(s, out var v)) return v; else return def;
                    }
                    float wpx = GetFloat("w", -1), hpx = GetFloat("h", -1);
                    float xpx = GetFloat("x", -1), ypx = GetFloat("y", -1);
                    float wbox = GetFloat("wbox", -1), hbox = GetFloat("hbox", -1);
                    if (wpx > 0 && hpx > 0 && xpx >= 0 && ypx >= 0 && wbox > 0 && hbox > 0)
                    {
                        cx = (xpx + wbox * 0.5f) / wpx;
                        cy = (ypx + hbox * 0.5f) / hpx;
                        size = Mathf.Max(wbox / wpx, hbox / hpx);
                    }
                    else
                    {
                        cx = GetFloat("cx", cx);
                        cy = GetFloat("cy", cy);
                        size = GetFloat("size", size);
                    }
                }
                else
                {
                    var parts = text.Trim().Split(',');
                    if (parts.Length >= 3)
                    {
                        cx = float.Parse(parts[0]);
                        cy = float.Parse(parts[1]);
                        size = float.Parse(parts[2]);
                    }
                }

                cx = Mathf.Clamp01(cx);
                cy = Mathf.Clamp01(cy);
                size = Mathf.Clamp(size, 1e-4f, 1f);

                float nx = (cx - 0.5f) * (invertX ? -1f : 1f);
                float ny = (cy - 0.5f) * (invertY ? 1f : -1f);
                Vector3 pos = new Vector3(nx * widthMeters, ny * heightMeters, neutralDepthMeters);

                float zFromSize = neutralDepthMeters * (neutralSizeNormalized > 1e-4f ? (neutralSizeNormalized / size) : 1f);
                zFromSize = Mathf.Clamp(zFromSize, zClamp.x, zClamp.y);
                if (_zSmoothed <= 0f) _zSmoothed = zFromSize;
                _zSmoothed = Mathf.SmoothDamp(_zSmoothed, zFromSize, ref _zVel, Mathf.Max(0.01f, zSmoothTime));
                pos.z = _zSmoothed;

                var eul = Vector3.zero; // no rotation ever
                var ts = Time.realtimeSinceStartupAsDouble;
                pose = new HeadPose(pos, eul, ts);
                return true;
            }
            catch { return false; }
        }
    }
}
