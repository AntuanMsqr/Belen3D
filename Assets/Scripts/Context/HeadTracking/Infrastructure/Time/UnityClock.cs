using UnityEngine;
using Hcp.HeadTracking.Domain;

namespace Hcp.HeadTracking.Infrastructure
{
    // IClock backed by UnityEngine.Time. Lives in Infrastructure so Application stays time-free.
    public sealed class UnityClock : IClock
    {
        public double NowSeconds => Time.realtimeSinceStartupAsDouble;
    }
}
