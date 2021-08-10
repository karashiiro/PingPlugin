using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class Win32APIPingTracker : PingTracker
    {
        public Win32APIPingTracker(PingConfiguration config) : base(config)
        {
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                var rtt = GetAddressLastRTT(SeAddressRaw);
                LastError = (WinError)Marshal.GetLastWin32Error();
                if (LastError == WinError.NO_ERROR)
                    NextRTTCalculation(rtt);
                await Task.Delay(3000, token);
            }
        }

        private static long GetAddressLastRTT(long address)
        {
            var hopCount = 0UL;
            var rtt = 0UL;
            NetUtils.GetRTTAndHopCount((ulong)address, ref hopCount, 51, ref rtt);
            return (long)rtt;
        }
    }
}
