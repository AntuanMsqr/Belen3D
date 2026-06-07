using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Infrastructure
{
    // Lightweight UDP receiver for JSON or CSV head pose packets.
    // JSON: {"pos":[x,y,z],"rot":[pitch,yaw,roll],"ts":seconds}  |  CSV: x,y,z,pitch,yaw,roll,ts
    public class UdpHeadPoseReceiverView : MonoBehaviour, IHeadPoseSource
    {
        [Header("Network")]
        public int listenPort = 11573;
        public bool useIPv6 = false;

        [Header("Coordinate System")]
        public float positionScale = 1.0f;
        public bool swapYZ = false;
        public bool invertZ = true;

        [Header("Debug")]
        public bool logPackets = false;

        private UdpClient client;
        private Thread thread;
        private volatile bool running;
        private readonly object gate = new object();
        private bool hasPose;
        private HeadPose latest;

        public event Action<HeadPose> OnPose;

        private void OnEnable() => Start();
        private void OnDisable() => Stop();

        public void Start()
        {
            if (running) return;
            try
            {
                var endpoint = new IPEndPoint(useIPv6 ? IPAddress.IPv6Any : IPAddress.Any, listenPort);
                client = useIPv6 ? new UdpClient(AddressFamily.InterNetworkV6) : new UdpClient();
                client.ExclusiveAddressUse = false;
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(endpoint);

                running = true;
                thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "UdpHeadPoseReceiver" };
                thread.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpHeadPoseReceiver] Failed to start: {e.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            running = false;
            try { client?.Close(); } catch { /* ignore */ }
            client = null;
            try { thread?.Join(100); } catch { /* ignore */ }
            thread = null;
        }

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    var data = client.Receive(ref remote);
                    if (data == null || data.Length == 0) continue;
                    var text = Encoding.UTF8.GetString(data);
                    if (logPackets) Debug.Log($"[UdpHeadPoseReceiver] {text}");

                    if (TryParseJson(text, out var pose) || TryParseCsv(text, out pose))
                    {
                        lock (gate)
                        {
                            latest = pose;
                            hasPose = true;
                        }
                        OnPose?.Invoke(pose);
                    }
                }
                catch (SocketException)
                {
                    if (!running) break;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UdpHeadPoseReceiver] Error: {e.Message}");
                }
            }
        }

        public bool TryGetLatest(out HeadPose pose)
        {
            lock (gate)
            {
                pose = latest;
                var had = hasPose;
                hasPose = false;
                return had;
            }
        }

        private bool TryParseJson(string text, out HeadPose pose)
        {
            pose = default;
            try
            {
                if (!text.Contains("pos") || !text.Contains("rot")) return false;

                int pi = text.IndexOf("[", text.IndexOf("pos"), StringComparison.Ordinal);
                int pe = text.IndexOf("]", pi + 1, StringComparison.Ordinal);
                int ri = text.IndexOf("[", text.IndexOf("rot"), StringComparison.Ordinal);
                int re = text.IndexOf("]", ri + 1, StringComparison.Ordinal);

                if (pi < 0 || pe < 0 || ri < 0 || re < 0) return false;

                var pTokens = text.Substring(pi + 1, pe - pi - 1).Split(',');
                var rTokens = text.Substring(ri + 1, re - ri - 1).Split(',');

                if (pTokens.Length < 3 || rTokens.Length < 3) return false;

                float px = float.Parse(pTokens[0]);
                float py = float.Parse(pTokens[1]);
                float pz = float.Parse(pTokens[2]);
                float rx = float.Parse(rTokens[0]);
                float ry = float.Parse(rTokens[1]);
                float rz = float.Parse(rTokens[2]);

                double ts = 0.0;
                int tsi = text.IndexOf("\"ts\"", StringComparison.Ordinal);
                if (tsi >= 0)
                {
                    int colon = text.IndexOf(":", tsi, StringComparison.Ordinal);
                    if (colon > 0)
                    {
                        int end = text.IndexOfAny(new[] { ',', '}', '\n', '\r' }, colon + 1);
                        var tss = (end > 0 ? text.Substring(colon + 1, end - (colon + 1)) : text.Substring(colon + 1)).Trim();
                        double.TryParse(tss, out ts);
                    }
                }

                var pos = new Vector3(px, py, pz) * positionScale;
                var eul = new Vector3(rx, ry, rz);
                TransformIncoming(ref pos, ref eul);
                pose = new HeadPose(pos, eul, ts > 0 ? ts : Time.realtimeSinceStartupAsDouble);
                return true;
            }
            catch { return false; }
        }

        private bool TryParseCsv(string text, out HeadPose pose)
        {
            pose = default;
            try
            {
                var t = text.Trim();
                var parts = t.Split(',');
                if (parts.Length < 6) return false;
                float px = float.Parse(parts[0]);
                float py = float.Parse(parts[1]);
                float pz = float.Parse(parts[2]);
                float rx = float.Parse(parts[3]);
                float ry = float.Parse(parts[4]);
                float rz = float.Parse(parts[5]);
                double ts = parts.Length > 6 ? double.Parse(parts[6]) : Time.realtimeSinceStartupAsDouble;

                var pos = new Vector3(px, py, pz) * positionScale;
                var eul = new Vector3(rx, ry, rz);
                TransformIncoming(ref pos, ref eul);
                pose = new HeadPose(pos, eul, ts);
                return true;
            }
            catch { return false; }
        }

        private void TransformIncoming(ref Vector3 pos, ref Vector3 eul)
        {
            if (swapYZ)
            {
                (pos.y, pos.z) = (pos.z, pos.y);
                (eul.y, eul.z) = (eul.z, eul.y);
            }
            if (invertZ)
            {
                pos.z = -pos.z;
                eul.z = -eul.z;
            }
        }
    }
}
