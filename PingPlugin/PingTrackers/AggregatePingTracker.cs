using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class AggregatePingTracker : IPingTracker
    {
        private readonly CancellationTokenSource tokenSource;
        private readonly IEnumerable<IPingTracker> pingTrackers;
        private readonly PingConfiguration config;

        public bool Reset { get; set; }
        public double AverageRTT { get; set; }
        public IPAddress SeAddress { get; set; }
        public long SeAddressRaw { get; set; }
        public WinError LastError { get; set; }
        public ulong LastRTT { get; set; }
        public ConcurrentQueue<float> RTTTimes { get; set; }

        public delegate void PingUpdatedDelegate(PingStatsPayload payload);
        public event PingUpdatedDelegate OnPingUpdated;

        public AggregatePingTracker(PingConfiguration config, params IPingTracker[] pingTrackers)
        {
            this.tokenSource = new CancellationTokenSource();
            this.config = config;

            this.pingTrackers = pingTrackers;
            RTTTimes = new ConcurrentQueue<float>();

            Task.Run(() => UpdateLoop(this.tokenSource.Token));
        }

        private async Task UpdateLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                await Task.Delay(1500, token); // We split the wait in order to prevent running this before the other trackers can complete one loop.

                var bestTracker = GetBestTracker();
                if (bestTracker.Reset)
                {
                    ResetRTT();
                    bestTracker.Reset = false;
                }

                SeAddressRaw = bestTracker.SeAddressRaw;
                SeAddress = bestTracker.SeAddress;
                LastError = bestTracker.LastError;
                LastRTT = bestTracker.LastRTT;

                lock (RTTTimes)
                {
                    RTTTimes.Enqueue(LastRTT);
                
                    while (RTTTimes.Count > this.config.PingQueueSize)
                        RTTTimes.TryDequeue(out _);
                }
                CalcAverage();

                SendMessage();

                await Task.Delay(1500, token);
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

        private IPingTracker GetBestTracker()
        {
            var bestPing = 0UL;
            IPingTracker bestTracker = null;
            foreach (var tracker in this.pingTrackers)
            {
                if (tracker.LastRTT >= bestPing)
                {
                    bestPing = tracker.LastRTT;
                    bestTracker = tracker;
                }
            }
            return bestTracker;
        }

        private void SendMessage()
        {
            var del = OnPingUpdated;
            del?.Invoke(new PingStatsPayload
            {
                AverageRTT = Convert.ToUInt64(AverageRTT),
                LastRTT = LastRTT,
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.tokenSource.Cancel();
                this.tokenSource.Dispose();

                foreach (var pingTracker in pingTrackers)
                {
                    pingTracker.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
