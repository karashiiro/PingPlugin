using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;

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
                PluginLog.Log("OK1");
                var rtt = (long)GetAddressLastRTT(SeAddressRaw);
                PluginLog.Log("OK2");
                LastError = (WinError)Marshal.GetLastWin32Error();
                PluginLog.Log("OK3");
                if (LastError == WinError.NO_ERROR)
                    NextRTTCalculation(rtt);
                await Task.Delay(3000, token);
            }
        }

        [DllImport("OSBindingsV2.dll", EntryPoint = "GetAddressLastRTT", SetLastError = true)]
        private static extern ulong GetAddressLastRTT(long address);
    }
}
