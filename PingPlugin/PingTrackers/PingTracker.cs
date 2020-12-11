using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;

namespace PingPlugin.PingTrackers
{
    public abstract class PingTracker : IDisposable
    {
        private readonly int pid;

        private readonly CancellationTokenSource tokenSource;
        protected readonly PingConfiguration config;

        public bool Reset { get; set; }
        public double AverageRTT { get; private set; }
        public IPAddress SeAddress { get; protected set; }
        public long SeAddressRaw { get; protected set; }
        public WinError LastError { get; protected set; }
        public ulong LastRTT { get; protected set; }
        public ConcurrentQueue<float> RTTTimes { get; private set; }

        protected PingTracker(PingConfiguration config)
        {
            this.tokenSource = new CancellationTokenSource();
            this.config = config;

            this.pid = Process.GetProcessesByName("ffxiv_dx11")[0].Id;

            UpdateSeAddress();

            LastError = WinError.NO_ERROR;
            RTTTimes = new ConcurrentQueue<float>();

            Task.Run(() => AddressUpdateLoop(this.tokenSource.Token));
            Task.Run(() => PingLoop(this.tokenSource.Token));
        }

        protected void NextRTTCalculation(long nextRTT)
        {
            lock (RTTTimes)
            {
                RTTTimes.Enqueue(nextRTT);
                
                while (RTTTimes.Count > this.config.PingQueueSize)
                    RTTTimes.TryDequeue(out _);
            }
            CalcAverage();

            LastRTT = (ulong)nextRTT;
        }

        protected void CalcAverage() => AverageRTT = RTTTimes.Average();

        protected void ResetRTT() => RTTTimes = new ConcurrentQueue<float>();

        protected abstract Task PingLoop(CancellationToken token);

        private async Task AddressUpdateLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
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
        }

        private void UpdateSeAddress()
        {
            var address = NetUtils.GetXIVServerAddress(this.pid);
            SeAddressRaw = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
            PluginLog.LogDebug("Got FFXIV server address {Address}", SeAddressRaw);
            SeAddress = address;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.tokenSource.Cancel();
                this.tokenSource.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
