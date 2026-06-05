using UnityEngine;
using Belen.HeadTracking.Domain;
using Belen.HeadTracking.Application;

namespace Belen.HeadTracking.Infrastructure
{
    // Resolves which IHeadPoseSource adapter to use for a given SourceKind, attaching the
    // matching View component to the host GameObject. Plain class (Router lives in Infra).
    public static class HeadPoseSourceRouter
    {
        public static IHeadPoseSource Create(SourceKind kind, GameObject host, OpenSee.OpenSee openSee,
                                             bool invertX = false, bool invertY = false,
                                             bool invertZ = false, float positionScale = 1f)
        {
            switch (kind)
            {
                case SourceKind.OpenSeePose:
                {
                    var v = host.AddComponent<OpenSeeHeadPoseSourceView>();
                    v.openSee = openSee;
                    v.invertX = invertX;
                    v.invertY = invertY;
                    v.invertZ = invertZ;
                    v.positionScale = positionScale;
                    return v;
                }
                case SourceKind.OpenSeeFaceBox:
                {
                    var v = host.AddComponent<OpenSeeFaceBoxSourceView>();
                    v.openSee = openSee;
                    v.invertX = invertX;
                    v.invertY = invertY;
                    return v;
                }
                case SourceKind.UdpPose:
                    return host.AddComponent<UdpHeadPoseReceiverView>();
                case SourceKind.UdpFaceBox:
                {
                    var v = host.AddComponent<UdpFaceBoxReceiverView>();
                    v.invertX = invertX;
                    v.invertY = invertY;
                    return v;
                }
                case SourceKind.Keyboard:
                default:
                    return host.AddComponent<KeyboardHeadPoseEmulatorView>();
            }
        }
    }
}
