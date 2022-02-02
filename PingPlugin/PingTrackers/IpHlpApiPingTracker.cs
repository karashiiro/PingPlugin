using Dalamud.Game.ClientState;
using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class IpHlpApiPingTracker : PingTracker
    {
        public IpHlpApiPingTracker(PingConfiguration config, ClientState clientState) : base(config, clientState)
        {
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (SeAddress != null)
                {
                    var rtt = GetAddressLastRTT(SeAddress);
                    var error = (WinError)Marshal.GetLastWin32Error();
                    if (error == WinError.NO_ERROR)
                    {
                        NextRTTCalculation(rtt);
                    }
                }

                await Task.Delay(3000, token);
            }
        }

        private static ulong GetAddressLastRTT(IPAddress address)
        {
            var addressBytes = address.GetAddressBytes();
            var addressRaw = BitConverter.ToUInt32(addressBytes);

            var hopCount = 0U;
            var rtt = 0U;
            GetRTTAndHopCount(addressRaw, ref hopCount, 51, ref rtt);
            return rtt;
        }

        [DllImport("Iphlpapi.dll", EntryPoint = "GetRTTAndHopCount", SetLastError = true)]
        private static extern ulong GetRTTAndHopCount(uint address, ref uint hopCount, uint maxHops, ref uint rtt);

        private enum WinError
        {
            UNKNOWN = -1,
            NO_ERROR = 0,
            ACCESS_DENIED = 5,
            NOT_ENOUGH_MEMORY = 8,
            OUTOFMEMORY = 14,
            NOT_SUPPORTED = 50,
            INVALID_PARAMETER = 87,
            ERROR_INVALID_NETNAME = 1214,
            WSAEINTR = 10004,
            WSAEACCES = 10013,
            WSAEFAULT = 10014,
            WSAEINVAL = 10022,
            WSAEWOULDBLOCK = 10035,
            WSAEINPROGRESS = 10036,
            WSAEALREADY = 10037,
            WSAENOTSOCK = 10038,
            WSAENETUNREACH = 10051,
            WSAENETRESET = 10052,
            WSAECONNABORTED = 10053,
            WSAECONNRESET = 10054,
            IP_REQ_TIMED_OUT = 11010,
        }
    }
}