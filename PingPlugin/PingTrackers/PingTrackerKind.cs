using System;
using CheapLoc;

namespace PingPlugin.PingTrackers
{
    public enum PingTrackerKind
    {
        Aggregate,
        COM,
        IpHlpApi,
        Packets,
    }

    public static class PingTrackerKindExtensions
    {
        public static string FormatName(this PingTrackerKind kind)
        {
            return kind switch
            {
                PingTrackerKind.Aggregate => Loc.Localize("PingTrackerKindAutodetect", string.Empty),
                PingTrackerKind.COM => Loc.Localize("PingTrackerKindCOM", string.Empty),
                PingTrackerKind.IpHlpApi => Loc.Localize("PingTrackerKindWin32API", string.Empty),
                PingTrackerKind.Packets => Loc.Localize("PingTrackerKindPackets", string.Empty),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }
    }
}