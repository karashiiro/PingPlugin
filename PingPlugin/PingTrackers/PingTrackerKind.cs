using System;

namespace PingPlugin.PingTrackers
{
    public enum PingTrackerKind
    {
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
                PingTrackerKind.COM => "COM",
                PingTrackerKind.IpHlpApi => "Win32 API",
                PingTrackerKind.Packets => "Game packets",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }
    }
}