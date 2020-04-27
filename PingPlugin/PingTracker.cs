using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin
{
    public class PingTracker : IDisposable
    {
        private readonly CancellationTokenSource tokenSource;
        private readonly Ping ping;
        private readonly int pid;

        // Everything we want the user to see
        public IPAddress SeAddress { get; private set; }
        public IPStatus LastStatus { get; private set; }
        public long LastRTT { get; private set; }

        public PingTracker()
        {
            this.tokenSource = new CancellationTokenSource();
            this.ping = new Ping();
            this.pid = Process.GetProcessesByName("ffxiv_dx11")[0].Id;

            SeAddress = GetSeAddress().GetAwaiter().GetResult();

            Task.Run(() => PingLoop(this.tokenSource.Token));
            Task.Run(() => CheckServerChangedLoop(this.tokenSource.Token));
        }

        private async Task PingLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                var pingReply = await this.ping.SendPingAsync(SeAddress);
                if (pingReply.Status != IPStatus.Success)
                    LastStatus = pingReply.Status;
                LastRTT = pingReply.RoundtripTime;
                await Task.Delay(3000, token);
            }
        }

        private async Task CheckServerChangedLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                SeAddress = await GetSeAddress();
                await Task.Delay(20000, token);
            }
        }

        private async Task<IPAddress> GetSeAddress()
        {
            var processConnection = await Task.Run(() => GetProcessFirstIngressConnection((ulong)this.pid));
            return new IPAddress(processConnection);
        }

        [DllImport("OSBindings.dll", EntryPoint = "#1")]
        private static extern long GetProcessFirstIngressConnection(ulong pid);

        public void Dispose()
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
            ping.Dispose();
        }
    }
}
