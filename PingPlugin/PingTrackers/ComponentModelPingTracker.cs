using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class ComponentModelPingTracker : IPingTracker
    {
        private readonly CancellationTokenSource tokenSource;
        private readonly int pid;
        private readonly Ping ping;
        private readonly PingConfiguration config;

        public bool Reset { get; set; }
        public double AverageRTT { get; set; }
        public IPAddress SeAddress { get; set; }
        public long SeAddressRaw { get; set; }
        public WinError LastError { get; set; }
        public ulong LastRTT { get; set; }
        public ConcurrentQueue<float> RTTTimes { get; set; }

        public ComponentModelPingTracker(PingConfiguration config)
        {
            this.config = config;
            this.tokenSource = new CancellationTokenSource();
            this.ping = new Ping();

            this.pid = Process.GetProcessesByName("ffxiv_dx11")[0].Id;

            UpdateSeAddress();

            LastError = WinError.NO_ERROR;
            RTTTimes = new ConcurrentQueue<float>();

            Task.Run(() => PingLoop(this.tokenSource.Token));
            Task.Run(() => AddressUpdateLoop(this.tokenSource.Token));
        }

        private void NextRTTCalculation(long nextRTT)
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

        private async Task PingLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                var pingReply = await this.ping.SendPingAsync(SeAddress);
                if (pingReply.Status == IPStatus.Success)
                    NextRTTCalculation(pingReply.RoundtripTime);
                await Task.Delay(3000, token);
            }
        }

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

        private void CalcAverage()
        {
            AverageRTT = RTTTimes.Average();
        }

        private void ResetRTT()
        {
            RTTTimes = new ConcurrentQueue<float>();
        }

        private void UpdateSeAddress()
        {
            SeAddressRaw = GetProcessHighestPortAddress(this.pid);
            SeAddress = new IPAddress(SeAddressRaw);
        }

        [DllImport("OSBindings.dll", EntryPoint = "?GetProcessHighestPortAddress@@YAKH@Z")]
        private static extern long GetProcessHighestPortAddress(int pid);

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.tokenSource.Cancel();
                this.tokenSource.Dispose();
                this.ping.Dispose();
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
