using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class Win32APIPingTracker : TcpTablePingTracker
    {
        public Win32APIPingTracker(PingConfiguration config) : base(config)
        {
            Task.Run(() => PingLoop(TokenSource.Token));
        }
        
        private async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var rtt = (long)GetAddressLastRTT(SeAddressRaw);
                LastError = (WinError)Marshal.GetLastWin32Error();
                if (LastError == WinError.NO_ERROR)
                    DoNextRTTCalculation(rtt);
                await Task.Delay(3000, token);
            }

            token.ThrowIfCancellationRequested();
        }

        [DllImport("OSBindings.dll", EntryPoint = "?GetAddressLastRTT@@YAKK@Z", SetLastError = true)]
        private static extern ulong GetAddressLastRTT(long address);
    }
}
