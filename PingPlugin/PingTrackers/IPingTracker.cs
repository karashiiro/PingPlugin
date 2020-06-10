using System;
using System.Collections.Concurrent;
using System.Net;

namespace PingPlugin.PingTrackers
{
    public interface IPingTracker : IDisposable
    {
        bool Reset { get; set; }

        double AverageRTT { get; set; }

        IPAddress SeAddress { get; set; }

        long SeAddressRaw { get; set; }

        WinError LastError { get; set; }

        ulong LastRTT { get; set; }

        ConcurrentQueue<float> RTTTimes { get; set; }
    }
}
