using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public abstract class TcpTablePingTracker : PingTracker
    {
        private readonly int pid;

        public bool Reset { get; set; }
        public long SeAddressRaw { get; private set; }
        public IPAddress SeAddress { get; private set; }
        public WinError LastError { get; protected set; }

        protected CancellationTokenSource TokenSource { get; }

        protected TcpTablePingTracker(PingConfiguration config) : base(config)
        {
            TokenSource = new CancellationTokenSource();
            this.pid = Process.GetProcessesByName("ffxiv_dx11")[0].Id;

            UpdateSeAddress();

            LastError = WinError.NO_ERROR;

            Task.Run(() => AddressUpdateLoop(TokenSource.Token));
        }

        private async Task AddressUpdateLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var lastAddress = SeAddress;
                UpdateSeAddress();
                if (!lastAddress.Equals(SeAddress))
                {
                    Reset = true;
                    ResetRTT();
                }
                else
                {
                    Reset = false;
                }
                await Task.Delay(10000, token); // It's probably not that expensive, but it's not like the address is constantly changing, either.
            }

            token.ThrowIfCancellationRequested();
        }

        private void UpdateSeAddress()
        {
            SeAddressRaw = GetProcessHighestPortAddress(this.pid);
            SeAddress = new IPAddress(SeAddressRaw);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TokenSource?.Cancel();
                TokenSource?.Dispose();
                base.Dispose(true);
            }
        }

        [DllImport("OSBindings.dll", EntryPoint = "?GetProcessHighestPortAddress@@YAKH@Z")]
        private static extern long GetProcessHighestPortAddress(int pid);
    }
}