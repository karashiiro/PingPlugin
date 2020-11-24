using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class AggregatePingTracker : PingTracker
    {
        private readonly IEnumerable<PingTracker> pingTrackers;

        public delegate void PingUpdatedDelegate(PingStatsPayload payload);
        public event PingUpdatedDelegate OnPingUpdated;

        public AggregatePingTracker(PingConfiguration config, params PingTracker[] pingTrackers) : base(config)
        {
            this.pingTrackers = pingTrackers;
        }

        protected override async Task PingLoop(CancellationToken token)
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

        private PingTracker GetBestTracker()
        {
            var bestPing = 0UL;
            PingTracker bestTracker = null;
            TrampolinePingTracker tpt = null;
            foreach (var tracker in this.pingTrackers)
            {
                if (tracker is TrampolinePingTracker _tpt)
                {
                    tpt = _tpt;
                    continue;
                }

                if (tracker.LastRTT >= bestPing)
                {
                    bestPing = tracker.LastRTT;
                    bestTracker = tracker;
                }
            }

            if (bestPing > 5)
            {
                return bestTracker;
            }
            return tpt;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var pingTracker in pingTrackers)
                {
                    pingTracker.Dispose();
                }

                base.Dispose(true);
            }
        }
    }
}
