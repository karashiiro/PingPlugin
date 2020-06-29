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
                var rtt = (long)GetAddressLastRTT(SeAddressRaw);
                LastError = (WinError)Marshal.GetLastWin32Error();
                if (LastError == WinError.NO_ERROR)
                    NextRTTCalculation(rtt);
                await Task.Delay(3000, token);
            }
        }

        [DllImport("OSBindings.dll", EntryPoint = "?GetAddressLastRTT@@YAKK@Z", SetLastError = true)]
        private static extern ulong GetAddressLastRTT(long address);
    }
}
