using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class LinuxViaWinePingTracker : PingTracker
    {
        private const string Command = "/bin/sh run_linux_program \"ss -i 'dst {0}'\"";

        public LinuxViaWinePingTracker(PingConfiguration config) : base(config)
        {
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                long rtt;
                try
                {
                    rtt = await GetNextRTT();
                }
                catch
                {
                    return; // Not running under wine
                }
                NextRTTCalculation(rtt);
                
                await Task.Delay(3000, token);
            }
        }

        private async Task<long> GetNextRTT()
        {
            var process = Process.Start(new ProcessStartInfo(string.Format(Command, SeAddress))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
            if (process == null)
                return 0;
            process.WaitForExit();

            var res = await process.StandardOutput.ReadToEndAsync();
            var rttStr = res.Substring(res.IndexOf("rtt:", StringComparison.Ordinal), res.IndexOf("ato:", StringComparison.Ordinal) - 1);
            var rtt1 = float.Parse(rttStr.Substring(0, rttStr.IndexOf('/')));
            var rtt2 = float.Parse(rttStr.Substring(rttStr.IndexOf('/')));
            return (long)Math.Floor(rtt1 / rtt2);
        }
    }
}
